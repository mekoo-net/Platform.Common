namespace Platform.Common.Observability;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    /// <summary>service.name 资源属性。每个服务必须不同。</summary>
    public string ServiceName { get; set; } = "meeko.unknown";

    /// <summary>OTLP/gRPC 端点；为空表示禁用导出。</summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>额外 ActivitySource，按需注册（如 Yarp.ReverseProxy）。</summary>
    public string[] AdditionalSources { get; set; } = Array.Empty<string>();

    public string[] AdditionalMeters { get; set; } = Array.Empty<string>();

    /// <summary>是否启用 EF / Redis / gRPC 仪表化（按服务取舍）。</summary>
    public bool EnableEntityFrameworkInstrumentation { get; set; } = true;
    public bool EnableRedisInstrumentation { get; set; } = true;
    public bool EnableGrpcClientInstrumentation { get; set; } = true;
    public bool EnableHangfireInstrumentation { get; set; }
}
