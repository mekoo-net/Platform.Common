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

    /// <summary>
    /// 本服务发起的调用在遥测控制台里归哪个业务世界。随 Consul 注册写进 Service Meta
    /// （<see cref="Discovery.WorldServiceMeta"/>），控制台从 catalog 发现，不再维护一份名单。
    /// <see cref="ObservabilityWorldOptions.Key"/> 为空 = 不归任何世界（纯下游服务）。
    /// </summary>
    public ObservabilityWorldOptions World { get; set; } = new();
}

/// <summary>
/// 本服务所属的业务世界。由服务在代码里用 <see cref="Default"/> 给出，
/// yaml <c>Observability:World</c> 可以整段覆盖。同一世界的各服务应声明同一个 Key 和 Label。
/// </summary>
public sealed class ObservabilityWorldOptions
{
    /// <summary>
    /// 代码里给的默认声明。yaml 已经写了 <see cref="Key"/> 就整段以 yaml 为准、一个字段都不混，
    /// 否则 yaml 改了 Key 却沿用代码的 Label，会拼出一个谁都没声明过的世界。
    /// </summary>
    /// <remarks>
    /// 放代码而不是 yaml：部署机上的 yaml 是单独维护的种子文件，新镜像不会更新它，
    /// 写在仓库 yaml 里的声明到不了线上。
    /// </remarks>
    public ObservabilityWorldOptions Default(
        string key, string label, string? captureNamespace = null, string? insightService = null)
    {
        if (!string.IsNullOrWhiteSpace(Key))
            return this;

        Key = key;
        Label = label;
        CaptureNamespace = captureNamespace;
        InsightService = insightService;
        return this;
    }

    /// <summary>世界的稳定标识，进 URL（<c>?domain=yomi.gateway</c>），改了旧链接就失效。</summary>
    public string? Key { get; set; }

    /// <summary>控制台上显示的名字。缺省用 Key。</summary>
    public string? Label { get; set; }

    /// <summary>
    /// 截获策略所在的 Consul KV 命名空间（<c>{CaptureNamespace}/config/dump</c>）。
    /// 截获引擎在执行 LLM 调用的下游（OneApi.Gateway），所以由发起这些调用的服务声明。空 = 没有截获能力。
    /// </summary>
    public string? CaptureNamespace { get; set; }

    /// <summary>
    /// 按 trace 查业务信息（<c>ITraceInsightService</c>）的服务的 Consul 注册 Key，
    /// 不是 service.name。空 = 这个世界没有业务信息。
    /// </summary>
    public string? InsightService { get; set; }
}
