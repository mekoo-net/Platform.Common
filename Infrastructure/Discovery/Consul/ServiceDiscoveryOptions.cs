namespace Platform.Common.Discovery;

/// <summary>
/// 挂在平台网关后面的服务随注册发布的声明（yaml 节 <c>Discovery</c>，宿主也可以在代码里 PostConfigure）。
/// 平台各服务和各产品服务是同一份：平台网关按 <see cref="Gateway"/> 建路由，Billing 按 <see cref="Products"/>
/// 建产品目录，Jobs 按 <see cref="Schedules"/> 排任务。Consul 地址在 <c>Connection:Consul</c>，
/// 本服务的 Key 写死在宿主代码里（<see cref="ServiceKeys"/>）。
/// </summary>
public sealed class ServiceDiscoveryOptions
{
    public const string SectionName = "Discovery";

    public GatewayManifestOptions Gateway { get; set; } = new();
    public List<ProductDeclaration> Products { get; set; } = [];
    public List<ScheduledTaskDeclaration> Schedules { get; set; } = [];
}

public sealed class GatewayManifestOptions
{
    public List<RouteManifest> Routes { get; set; } = [];
}

/// <summary>单条前缀路由：前缀 + 鉴权 policy + 是否剥离前缀。</summary>
public sealed class RouteManifest
{
    public string Prefix { get; set; } = string.Empty;

    /// <summary>
    /// YARP <c>RouteConfig.AuthorizationPolicy</c>：
    /// <c>Anonymous</c> / <c>Default</c> / 已注册的 policy 名（如 <c>StaffOnly</c>）。
    /// </summary>
    public string Policy { get; set; } = "Default";

    public bool StripPrefix { get; set; }
}

/// <summary>本服务向 Billing 声明的计费产品。</summary>
public sealed class ProductDeclaration
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>本服务要 Jobs 按 cron 触发的定时任务。</summary>
public sealed class ScheduledTaskDeclaration
{
    public string TaskKey { get; set; } = string.Empty;
    public string Cron { get; set; } = string.Empty;
}
