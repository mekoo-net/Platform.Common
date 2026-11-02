using Microsoft.Extensions.Configuration;
using Platform.Common.Observability;

namespace Platform.Common.Discovery;

/// <summary>
/// 业务层级树在 Consul Service Meta 里的编码。注册方（各 ConsulServiceRegistrar）写，
/// 遥测控制台从 catalog 读出每个在线服务的路径、拼成整棵树——key 定义两边共用这一份，不会只改到一边。
/// </summary>
/// <remarks>
/// 以前名单写在控制台的 observability.yaml 里，每加一个服务、每改一次 service.name 都要回去改那份文件，
/// 改漏了的表现是「这个服务发起的调用不出现在任何世界」，没有任何报错。现在由服务自己在注册时声明。
/// <para>以前声明的是扁平的世界 Key，同一个产品的网关和平台服务各写各的，控制台就把一个业务拆成了两个。
/// 现在声明的是层级路径，归属由父路径决定，不靠两处碰巧写成同一个字符串。</para>
/// <para>Meta 的 key 只允许字母、数字、<c>-</c>、<c>_</c>（不能带点号），值最长 512 字节。</para>
/// <para>声明跟着注册走：正常退出立即注销、崩溃后 1 分钟注销，这个节点就从控制台的树上消失。</para>
/// </remarks>
public static class WorldServiceMeta
{
    /// <summary>遥测里的 service.name。和 Consul 注册名（<see cref="ServiceKeys"/>）不是一回事。</summary>
    public const string ServiceName = "meeko-otel-service";

    /// <summary>层级路径，如 <c>meeko/product/yomi/gateway</c>。</summary>
    public const string Path = "meeko-path";

    /// <summary>与路径逐段对应的显示名，如 <c>Meeko/Product/Yomi/Gateway</c>。</summary>
    public const string PathLabels = "meeko-path-labels";

    public const string CaptureNamespace = "meeko-world-capture";
    public const string InsightService = "meeko-world-insight";

    /// <summary>把本服务在树上的位置写进 <paramref name="meta"/>。没声明路径时什么都不写，返回 null。</summary>
    /// <param name="effective">
    /// <c>AddPlatformObservability</c> 注册的生效值，含代码里对 ServiceName 的覆盖。
    /// 没走那条路的宿主传 null，退回直接读 <c>Observability</c> 配置段。
    /// </param>
    public static WorldDeclaration? Write(
        IDictionary<string, string> meta, IConfiguration configuration, ObservabilityOptions? effective = null)
    {
        ArgumentNullException.ThrowIfNull(meta);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(ObservabilityOptions.SectionName);
        var worldSection = section.GetSection(nameof(ObservabilityOptions.World));

        var world = effective?.World
            ?? worldSection.Get<ObservabilityWorldOptions>()
            ?? new ObservabilityWorldOptions();

        if (Clean(world.Path) is null)
            return null;

        if (!TryNormalize(world.Path, world.Labels, out var path, out var labels, out var error))
            throw new InvalidOperationException($"{ObservabilityOptions.SectionName}:World 声明有误：{error}");

        var serviceName = Clean(effective?.ServiceName) ?? Clean(section[nameof(ObservabilityOptions.ServiceName)]);
        if (serviceName is null || serviceName == new ObservabilityOptions().ServiceName)
        {
            // 声明了路径却没有 service.name，控制台会拿一个空名字去 ClickHouse 里匹配，
            // 这个组件永远是空的。启动时就报，比上线后对着空列表查原因便宜。
            throw new InvalidOperationException(
                $"声明了 {ObservabilityOptions.SectionName}:World:Path='{path}'，但没配 {ObservabilityOptions.SectionName}:ServiceName。");
        }

        var declaration = new WorldDeclaration(
            serviceName,
            path,
            labels,
            Clean(world.CaptureNamespace),
            Clean(world.InsightService));

        meta[ServiceName] = serviceName;
        meta[Path] = declaration.Path;
        meta[PathLabels] = declaration.Labels;
        if (declaration.CaptureNamespace is { } capture)
            meta[CaptureNamespace] = capture;
        if (declaration.InsightService is { } insight)
            meta[InsightService] = insight;

        return declaration;
    }

