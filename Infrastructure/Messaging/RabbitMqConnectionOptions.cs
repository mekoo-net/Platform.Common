using System.Security.Authentication;

namespace Platform.Common.Messaging;

/// <summary>
/// RabbitMQ 连接配置（yaml 节 <c>ConnectionStrings:RabbitMq</c>）。
/// 形态与读取规则跟 PostgreSQL / Redis 一致，见
/// <c>Platform.Common/Infrastructure/Hosting/Connections/README.md</c>。
/// </summary>
/// <remarks>
/// 这里没有 <c>Parameters</c> 逃生舱：连接参数是通过 MassTransit 的
/// <c>cfg.Host(...)</c> 逐项方法调用下发的，没有"连接串关键字"这层可以透传，
/// 所以本类的字段就是全部可配面。要加参数就往这里加字段。
/// </remarks>
public sealed class RabbitMqConnectionOptions
{
    public const string SectionName = "ConnectionStrings:RabbitMq";

    // ---- 端点与身份 ----

    /// <summary>
    /// 可选的整串底座：<c>amqp://user:pass@host:5672/vhost</c>。下面任何显式字段都会覆盖它。
    /// <c>amqps://</c> 会同时把 <see cref="Ssl"/> 置真、默认端口切到 5671。
    /// </summary>
    public string? Url { get; set; }

    public string? Host { get; set; }
    public int? Port { get; set; }

    /// <summary>虚拟主机，默认 <c>/</c>。多环境共用一个 broker 时靠它隔离。</summary>
    public string? VirtualHost { get; set; }

    public string? Username { get; set; }

    /// <summary>
    /// 密码。<c>null</c> = 未配置，构建时报错；<c>""</c> = 显式无密码。
    /// 注意这里**不再**默认 guest/guest——那个默认值在生产 broker 上只会换来一条
    /// ACCESS_REFUSED，而且掩盖了"根本没配"这件事。
    /// </summary>
    public string? Password { get; set; }

    /// <summary>broker 管理界面里显示的连接名；留空时取 <c>Observability:ServiceName</c>。</summary>
    public string? ConnectionName { get; set; }

    // ---- TLS ----

    /// <summary>显式开关；用 <c>amqps://</c> 时自动为真。</summary>
    public bool? Ssl { get; set; }

    /// <summary>证书校验用的主机名，与连接地址不一致时才需要。</summary>
    public string? SslServerName { get; set; }

    /// <summary>允许的 TLS 版本，如 <c>Tls12</c>。留空跟随 OS 默认。</summary>
    public SslProtocols? SslProtocol { get; set; }

    /// <summary>客户端证书路径（mTLS）。</summary>
    public string? SslCertificatePath { get; set; }

    public string? SslCertificatePassphrase { get; set; }

    // ---- 连接调优 ----

    /// <summary>心跳间隔（秒）。0 = 关闭。经过 NAT / LB 时别关，否则半开连接要等 TCP 超时才发现。</summary>
    public int? Heartbeat { get; set; }

    /// <summary>建立连接的超时（毫秒）。</summary>
    public int? RequestedConnectionTimeout { get; set; }

    /// <summary>单连接上的最大 channel 数。MassTransit 每个 endpoint 占一个。</summary>
    public int? RequestedChannelMax { get; set; }

    /// <summary>最大帧字节数，0 = 不限。</summary>
    public long? RequestedFrameMax { get; set; }

    /// <summary>单条消息字节上限，防止超大消息打爆内存。</summary>
    public long? MaxMessageSize { get; set; }

    /// <summary>等待 broker 响应 RPC 操作的时长。</summary>
    public TimeSpan? ContinuationTimeout { get; set; }

    /// <summary>发布确认。关掉能提速，但发布方就不知道消息有没有真的落到 broker。</summary>
    public bool? PublisherConfirmation { get; set; }
}
