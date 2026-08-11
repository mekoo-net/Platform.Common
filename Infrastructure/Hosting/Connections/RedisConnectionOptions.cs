using StackExchange.Redis;

namespace Platform.Common.Hosting;

/// <summary>
/// Redis 连接配置（yaml 节 <c>ConnectionStrings:Redis</c>）。字段拆分理由同
/// <see cref="PostgresConnectionOptions"/>。
/// </summary>
/// <remarks>
/// 与 PostgreSQL 不同，Redis 允许完全不配密码（compose 里的 redis 就没有 requirepass），
/// 所以 <see cref="Password"/> 为 <c>null</c> 是合法的，不报错。
/// </remarks>
public sealed class RedisConnectionOptions
{
    public const string SectionName = "ConnectionStrings:Redis";

    // ---- 端点与身份 ----

    /// <summary>
    /// 可选的整串底座：<c>redis://</c> / <c>rediss://</c> URI，或 StackExchange.Redis 原生串
    /// （<c>host:port,password=…</c>）。下面任何显式字段都会覆盖它。
    /// </summary>
    public string? Url { get; set; }

    public string? Host { get; set; }
    public int? Port { get; set; }

    /// <summary>集群 / 哨兵多节点，形如 <c>redis-1:6379</c>。非空时取代 <see cref="Host"/> + <see cref="Port"/>。</summary>
    public List<string> Endpoints { get; set; } = [];

    /// <summary>
    /// Redis 6 ACL 用户名（<c>AUTH &lt;user&gt; &lt;pass&gt;</c>）。留空则走默认用户的单参数 AUTH。
    /// <c>redis://user:pass@host</c> 形态的 URI 也会拆出用户名填到这里。
    /// </summary>
    public string? User { get; set; }

    public string? Password { get; set; }

    /// <summary>默认 db 编号。</summary>
    public int? Database { get; set; }

    // ---- TLS ----

    public bool? Ssl { get; set; }

    /// <summary>证书校验用的主机名，与连接地址不一致时才需要。</summary>
    public string? SslHost { get; set; }

    /// <summary>允许的 TLS 版本，如 <c>Tls12</c>、<c>Tls12, Tls13</c>。留空跟随 OS 默认。</summary>
    public System.Security.Authentication.SslProtocols? SslProtocols { get; set; }

    public bool? CheckCertificateRevocation { get; set; }

    // ---- 客户端行为 ----

    /// <summary>CLIENT LIST 里的连接名；留空时由 <see cref="RedisConnectionBuilder"/> 填服务名。</summary>
    public string? ClientName { get; set; }

    /// <summary>连接超时（毫秒）。</summary>
    public int? ConnectTimeout { get; set; }

    /// <summary>同步命令超时（毫秒）。</summary>
    public int? SyncTimeout { get; set; }

    /// <summary>异步命令超时（毫秒）。</summary>
    public int? AsyncTimeout { get; set; }

    public int? ConnectRetry { get; set; }

    /// <summary>Redis 层心跳间隔（秒）。</summary>
    public int? KeepAlive { get; set; }

    /// <summary>
    /// 启用 TCP 层 keepalive。跟 <see cref="KeepAlive"/> 不是一回事：那个是按秒发 PING，
    /// 这个是布尔开关、走内核探测。同名不同义，别照搬 PostgreSQL 那边的写法。
    /// </summary>
    public bool? TcpKeepAlive { get; set; }

    /// <summary>
    /// pub/sub 频道前缀。多个应用共用一个 Redis 时用它隔离广播，
    /// 否则 A 服务的失效通知会打到 B 服务。
    /// </summary>
    public string? ChannelPrefix { get; set; }

    /// <summary>
    /// 首次连接失败是否直接抛错。默认 <c>false</c>（后台重连），
    /// 需要"连不上就不启动"的服务走 <see cref="RedisHostingExtensions.AddRequiredRedis"/>，
    /// 由它在启动期显式置 true，不要靠这里改。
    /// </summary>
    public bool? AbortOnConnectFail { get; set; }

    /// <summary>允许 FLUSHDB / CONFIG 等管理命令。</summary>
    public bool? AllowAdmin { get; set; }

    /// <summary>哨兵模式的 master 名。</summary>
    public string? ServiceName { get; set; }

    /// <summary>RESP 协议版本：<c>Resp2</c> / <c>Resp3</c>。留空由服务端版本推断。</summary>
    public RedisProtocol? Protocol { get; set; }

    /// <summary>走代理时的类型：<c>None</c> / <c>Twemproxy</c> / <c>Envoyproxy</c>。代理不支持的命令会被屏蔽。</summary>
    public Proxy? Proxy { get; set; }

    /// <summary>多节点选主用的 tie-breaker key。单节点没有意义，集群下配错会选错主。</summary>
    public string? TieBreaker { get; set; }

    /// <summary>拓扑变更广播用的 pub/sub 频道。</summary>
    public string? ConfigurationChannel { get; set; }

    /// <summary>主动复查拓扑的间隔（秒）。</summary>
    public int? ConfigCheckSeconds { get; set; }

    /// <summary>连接前先把主机名解析成 IP。DNS 不稳时打开。</summary>
    public bool? ResolveDns { get; set; }

    /// <summary>手工指定服务端版本，跳过探测——服务端不给版本号（部分托管 Redis）时用。</summary>
    public Version? DefaultVersion { get; set; }

    /// <summary>启用响应完整性校验，代价是额外开销。怀疑连接串包时用。</summary>
    public bool? HighIntegrity { get; set; }

    /// <summary>用 CLIENT SETINFO 上报客户端库名版本。部分托管 Redis 不认这条命令，需要关掉。</summary>
    public bool? SetClientLibrary { get; set; }

    /// <summary>
    /// 逃生舱：任意 StackExchange.Redis 连接串关键字原样透传（如 <c>"tiebreaker"</c>）。最后应用。
    /// </summary>
    public Dictionary<string, string?> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
