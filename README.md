# Platform.Common

全平台共享基础设施：不放业务代码，不放跨服务契约。git submodule，上游
`github.com/mekoo-net/Platform.Common`，检出在 `Meeko.Platform/common/`、
`{Demux,Tavern,ToApi,Observability}/Common/`。

## 策略：必须用公共组件，禁止自造

**硬规则，不是建议。** 人写的代码、AI 生成的代码、评审，一律按这条执行。

1. **先查，再写。** 接任何基础设施能力——连接配置、缓存、服务发现、gRPC 通道、鉴权与身份、
   遥测与日志、JSON、结果与错误、响应信封、分页、健康检查——先查下面的[组件清单](#组件清单)，
   产品仓再查 [Product.Common](../Product.Common/README.md#组件清单)。清单里有的，必须用。
2. **不在服务里另建同类东西。** 典型的违规：
   - 自己 `GetSection("ConnectionStrings:…").Get<T>()`，自建 `XxxConnectionConfig`；
   - 自己拼 Npgsql / Redis 连接串、解析 `redis://`、设连接超时与重试；
   - 自定义 `Observability` / `Logging` / `Cors` 之类的配置类——**没人引用也不行**，
     死代码会被下一个人照抄；
   - 自建结果类型、错误码、响应信封、分页 DTO、`JsonSerializerOptions`；
   - 自己配 gRPC channel 的 keepalive、重连退避。
3. **不够用就改公共库，不要绕。** 缺功能、默认值不合适、语义有坑，都在任一检出里改公共库，
   提交推上游，其余仓库 `git submodule update --remote` 跟指针。以下做法一律禁止：
   复制一份到服务里改；在服务里包一层去"修正"公共库的行为；在多个检出里分别编辑。
4. **服务里只留真正私有的东西。** 判断标准：换一个服务是否也需要它？需要，就属于公共库。
   例：Tavern 在 EF 连接串上加 `Search Path` 给 Orleans 用，只有 Tavern 有 Orleans，留在
   Tavern（`OrleansDatabase`）；而"读 PG 配置、拼连接串"每个服务都要，只能用公共库。
5. **yaml 跟着公共模型走。** 只写公共模型认的字段与形态（如 `ConnectionStrings:*` 的拆字段写法，
   见 [Connections/README.md](Infrastructure/Hosting/Connections/README.md)），
   不为单个服务另起键名。

### 反面例子

`Tavern.Gateway/Config/`（已删除）自建了 Database / Redis / Consul 三个连接模型：自己实现
Url + 连接池合并，抄了一份 Redis URL 归一化，外加三个无人引用的配置类。后果是：yaml 多出
第四种写法；平台的 Redis 超时默认值没生效；Redis 库号得单独一路传参；Consul 写错键名时
不报错，LLM 静默退化成 Echo。

## 分层

- **Platform.Common**：基础设施本体。平台服务（Meeko.Platform 下各服务）直接用。
- **Product.Common**：引用 Platform.Common，给产品仓（Yomi / Tavern / ToApi）提供统一入口。
  产品仓的分层规则见 [Product.Common/README.md](../Product.Common/README.md#分层规则)。

## 组件清单

| 需求 | 用这个 |
|---|---|
| PostgreSQL 连接串 | `configuration.GetRequiredDbConnectionString()` / `GetDbConnectionString()` |
| Redis 连接串 / `ConfigurationOptions` | `GetRedisConnectionString()` / `GetRedisConfigurationOptions()` |
| Redis 硬依赖（连不上不启动） | `services.AddRequiredRedis(configuration)` |
| RabbitMQ | `RabbitMqConnectionResolver.GetRequired()` + `cfg.ApplyPlatformRabbitMqHost()` |
| Consul 客户端 | `builder.AddServiceDiscovery()`；Consul 可选的服务用 `ConsulConnectionResolver.Resolve()` |
| 本服务注册地址 / 服务 Key | `LocalServiceInstance.Resolve()` / `ServiceKeys` |
| gRPC 通道 | `GrpcClientDefaults.CreateChannel()` |
| Kestrel | `ConfigurePlatformKestrel()` |
| 缓存 | `AddPlatformCache()` + yaml `FusionCache:Profiles` + `ApplyProfile()` |
| 遥测 / 日志 | `AddPlatformObservability()` / `UsePlatformSerilog()` / `PlatformActivitySource` |
| 健康检查 | `MapHealthChecks()`（`/health/live`） |
| 关联 ID | `UseCorrelationId()` / `CorrelationContext` |
| JSON | REST 用 `AddPlatformJsonOptions()`；手写序列化用 `PlatformJson.Web` / `PlatformJson.Exact` |
| 结果与错误 | `Result` / `Error` / `ErrorCode` + `ToHttp()` |
| 响应信封 | `ApiEnvelope` / `ItemsEnvelope` / `EpochMillis` |
| 分页 | `PageRequest.Normalize()` / `PagedList<T>` |
| 内部 RPC 鉴权 | `Infrastructure/Auth/InternalRpc/` |
| 网关透传身份 | `AddGatewayForwardedAuth()` / `AddCurrentAuth()` / `AddCurrentActor()` / `GatewayClientIp` |
| 权限 | `AddPermissionAuthorization()` + `RequirePermission(code)` / `AddStaffPermissions()` / `AddAccountPermissions()` |

清单没列到的，先在 `Core/`、`Infrastructure/` 里搜一遍再动手。新增公共组件时把它补进这张表，
否则下一个人找不到，还是会自己造。
