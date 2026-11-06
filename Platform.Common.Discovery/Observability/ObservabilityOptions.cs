namespace Platform.Common.Observability;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    /// <summary>service.name 资源属性。每个服务必须不同。</summary>
    public string ServiceName { get; set; } = "meeko.unknown";

    /// <summary>
    /// OTLP/gRPC 端点；<c>null</c> 表示禁用导出。由 AddPlatformObservability 从 <c>Connection:Otlp</c>
    /// 解析（Platform.Common 的 <c>OtlpConnectionResolver</c>），setter 不公开，<c>Observability</c> 节绑定不到它。
    /// </summary>
    public Uri? OtlpEndpoint { get; internal set; }

    /// <summary>额外 ActivitySource，按需注册（如 Yarp.ReverseProxy）。</summary>
    public string[] AdditionalSources { get; set; } = Array.Empty<string>();

    public string[] AdditionalMeters { get; set; } = Array.Empty<string>();

    /// <summary>是否启用 EF / Redis / gRPC 仪表化（按服务取舍）。</summary>
    public bool EnableEntityFrameworkInstrumentation { get; set; } = true;
    public bool EnableRedisInstrumentation { get; set; } = true;
    public bool EnableGrpcClientInstrumentation { get; set; } = true;
    public bool EnableHangfireInstrumentation { get; set; }

    /// <summary>
    /// 本服务在业务层级树上的位置。随 Consul 注册写进 Service Meta
    /// （<see cref="Discovery.WorldServiceMeta"/>），控制台读整棵树汇总，不再维护一份名单。
    /// <see cref="ObservabilityWorldOptions.Path"/> 为空 = 不在树上（纯下游服务）。
    /// </summary>
    public ObservabilityWorldOptions World { get; set; } = new();
}

/// <summary>
/// 本服务在业务层级树上的位置，如 <c>meeko/product/yomi/gateway</c>：最后一段是组件，前面是它所属的业务。
/// 父路径相同的组件就是同一个业务——Yomi 的网关和平台服务都挂在 <c>meeko/product/yomi</c> 下，
/// 控制台据此把它们归到一起，而不是各自成一个世界。
/// 由服务在代码里用 <see cref="Product"/> / <see cref="Platform"/> 给出，yaml <c>Observability:World</c> 可以整段覆盖。
/// </summary>
public sealed class ObservabilityWorldOptions
{
    public const string Root = "meeko";
    private const string RootLabel = "Meeko";

    /// <summary>
    /// 产品的一个组件：<c>meeko/product/{product}/{component}</c>。
    /// 同一产品的各组件 <paramref name="productLabel"/>、截获、业务信息应一致，不一致时控制台取第一个并打警告。
    /// </summary>
    public ObservabilityWorldOptions Product(
        string product, string productLabel, string component, string componentLabel,
        string? captureNamespace = null, string? insightService = null) =>
        Default(
            $"{Root}/product/{product}/{component}",
            $"{RootLabel}/Product/{productLabel}/{componentLabel}",
            captureNamespace,
            insightService);

    /// <summary>平台的一个服务：<c>meeko/platform/{component}</c>。</summary>
    public ObservabilityWorldOptions Platform(string component, string componentLabel) =>
        Default($"{Root}/platform/{component}", $"{RootLabel}/Platform/{componentLabel}");

    /// <summary>
    /// 代码里给的默认声明。yaml 已经写了 <see cref="Path"/> 就整段以 yaml 为准、一个字段都不混，
    /// 否则 yaml 改了路径却沿用代码的显示名，会拼出一个谁都没声明过的节点。
    /// </summary>
    /// <remarks>
    /// 放代码而不是 yaml：部署机上的 yaml 是单独维护的种子文件，新镜像不会更新它，
    /// 写在仓库 yaml 里的声明到不了线上。
    /// </remarks>
    public ObservabilityWorldOptions Default(
        string path, string? labels = null, string? captureNamespace = null, string? insightService = null)
    {
        if (!string.IsNullOrWhiteSpace(Path))
            return this;

        Path = path;
        Labels = labels;
        CaptureNamespace = captureNamespace;
        InsightService = insightService;
        return this;
    }

    /// <summary>
    /// 层级路径，段之间用 <c>/</c>，至少两段（业务 + 组件），段名只用小写字母、数字和 <c>-</c>。
    /// 业务那部分进控制台的 URL（<c>?domain=meeko/product/yomi</c>），改了旧链接就失效。
    /// </summary>
    public string? Path { get; set; }

    /// <summary>与 <see cref="Path"/> 逐段对应的显示名，同样用 <c>/</c> 分隔；空着的段用段名。</summary>
    public string? Labels { get; set; }

    /// <summary>
    /// 截获策略所在的 Consul KV 命名空间（<c>{CaptureNamespace}/config/dump</c>）。
    /// 截获引擎在执行 LLM 调用的下游（OneApi.Gateway），所以由发起这些调用的服务声明。空 = 没有截获能力。
    /// </summary>
    public string? CaptureNamespace { get; set; }

    /// <summary>
    /// 按 trace 查业务信息（<c>ITraceInsightService</c>）的服务的 Consul 注册 Key，
    /// 不是 service.name。空 = 这个业务没有业务信息。
    /// </summary>
    public string? InsightService { get; set; }
}
