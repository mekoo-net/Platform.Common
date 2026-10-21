namespace Platform.Common.Discovery;

/// <summary>
/// Consul 连接配置（yaml 节 <c>ConnectionStrings:Consul</c>）。
/// 形态与读取规则跟 PostgreSQL / Redis / RabbitMQ 一致，见
/// <c>Platform.Common/Infrastructure/Hosting/Connections/README.md</c>。
/// </summary>
/// <remarks>
/// 以前挂在 <c>Discovery:Consul:Address</c>。Consul 跟数据库、broker 一样是"连到哪台基础设施"，
/// 部署机上要跟着环境改的就这一类；放进 <c>ConnectionStrings</c> 后运维只需改一个节，
/// 而 <c>Discovery:*</c> 只剩随代码走的 manifest（路由、产品、定时任务）。
/// </remarks>
public sealed class ConsulConnectionOptions
{
    public const string SectionName = "ConnectionStrings:Consul";

    /// <summary>
    /// 可选的整串底座：<c>http://consul:8500</c>。下面任何显式字段都会覆盖它。
    /// <c>https://</c> 会同时把 <see cref="Ssl"/> 置真、默认端口切到 8501。
    /// </summary>
    public string? Url { get; set; }

    public string? Host { get; set; }
    public int? Port { get; set; }

    /// <summary>走 HTTPS 访问 agent。显式开关；用 <c>https://</c> 底座时自动为真。</summary>
    public bool? Ssl { get; set; }

    /// <summary>留空 = agent 所在的数据中心。</summary>
    public string? Datacenter { get; set; }

    /// <summary>ACL token。<c>null</c> = 不带；Consul 没开 ACL 时就是这样。</summary>
    public string? Token { get; set; }
}
