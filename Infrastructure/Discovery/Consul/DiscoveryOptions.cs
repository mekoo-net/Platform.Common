namespace Platform.Common.Discovery;

/// <summary>
/// 通用服务发现配置（YAML 节 <c>Discovery:*</c>）。仅 Consul 接入 + 本服务注册元数据。
/// Meeko 平台专属的 route/product/schedule manifest 见 <c>Meeko.Common.Discovery.MeekoDiscoveryOptions</c>。
/// </summary>
public sealed class DiscoveryOptions
{
    public const string SectionName = "Discovery";

    public ConsulOptions Consul { get; set; } = new();
    public ServiceOptions? Service { get; set; }
}

/// <summary>Consul agent 接入点。</summary>
public sealed class ConsulOptions
{
    /// <summary>Consul HTTP API 地址，例如 <c>http://consul:8500</c>。</summary>
    public string Address { get; set; } = "http://consul:8500";
    public string? Datacenter { get; set; }
    public string? Token { get; set; }
}

/// <summary>当前服务向 Consul 注册时的元数据。</summary>
public sealed class ServiceOptions
{
    /// <summary>Consul service name（同时作为 YARP cluster id）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 服务对外可达的完整 base URL（含 scheme/host/port）。
    /// 例如 <c>http://keystone:7020/</c>。
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// 可选。MagicOnion gRPC base URL（HTTP/2）。缺省时由注册器从 <see cref="Address"/> 推导（REST 端口 + 1）。
    /// </summary>
    public string? GrpcAddress { get; set; }

    public HealthCheckOptions HealthCheck { get; set; } = new();
}

public sealed class HealthCheckOptions
{
    public string Path { get; set; } = "/health/live";
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);
    public TimeSpan DeregisterAfter { get; set; } = TimeSpan.FromMinutes(1);
}
