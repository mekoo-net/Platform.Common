namespace Platform.Common.Observability;

/// <summary>
/// OTLP 采集端连接配置（yaml 节 <c>Connection:Otlp</c>），只支持 OTLP/gRPC。
/// 形态与读取规则跟 PostgreSQL / Redis / Consul 一致，见
/// <c>Platform.Common/Infrastructure/Hosting/Connections/README.md</c>。
/// </summary>
/// <remarks>
/// 以前挂在 <c>Observability:OtlpEndpoint</c>。collector 跟数据库、Consul 一样是"连到哪台基础设施"，
/// 部署机上要跟着环境改的就这一类；放进 <c>Connection</c> 后运维只需改一个节，
/// 而 <c>Observability:*</c> 只剩随代码走的声明（service.name、业务世界）。
/// </remarks>
public sealed class OtlpConnectionOptions
{
    public const string SectionName = "Connection:Otlp";

    /// <summary>
    /// 可选的整串底座：<c>http://collector:4317</c>。下面任何显式字段都会覆盖它。
    /// <c>https://</c> 会同时把 <see cref="Ssl"/> 置真。
    /// </summary>
    public string? Url { get; set; }

    /// <summary>留空 = 不导出遥测（日志退回 stdout）。</summary>
    public string? Host { get; set; }

    public int? Port { get; set; }

    /// <summary>走 TLS 连 collector。显式开关；用 <c>https://</c> 底座时自动为真。</summary>
    public bool? Ssl { get; set; }
}
