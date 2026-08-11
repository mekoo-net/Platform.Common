using Npgsql;

namespace Platform.Common.Hosting;

/// <summary>
/// PostgreSQL 连接配置（yaml 节 <c>ConnectionStrings:Database</c>）。
/// </summary>
/// <remarks>
/// 拆成字段而不是一整条连接串，是为了让密码单独成为一个可覆盖的叶子节点：
/// 环境变量 <c>…ConnectionStrings__Database__Password</c> 只覆盖密码，其余留在 yaml 里；
/// 整串形态做不到这一点，改端口就得把密码一起重写一遍。
///
/// 所有可选项都是可空类型：<c>null</c> = 不写入连接串，交给 Npgsql 自己的默认值。
/// 这样模板里列出全部键名做文档用，不会因为"列出来了"就把默认值钉死。
/// </remarks>
public sealed class PostgresConnectionOptions
{
    public const string SectionName = "ConnectionStrings:Database";

    // ---- 端点与身份 ----

    /// <summary>
    /// 可选的整串底座（Npgsql 关键字格式，非 <c>postgresql://</c> URI）。
    /// 托管数据库直接给一整条串时用它，下面任何显式字段都会覆盖串里的同名项。
    /// </summary>
    public string? Url { get; set; }

    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? Database { get; set; }
    public string? Username { get; set; }

    /// <summary>
    /// 密码。<c>null</c> = 未配置，构建时报错；<c>""</c> = 显式声明无密码（trust / peer 认证）。
    /// 两者必须区分：把"忘了填"和"不需要"混成同一个值，只会在生产上换来一条 28P01。
    /// </summary>
    public string? Password { get; set; }

    /// <summary>schema 搜索路径。多 schema 共库时区分服务。</summary>
    public string? SearchPath { get; set; }

    /// <summary>pg_stat_activity 里的连接标识；留空时由 <see cref="PostgresConnectionBuilder"/> 填服务名。</summary>
    public string? ApplicationName { get; set; }

    // ---- TLS ----

    /// <summary>
    /// <c>Disable</c> / <c>Allow</c> / <c>Prefer</c> / <c>Require</c> / <c>VerifyCA</c> / <c>VerifyFull</c>。
    /// Npgsql 8 起 <c>Require</c> 就是"加密但不校验证书"，老的 TrustServerCertificate 已废弃且无效果，
    /// 要校验证书用 <c>VerifyCA</c> / <c>VerifyFull</c>。
    /// </summary>
    public SslMode? SslMode { get; set; }

    /// <summary>CA 证书路径，配合 <c>VerifyCA</c> / <c>VerifyFull</c>。</summary>
    public string? RootCertificate { get; set; }

    /// <summary>客户端证书路径（证书认证，替代密码）。</summary>
    public string? SslCertificate { get; set; }

    /// <summary>客户端私钥路径。</summary>
    public string? SslKey { get; set; }

    /// <summary>客户端私钥的解密口令。</summary>
    public string? SslPassword { get; set; }

    public bool? CheckCertificateRevocation { get; set; }

    /// <summary><c>Postgres</c>（先明文握手再升级）/ <c>Direct</c>（直接 TLS，PG 17+）。</summary>
    public SslNegotiation? SslNegotiation { get; set; }

    /// <summary>通道绑定：<c>Disable</c> / <c>Prefer</c> / <c>Require</c>。防中间人降级。</summary>
    public ChannelBinding? ChannelBinding { get; set; }

    /// <summary>
    /// 强制服务端使用的认证方式，如 <c>scram-sha-256</c>；不匹配就断开，防降级到明文。
    /// 这一项只能走一等字段——Npgsql 10 的连接串索引器不认这个键名，
    /// <see cref="Parameters"/> 设不进去（已实测）。
    /// </summary>
    public string? RequireAuth { get; set; }

    /// <summary>密码文件路径（.pgpass），密码不落 yaml 的一种走法。</summary>
    public string? Passfile { get; set; }

    // ---- 超时（秒）----

    /// <summary>建立连接的超时。Npgsql 默认 15。</summary>
    public int? Timeout { get; set; }