    /// <summary>
    /// 从一个 catalog 实例的 Service Meta（<c>CatalogService.ServiceMeta</c>）里读出它在树上的位置。
    /// 没声明或不合法返回 false——别人注册的东西，读的一方只跳过、不抛。
    /// </summary>
    public static bool TryRead(IDictionary<string, string>? meta, out WorldDeclaration declaration)
    {
        declaration = null!;
        if (meta is null)
            return false;

        // service.name 可以没有：OneApi 的 provider 不导出遥测，但它们照样是树上的节点。
        var serviceName = Get(meta, ServiceName);

        if (!TryNormalize(Get(meta, Path), Get(meta, PathLabels), out var path, out var labels, out _))
        {
            // 显示名写坏了不该让整个组件从树上消失，退回用段名。
            if (!TryNormalize(Get(meta, Path), null, out path, out labels, out _))
                return false;
        }

        declaration = new WorldDeclaration(
            serviceName,
            path,
            labels,
            Get(meta, CaptureNamespace),
            Get(meta, InsightService));
        return true;
    }

    /// <summary>校验路径并补齐显示名，两者段数一致。段名进 URL，只放小写字母、数字和 <c>-</c>。</summary>
    private static bool TryNormalize(
        string? rawPath, string? rawLabels, out string path, out string labels, out string? error)
    {
        path = labels = "";
        error = null;

        var segments = (Clean(rawPath) ?? "").Split('/');
        if (segments.Length < 2)
        {
            error = $"路径 '{rawPath}' 至少要两段（业务/组件）";
            return false;
        }

        foreach (var segment in segments)
        {
            if (segment.Length == 0 || !segment.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
            {
                error = $"路径 '{rawPath}' 的段 '{segment}' 只能用小写字母、数字和 -";
                return false;
            }
        }

        var labelSegments = Clean(rawLabels)?.Split('/');
        if (labelSegments is not null && labelSegments.Length != segments.Length)
        {
            error = $"显示名 '{rawLabels}' 有 {labelSegments.Length} 段，路径 '{rawPath}' 有 {segments.Length} 段";
            return false;
        }

        path = string.Join('/', segments);
        labels = string.Join('/', segments.Select((s, i) => Clean(labelSegments?[i]) ?? s));
        return true;
    }

    private static string? Get(IDictionary<string, string> meta, string name) =>
        meta.TryGetValue(name, out var value) ? Clean(value) : null;

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>一个服务实例对自己在业务层级树上位置的声明。</summary>
/// <param name="ServiceName">
/// 遥测 service.name，大小写敏感（ClickHouse 存原样字符串）。null = 这个组件不导出遥测（OneApi 的 provider）。
/// 本库的 <see cref="WorldServiceMeta.Write"/> 总会写；只有别的仓库自己写 Meta 时才可能没有。
/// </param>
/// <param name="Path">层级路径，如 <c>meeko/product/yomi/gateway</c>，已校验，至少两段。</param>
/// <param name="Labels">与 <paramref name="Path"/> 逐段对应的显示名，段数相同。</param>
/// <param name="CaptureNamespace">截获策略的 Consul KV 命名空间；null = 无截获。</param>
/// <param name="InsightService">业务信息服务的 Consul 注册 Key；null = 无业务信息。</param>
public sealed record WorldDeclaration(
    string? ServiceName,
    string Path,
    string Labels,
    string? CaptureNamespace,
    string? InsightService)
{
    public IReadOnlyList<string> Segments => Path.Split('/');

    public IReadOnlyList<string> SegmentLabels => Labels.Split('/');

    /// <summary>所属业务：路径去掉最后一段。父路径相同的组件属于同一个业务。</summary>
    public string Business => Path[..Path.LastIndexOf('/')];

    /// <summary>组件：路径最后一段。</summary>
    public string Component => Path[(Path.LastIndexOf('/') + 1)..];
}
