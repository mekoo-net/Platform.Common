using Microsoft.Extensions.Configuration;
using Platform.Common.Observability;

namespace Platform.Common.Discovery;

/// <summary>
/// 业务世界声明在 Consul Service Meta 里的编码。注册方（各 ConsulServiceRegistrar）写，
/// 遥测控制台从 catalog 读——key 定义两边共用这一份，不会只改到一边。
/// </summary>
/// <remarks>
/// 以前名单写在控制台的 observability.yaml 里，每加一个服务、每改一次 service.name 都要回去改那份文件，
/// 改漏了的表现是「这个服务发起的调用不出现在任何世界」，没有任何报错。现在由服务自己在注册时声明。
/// <para>Meta 的 key 只允许字母、数字、<c>-</c>、<c>_</c>（不能带点号），值最长 512 字节。</para>
/// <para>声明跟着注册走：正常退出立即注销、崩溃后 1 分钟注销，这个服务就从控制台的世界里消失。</para>
/// </remarks>
public static class WorldServiceMeta
{
    /// <summary>遥测里的 service.name。和 Consul 注册名（<see cref="ServiceKeys"/>）不是一回事。</summary>
    public const string ServiceName = "meeko-otel-service";

    public const string World = "meeko-world";
    public const string Label = "meeko-world-label";
    public const string CaptureNamespace = "meeko-world-capture";
    public const string InsightService = "meeko-world-insight";

    /// <summary>把本服务的世界声明写进 <paramref name="meta"/>。没声明世界时什么都不写，返回 null。</summary>
    /// <param name="effective">
    /// <c>AddPlatformObservability</c> 注册的生效值，含代码里对 ServiceName 的覆盖。
    /// 没走那条路的宿主（Yomi.Gateway 自己装配 OTel）传 null，退回直接读 <c>Observability</c> 配置段。
    /// </param>
    public static WorldDeclaration? Write(
        IDictionary<string, string> meta, IConfiguration configuration, ObservabilityOptions? effective = null)
    {
        ArgumentNullException.ThrowIfNull(meta);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(ObservabilityOptions.SectionName);
        var world = effective?.World
            ?? section.GetSection(nameof(ObservabilityOptions.World)).Get<ObservabilityWorldOptions>()
            ?? new ObservabilityWorldOptions();

        var key = Clean(world.Key);
        if (key is null)
            return null;

        var serviceName = Clean(effective?.ServiceName) ?? Clean(section[nameof(ObservabilityOptions.ServiceName)]);
        if (serviceName is null || serviceName == new ObservabilityOptions().ServiceName)
        {
            // 声明了世界却没有 service.name，控制台会拿一个空名字去 ClickHouse 里匹配，
            // 世界永远是空的。启动时就报，比上线后对着空列表查原因便宜。
            throw new InvalidOperationException(
                $"声明了 {ObservabilityOptions.SectionName}:World:Key='{key}'，但没配 {ObservabilityOptions.SectionName}:ServiceName。");
        }

        var declaration = new WorldDeclaration(
            serviceName,
            key,
            Clean(world.Label) ?? key,
            Clean(world.CaptureNamespace),
            Clean(world.InsightService));

        meta[ServiceName] = declaration.ServiceName;
        meta[World] = declaration.Key;
        meta[Label] = declaration.Label;
        if (declaration.CaptureNamespace is { } capture)
            meta[CaptureNamespace] = capture;
        if (declaration.InsightService is { } insight)
            meta[InsightService] = insight;

        return declaration;
    }

    /// <summary>
    /// 从一个 catalog 实例的 Service Meta（<c>CatalogService.ServiceMeta</c>）里读出世界声明。
    /// 没声明或不完整返回 false。
    /// </summary>
    public static bool TryRead(IDictionary<string, string>? meta, out WorldDeclaration declaration)
    {
        declaration = null!;
        if (meta is null)
            return false;

        var key = Get(meta, World);
        var serviceName = Get(meta, ServiceName);
        if (key is null || serviceName is null)
            return false;

        declaration = new WorldDeclaration(
            serviceName,
            key,
            Get(meta, Label) ?? key,
            Get(meta, CaptureNamespace),
            Get(meta, InsightService));
        return true;
    }

    private static string? Get(IDictionary<string, string> meta, string name) =>
        meta.TryGetValue(name, out var value) ? Clean(value) : null;

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>一个服务实例对自己所属业务世界的声明。</summary>
/// <param name="ServiceName">遥测 service.name，大小写敏感（ClickHouse 存原样字符串）。</param>
/// <param name="Key">世界标识。</param>
/// <param name="Label">显示名。</param>
/// <param name="CaptureNamespace">截获策略的 Consul KV 命名空间；null = 无截获。</param>
/// <param name="InsightService">业务信息服务的 Consul 注册 Key；null = 无业务信息。</param>
public sealed record WorldDeclaration(
    string ServiceName,
    string Key,
    string Label,
    string? CaptureNamespace,
    string? InsightService);
