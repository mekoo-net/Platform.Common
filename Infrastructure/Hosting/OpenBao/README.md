# 密钥放 OpenBao

数据库 / Redis / RabbitMQ 密码、JWT 私钥、ClientSecret、签名密钥一律放 OpenBao；yaml 只留地址、端口、
超时这类不怕看见的东西，密码字段在 yaml 里留 `null`（含义见 [Connections/README.md](../Connections/README.md#密码)）。

## 服务怎么接

`Program.cs` 里加载 yaml 之后加一行，参数是本服务读的路径——写死在代码里，和 ServiceKeys 一样是代码和 OpenBao
布局之间的约定，不交给部署机改：

```csharp
builder.Configuration.AddYamlFile("/config/Keystone.yaml", optional: false, reloadOnChange: true);
builder.Configuration.AddOpenBao("Meeko/Keystone", "Meeko/Shared");   // 按顺序叠加，后面的覆盖前面的
```

怎么连 OpenBao 写在 yaml 的**顶层 `OpenBao` 节**（`OpenBaoOptions`），字段写法和 Consul 一样。
它是引导配置，连上 OpenBao 之前就要用，只能在本地 yaml；**不放 `Connection`**——`Connection` 整节都可以放进
OpenBao，OpenBao 自己的连接放进去就成了"从 OpenBao 里读怎么连 OpenBao"。

```yaml
OpenBao:
  Host: 127.0.0.1
  Port: 8200                     # 缺省 8200；开了 TLS 加 Ssl: true
  # 凭据二选一：
  # AppRoleFile: "/run/secrets/openbao-approle.json"   部署机用这个（缺省值）
  # RoleId: "…" / SecretId: "…"                        只限 Debug 的仓库 yaml
  # RefreshInterval: "00:05:00"
  # StartupTimeout: "00:02:00"
```

- 没配 `OpenBao` 节：`AddOpenBao` 什么都不做，只读配置文件。
- 配了但没有凭据（RoleId / SecretId 为空、凭据文件不存在）：记一条错误，同样只读配置文件，不在启动时干等。
- 仓库 yaml 里的开发值保留着，当 OpenBao 连不上时的回退。键层级和下面 OpenBao 的路径层级一一对应，
  开发值放在哪、OpenBao 里就去哪条路径找。

## 路径层级就是配置层级

yaml 里每个 `Secrets` 条目指向一个路径，服务读它本身和它下面的**整棵子树**；每条密钥相对这个路径的
位置就是配置前缀。UI 里逐层点进去看到的，就是 yaml 的层级：

```
Meeko/Keystone/Connection/Database            {"Password": "…"}                        → Connection:Database:Password
Meeko/Keystone/Connection/Redis               {"Password": "…"}                        → Connection:Redis:Password
Meeko/Keystone/Connection/RabbitMq            {"Password": "…"}                        → Connection:RabbitMq:Password
Meeko/Keystone/Jwt/Account                    {"PrivateKeyPem": "…"}                   → Jwt:Account:PrivateKeyPem
Meeko/Keystone/Jwt/Staff                      {"PrivateKeyPem": "…"}                   → Jwt:Staff:PrivateKeyPem
Meeko/Shared/Jwt/PublicKeys                   {"Account": "…", "Staff": "…"}           → Shared:Jwt:PublicKeys:Account / Staff
Meeko/Shared/PlatformApiKey                   {"SigningKey": "base64…"}                → Shared:PlatformApiKey:SigningKey
```

- **只放密码这类叶子**。配置按叶子合并，`Host` / `Port` / `Username` 留在 yaml，不会被整段替换。
- **`Shared` 下的条目保留 `Shared:` 前缀**，服务自己的树落在配置根上。共享的东西在配置里也挂在 `Shared:` 下，
  和服务自己的同名节分开：Keystone 的 `Jwt` 节放私钥（`Meeko/Keystone/Jwt/…`，只有它读），
  共享的 `Shared:Jwt` 只放公钥（`Meeko/Shared/Jwt/…`，两边读）。配置路径去掉挂载点就是 OpenBao 路径。
- 条目指向的那条密钥本身（`Meeko/Keystone` 如果存在）的键原样用，没有前缀。
- 一条密钥里的值也可以是嵌套 JSON（UI 的 JSON 模式），按冒号展开；PEM 在 JSON 里换行写成 `\n`。
- **路径一律 PascalCase**，挂载点也是（`Meeko/Keystone`），和配置键、yaml 文件名（`Keystone.yaml`）同一套写法。
  OpenBao 的路径区分大小写，`meeko/keystone` 和 `Meeko/Keystone` 是两条：大小写写错就读不到，这个条目退回配置文件。
  只有 OpenBao 自己定的段是小写——KV v2 策略里的 `data/` / `metadata/`、认证挂载 `approle`。
- 同一个键在两层都写了，更深的路径赢。这本身是录入错误，只是结果确定。
- 一个条目下面一条都读不到（路径写错）或策略不放行，**只有这个条目**退回配置文件，记一条错误，其余条目照常生效。

| 路径 | 放什么 |
|---|---|
| `Meeko/<服务>/…` | 平台各服务自己的密钥：`Meeko/Keystone`、`Meeko/Gateway`、`Meeko/Bff`…… |
| `Meeko/Shared/<名字>` | 确实要多个服务共用的，一项一条，只给用到的服务读权限。目前是 Keystone 签、Gateway 验都要的两项：`Jwt/PublicKeys`（JWT 公钥，按 `Account` / `Staff` 标名字）和 `PlatformApiKey`（API Key 签名密钥 `SigningKey`，只有一把）。两边各存一份就又回到了"同一个东西配两次"。**私钥不在这里**，只在 `Meeko/Keystone` |
| `OneApi/<服务>/…` | OneApi 独立挂载，不放 Meeko 下——它是纯下游服务，不属于任何业务世界 |

## 身份与权限

每个服务一个 AppRole、一份策略，**只放行自己的子树**。读子树要两种权限：`data/` 上的 `read`（读每一条）
和 `metadata/` 上的 `list`（列出下一层）。漏给 `list` 不会悄悄只读到一半，而是整个条目退回配置文件并记错误：

```hcl
# policy svc-keystone
path "Meeko/data/Keystone"           { capabilities = ["read"] }
path "Meeko/data/Keystone/*"         { capabilities = ["read"] }
path "Meeko/metadata/Keystone/*"     { capabilities = ["list"] }
path "Meeko/data/Shared"             { capabilities = ["read"] }
path "Meeko/data/Shared/*"           { capabilities = ["read"] }
path "Meeko/metadata/Shared/*"       { capabilities = ["list"] }
```

`svc-gateway` 同理：自己的 `Meeko/Gateway` 加上同样三行 `Shared`。它读不到 `Meeko/Keystone/*`，拿不到私钥。
以后 `Shared` 下出现某个服务不该读的项，就给那个服务按项列条目（`Meeko/Shared/Jwt`）、按项放权限，配置前缀不变。

别写成 `Meeko/data/Keystone*`（不带斜杠）——那会连 `Meeko/data/Keystone-xxx` 一起放行。

AppRole 绑定本机来源（服务是 host 网络，来源就是 127.0.0.1），凭据泄露到别处也用不了。
Debug 用的那个 AppRole（RoleId / SecretId 写在仓库 yaml 里）是从开发机远程连的，`secret_id_bound_cidrs` 要放行开发机网段，
它的策略也只该给开发用的那份数据——凭据进了仓库，就当它是公开的。
凭据文件 `{"role_id":"…","secret_id":"…"}` 单独挂进该服务的容器，**不能放 `/opt/meeko`**：
那个目录整个挂进每个服务，放进去等于所有服务都拿得到所有服务的凭据。

## 运行时行为

- **启动**：`AddOpenBao(...)` 那一行同步拉取。连不上按退避重试到 `StartupTimeout`（给 OpenBao 解封的时间），
  还不行就**退回配置文件**：记一条错误，用 yaml 里的值起来，之后每轮刷新照常重试。生产 yaml 里密码是 `null`，
  用到它的组件照样报"未配置"——退回不会让服务带着空密码悄悄连上。
- **每一轮**：读凭据文件 → AppRole 登录 → 逐个读密钥 → 吊销本轮令牌。不留长期令牌，不用管续期；
  凭据文件每轮重读，轮换 secret_id 不用重启。
- **刷新**：每 `RefreshInterval` 重读一次，变了触发配置重载（`IOptionsMonitor` 能看到）。
  启动时就构建好的东西（连接串、连接池、签名密钥环）不会跟着变，**改了这类密钥要重启服务**。
- **刷新失败**：保留手里那份，记一条警告。启动时退回了配置文件的，第一次刷新成功就把 OpenBao 的值叠上来并触发重载。
- **日志**：任何时候都不写值，只记地址、路径和键的个数。

## 传播

这是 Platform.Common 子模块。VaultSharp 的版本登记在子模块自己的 `Directory.Packages.props` 里，
其他仓库跟进指针就带上了，不用各自再加。