    /// <summary>单条命令的超时。Npgsql 默认 30；设 0 为不限。</summary>
    public int? CommandTimeout { get; set; }

    /// <summary>Npgsql 层的 keepalive 间隔（秒），0 / null 关闭。</summary>
    public int? KeepAlive { get; set; }

    /// <summary>启用 TCP 层 keepalive。开了才轮到下面两项生效。</summary>
    public bool? TcpKeepAlive { get; set; }

    /// <summary>连接空闲多久开始发 TCP keepalive 探测（秒）。</summary>
    public int? TcpKeepAliveTime { get; set; }

    /// <summary>TCP keepalive 探测间隔（秒），默认取 <see cref="TcpKeepAliveTime"/>。</summary>
    public int? TcpKeepAliveInterval { get; set; }

    /// <summary>
    /// 取消命令后等服务端响应的时长（毫秒）。0 = 发完取消就断开，-1 = 无限等。
    /// CommandTimeout 到点之后卡在这里的时间不算在 CommandTimeout 里，排查"超时了还不返回"看这项。
    /// </summary>
    public int? CancellationTimeout { get; set; }

    // ---- 连接池 ----

    public bool? Pooling { get; set; }
    public int? MinPoolSize { get; set; }
    public int? MaxPoolSize { get; set; }

    /// <summary>空闲连接被回收前的存活秒数。Npgsql 默认 300。</summary>
    public int? ConnectionIdleLifetime { get; set; }

    /// <summary>池清理扫描间隔（秒）。Npgsql 默认 10。</summary>
    public int? ConnectionPruningInterval { get; set; }

    /// <summary>连接最大存活秒数，到点强制关闭。0 = 不限。走 pgbouncer / 故障转移时才需要。</summary>
    public int? ConnectionLifetime { get; set; }

    // ---- 行为 ----

    public bool? Multiplexing { get; set; }

    /// <summary>归还连接时跳过 DISCARD ALL。只有确认没人改会话状态时才能开。</summary>
    public bool? NoResetOnClose { get; set; }

    /// <summary>异常里带上参数值。含用户数据，生产环境别开。</summary>
    public bool? IncludeErrorDetail { get; set; }

    /// <summary>自动 prepare 的语句上限，0 = 关闭。</summary>
    public int? MaxAutoPrepare { get; set; }

    /// <summary>同一条语句出现几次后才自动 prepare。仅在 <see cref="MaxAutoPrepare"/> 非 0 时有意义。</summary>
    public int? AutoPrepareMinUsages { get; set; }

    /// <summary>读写分离时挑连接：<c>primary</c> / <c>standby</c> / <c>prefer-standby</c> / <c>any</c>。</summary>
    public string? TargetSessionAttributes { get; set; }

    /// <summary>多主机时随机打散连接顺序，配合 <see cref="TargetSessionAttributes"/> 做负载均衡。</summary>
    public bool? LoadBalanceHosts { get; set; }

    /// <summary>主机角色（主/备）缓存多久重新探测（秒）。故障转移后多久能认出新主。</summary>
    public int? HostRecheckSeconds { get; set; }

    /// <summary>读缓冲字节数。大结果集调它。</summary>
    public int? ReadBufferSize { get; set; }

    /// <summary>写缓冲字节数。大批量写入调它。</summary>
    public int? WriteBufferSize { get; set; }

    /// <summary>
    /// 连接建立时下发给服务端的 GUC，形如 <c>-c statement_timeout=5000 -c lock_timeout=2000</c>。
    /// 注意这是**服务端参数**，跟 <see cref="Parameters"/>（客户端驱动关键字）不是一回事。
    /// </summary>
    public string? Options { get; set; }

    /// <summary>会话时区，如 <c>Asia/Shanghai</c>。</summary>
    public string? Timezone { get; set; }

    /// <summary>
    /// 逃生舱：任意 Npgsql 关键字原样透传（键名用 Npgsql 官方写法，如 <c>"Options"</c>、<c>"Channel Binding"</c>）。
    /// 最后应用，覆盖上面所有字段。加了这个，遇到冷门参数就不必再改这个类。
    /// </summary>
    public Dictionary<string, string?> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
