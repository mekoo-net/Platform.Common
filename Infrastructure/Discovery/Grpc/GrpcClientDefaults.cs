using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
using Microsoft.Extensions.Logging;

namespace Platform.Common.Discovery;

/// <summary>
/// 平台内部 gRPC/MagicOnion 通道的统一弹性配置，所有服务间通道共用，避免各处手搓参数不一致。
///
/// <para>三层自愈机制（缺一不可，参考 gRPC 官方 + 大厂服务间通信规范）：</para>
/// <list type="number">
///   <item><b>HTTP/2 KeepAlive Ping</b>：周期性 ping 让 NAT/LB 空闲超时不会悄悄掐断连接，
///         半死连接（对端进程没了但 TCP 没收到 RST）能被快速探测并主动重连，
///         而不是等到下一次真正发请求时才超时。</item>
///   <item><b>有界重连退避</b>：subchannel 连接失败后按指数退避重连，但<b>封顶 <see cref="MaxReconnectBackoff"/></b>。
///         grpc-dotnet 默认封顶 <b>120s</b>——一旦下游/路由短暂不可用，退避会涨到 2 分钟，
///         即使下游早已恢复、Consul 也重新解析出地址，旧 subchannel 仍卡在 2 分钟退避里不重连，
///         表现就是"必须重启进程才好"。封到 10s 后最长 10s 自愈。</item>
///   <item><b>连接级重试</b>：对 <see cref="StatusCode.Unavailable"/>（连接建立失败、对端不可达）做有限次重试，
///         平滑下游滚动重启 / 实例切换期间的瞬时抖动，调用方不直接看到 Unavailable。</item>
/// </list>
///
/// <para><b>关键约束（已踩坑）</b>：重连退避仅在「客户端负载均衡开启 + 使用 <see cref="GrpcChannelOptions.HttpHandler"/>
/// （而非 HttpClient）」时生效；一旦传 HttpClient，grpc-dotnet 绕过连接管理器，
/// <see cref="GrpcChannelOptions.InitialReconnectBackoff"/> / <see cref="GrpcChannelOptions.MaxReconnectBackoff"/> 会被忽略。
/// 因此本助手固定使用 <see cref="SocketsHttpHandler"/>。</para>
/// </summary>
public static class GrpcClientDefaults
{
    /// <summary>空闲连接的 KeepAlive ping 间隔（内网带宽充足，取激进值以更快发现掉线）。</summary>
    public static readonly TimeSpan KeepAlivePingDelay = TimeSpan.FromSeconds(10);

    /// <summary>KeepAlive ping 未收到回应判定连接已死的等待时间（内网取激进值，掉线 ~10~13s 内即被探测）。</summary>
    public static readonly TimeSpan KeepAlivePingTimeout = TimeSpan.FromSeconds(3);

    /// <summary>建立 TCP/HTTP2 连接的超时（内网建连很快，取 5s 更早 fail-fast 进入重连退避，避免长时间挂起）。</summary>
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    /// <summary>首次重连退避。</summary>
    public static readonly TimeSpan InitialReconnectBackoff = TimeSpan.FromSeconds(1);

    /// <summary>重连退避封顶（核心：把 grpc-dotnet 默认的 120s 压到 10s，保证下游恢复后最长 10s 自愈）。</summary>
    public static readonly TimeSpan MaxReconnectBackoff = TimeSpan.FromSeconds(10);

    /// <summary>对 Unavailable 的最大尝试次数（含首次）。</summary>
    public const int MaxRetryAttempts = 3;

    /// <summary>
    /// 创建带 KeepAlive、连接超时、多 HTTP/2 连接的 <see cref="SocketsHttpHandler"/>。
    /// 每个通道独立一个 handler（廉价），由通道侧负载均衡管理 subchannel 连接生命周期，故空闲连接不回收。
    /// </summary>
    public static SocketsHttpHandler CreateHandler() => new()
    {
        EnableMultipleHttp2Connections = true,
        ConnectTimeout = ConnectTimeout,
        // 交给 LB/subchannel 管理连接生命周期；靠 KeepAlive 保活而非空闲回收，避免反复建连。
        PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
        KeepAlivePingDelay = KeepAlivePingDelay,
        KeepAlivePingTimeout = KeepAlivePingTimeout,
        // Always：即使没有在途请求也发 ping，确保空闲 subchannel 的死连接也能被及时发现。
        KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always,
    };

    /// <summary>
    /// 构建统一的 <see cref="GrpcChannelOptions"/>：明文 h2c + RoundRobin 负载均衡 + KeepAlive handler
    /// + 有界重连退避 + Unavailable 重试。
    /// </summary>
    public static GrpcChannelOptions CreateChannelOptions(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory)
        => new()
        {
            // 平台内部服务间走 h2c（明文 HTTP/2），与服务注册写入的 http gRPC 端点一致。
            Credentials = ChannelCredentials.Insecure,
            ServiceProvider = serviceProvider,
            LoggerFactory = loggerFactory,
            HttpHandler = CreateHandler(),
            InitialReconnectBackoff = InitialReconnectBackoff,
            MaxReconnectBackoff = MaxReconnectBackoff,
            ServiceConfig = new ServiceConfig
            {
                LoadBalancingConfigs = { new RoundRobinConfig() },
                MethodConfigs =
                {
                    new MethodConfig
                    {
                        Names = { MethodName.Default },
                        RetryPolicy = new RetryPolicy
                        {
                            MaxAttempts = MaxRetryAttempts,
                            InitialBackoff = TimeSpan.FromMilliseconds(200),
                            MaxBackoff = TimeSpan.FromSeconds(3),
                            BackoffMultiplier = 2,
                            RetryableStatusCodes = { StatusCode.Unavailable },
                        },
                    },
                },
            },
        };

    /// <summary>按统一弹性配置创建一个面向 <paramref name="address"/> 的通道。</summary>
    public static GrpcChannel CreateChannel(
        string address,
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory)
        => GrpcChannel.ForAddress(address, CreateChannelOptions(serviceProvider, loggerFactory));
}
