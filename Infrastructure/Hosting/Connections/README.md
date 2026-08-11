# 连接配置规范（ConnectionStrings）

全平台服务读 PostgreSQL / Redis 连接的唯一约定。改这里等于改五个仓库，先看
[submodule 传播](#传播)。

## 为什么拆字段

整串 `Database: "Host=…;Password=…"` 有两个硬伤：

1. **密码没法单独覆盖。** 配置系统按叶子节点覆盖，整串是一个叶子，
   环境变量想改密码就得把 host/port/dbname 一起重写。拆开之后
   `…ConnectionStrings__Database__Password` 只动密码。
2. **参数无处可放。** 连接池、超时、SSL 这些迟早要调，塞进串里既没有类型也没有文档，
   拼错一个关键字要到运行时才知道。

## 写法

三种形态都合法，`ConnectionSectionReader` 按顺序判定：

```yaml
# ① 规范写法：拆字段
ConnectionStrings:
  Database:
    Host: postgres
    Port: 5432
    Database: meeko
    Username: meeko
    Password: ""          # 部署时填，见下方"密码"
    MaxPoolSize: 30
  Redis:
    Host: redis
    Port: 6379
    Database: 0

# ② 整串底座 + 字段覆盖：托管数据库给了一整条串，但连接池要自己调
ConnectionStrings:
  Database:
    Url: "Host=pg.example.com;Port=5432;Database=meeko;Username=u;Password=p"
    MaxPoolSize: 30       # 覆盖串里的同名项

# ③ 纯整串：历史配置，仍然读得动，新服务不要用
ConnectionStrings:
  Database: "Host=postgres;Port=5432;Database=meeko;Username=meeko;Password=meeko"
  Redis: "redis://redis:6379"
```

合并顺序恒定：**`Url` → 显式字段 → `Parameters`**，后者覆盖前者。

`Database: null` / 键缺失 / 键都在但值全 `null` —— 一律等同**未配置**，
`GetDbConnectionString()` 返回 `null`，`GetRequiredDbConnectionString()` 抛错。

## 密码

`null` 和 `""` 不是一回事，别混：

| 值 | 含义 |
|---|---|
| `Password: null` | 未配置 → PostgreSQL 构建时报错（Redis 允许，容器里的 redis 默认无密码） |
| `Password: ""` | 显式声明无密码（trust / peer 认证） |

仓库里提交的 yaml 一律留 `null`。真实密码在部署机的 `/opt/meeko/<服务>.yaml`。

## 参数覆盖到什么程度

**不写就是驱动默认值。** Model 里除端点/身份外全是可空类型，`null` = 不写进连接串，
不会把某个默认值悄悄钉死。所以模板可以只写四五行，以后要调哪个再加哪个。

覆盖率是数出来的，不是估的（反射 `NpgsqlConnectionStringBuilder` 和
`ConfigurationOptions` 的全部可配项比对）：

| | 驱动可配项 | 一等字段 | 走 `Parameters` | 够不着 |
|---|---|---|---|---|
| PostgreSQL | 55（另有 4 个已废弃） | 42 | 13 | 0 |
| Redis | 32（连接串可解析的键） | 27 | 1（`tunnel`） | 4，全是 SE.Redis 已废弃的空操作 |

一等字段的取舍标准，新增时照这个来：

1. **逃生舱够不着的** —— 必须提升。`Require Auth` 就是：Npgsql 10 的连接串索引器不认这个键，
   只能用属性赋值，实测过。
2. **安全相关的** —— TLS、认证、证书不该藏在 stringly-typed 的袋子里，写错了没人发现。
3. **成对出现的** —— 有 `MaxAutoPrepare` 就得有 `AutoPrepareMinUsages`，有 `TcpKeepAlive`
   就得有它的两个间隔。只给一半，调的人会以为另一半不支持。
4. **生产常调的** —— 连接池、超时、缓冲区。

Redis 侧剩下那 18 个 `ConfigurationOptions` 属性（`SocketManager`、`CommandMap`、
`LoggerFactory`、`ReconnectRetryPolicy` 等）是对象/委托类型，任何连接串都表达不了，
要用只能在代码里设，不是配置能覆盖的范畴。

### 两个同名不同义的坑

- `TcpKeepAlive`：PostgreSQL 侧是 `bool`（开关，配合 `TcpKeepAliveTime` / `TcpKeepAliveInterval`），
  Redis 侧也是 `bool` 但走内核探测，而 Redis 的 `KeepAlive` 是按秒发 PING。别照搬。
- `Options`：PostgreSQL 的 `Options` 字段是下发给**服务端**的 GUC
  （`-c statement_timeout=5000`），跟 `Parameters`（客户端驱动关键字）完全两回事。

## 参数

字段覆盖不了的冷门关键字走 `Parameters`，键名用驱动的官方写法，最后应用：

```yaml
ConnectionStrings:
  Database:
    Host: postgres
    Parameters:
      "Channel Binding": "require"
  Redis:
    Host: redis
    Parameters:
      tiebreaker: "__Booksleeve_TieBreak"
```

PostgreSQL 侧的键名由 `NpgsqlConnectionStringBuilder` 校验，写错立刻抛错并带上原键名。
Redis 侧 `ConfigurationOptions.Parse` 会**静默忽略**不认识的键——这是 StackExchange.Redis
的行为，改不了，用之前对着文档核一遍。

## 代码入口

| 需求 | 调用 |
|---|---|
| PG 连接串（EF / Hangfire / 健康检查） | `configuration.GetRequiredDbConnectionString()` |
| PG 连接串，允许缺省 | `configuration.GetDbConnectionString()` |
| Redis 连接串（FusionCache / 分布式缓存） | `configuration.GetRequiredRedisConnectionString()` |
| Redis `ConfigurationOptions` | `configuration.GetRedisConfigurationOptions()` |
| Redis 连上去 | `RedisConnectionResolver.ConnectMultiplexer(configuration, requireConnected)` |
| 拿结构化对象自己处理 | `configuration.GetPostgresOptions()` / `GetRedisOptions()` |
| RabbitMQ 连接参数 | `RabbitMqConnectionResolver.GetRequired(configuration)` |
| RabbitMQ 落到 MassTransit | `cfg.ApplyPlatformRabbitMqHost(settings)` |

**服务里不要自己 `GetSection("ConnectionStrings:Database").Get<T>()`。** 那样绕过合并顺序和校验，
就会长出第四种写法——`Tavern.Gateway/Config/TavernConfigurationExtensions.cs` 是前车之鉴：
它自己实现了一遍 Url+连接池，还把 Redis URL 归一化整段抄了过去。

`ApplicationName`（PG）和 `ClientName`（Redis）留空时自动取 `Observability:ServiceName`，
`pg_stat_activity` / `CLIENT LIST` 里能直接认出是哪个服务占着连接，不用再猜。

## RabbitMQ

模型、读取、MassTransit 装配都在 `Infrastructure/Messaging/`，与 PG、Redis 同库同规范：
读取走 `ConnectionSectionReader.TryRead`，标量整串 / 结构化映射 / 未配置的判定完全一致。

Platform.Common 因此带上 `MassTransit.RabbitMQ`，凡是引到本库的项目都会传递拿到
MassTransit + RabbitMQ.Client（≈5.6 MB）。**这是有意的**：本库就是平台基础设施，
按依赖裁剪去拆项目得不偿失。版本钉在本库自己的 `Directory.Packages.props`（8.5.10，
与 Meeko.Platform 根一致），另外四个仓跟指针即可，不需要改各自的中央版本。

```yaml
ConnectionStrings:
  RabbitMq:
    Host: rabbitmq
    Port: 5672
    VirtualHost: "/"
    Username: meeko
    Password: null
    # 可选：Heartbeat / RequestedConnectionTimeout / RequestedChannelMax /
    #       MaxMessageSize / PublisherConfirmation / Ssl 一族
```

两点与 PG、Redis 不同：

- **没有 `Parameters` 逃生舱。** 参数是通过 MassTransit 的 `cfg.Host(...)` 逐项方法调用下发的，
  没有"连接串关键字"这层可透传。`RabbitMqConnectionOptions` 的字段就是全部可配面，要加参数就加字段。
- **不再有 guest/guest 默认值。** 旧实现在 URI 缺少用户信息时静默用 guest——那个账号在
  非 localhost 的 broker 上必然 ACCESS_REFUSED，而且掩盖了"根本没配"。现在 `Username` /
  `Password` 缺失直接报错。

## Redis 超时

`RedisConnectionBuilder.ApplyPlatformDefaults` 统一把连接/命令超时放宽到 10s、重试 5 次
（SE.Redis 自带 5s 在容器网络里偏紧）。拿现成连接串直接连的调用方也经过这里，
避免同一个集群因为入口不同有两套超时。yaml 里显式写 `ConnectTimeout` 等字段可覆盖。

`AbortOnConnectFail` 由调用方决定，不要在 yaml 里配：硬依赖 Redis 的服务用
`services.AddRequiredRedis(configuration)`，启动期连不上直接不启动。

## 传播

`Platform.Common` 是 submodule，检出在 5 个仓库里
（`Meeko.Platform/common/`、`{Demux,Tavern,ToApi,Observability}/Common/`）。
**在任一检出里改完提交推上去，其余仓库 `git submodule update --remote` 跟指针**，
不要分别编辑多个检出。
