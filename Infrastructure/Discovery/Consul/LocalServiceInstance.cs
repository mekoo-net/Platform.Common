using System.Net;
using System.Net.Sockets;
using Consul;
using Microsoft.Extensions.Configuration;
using Platform.Common.HealthChecks;

namespace Platform.Common.Discovery;

/// <summary>
/// 本进程向 Consul 登记的身份与地址。Key 由宿主在代码里写死（<see cref="ServiceKeys"/>），
/// 地址不读配置：主机取本机连 Consul 时用的源 IP，端口取 Kestrel 的 <c>Http</c> / <c>InternalRpc</c> 端点。
/// </summary>
/// <remarks>
/// 以前 yaml 里另写一份 <c>Discovery:Service:Address: http://billing:7030/</c>，跟 Kestrel 端口是同一个事实
/// 抄了两遍，改端口只改一处就会注册出一个打不通的地址。
/// <para>主机以前取容器 hostname，前提是同网段可解析。host 网络下 hostname 是宿主机名（Yomi 网关那台就叫
/// <c>localhost</c>），Consul 在另一台机器上做健康检查打的是它自己，1 分钟后把服务摘掉。源 IP 不挑网络模式：
/// 健康检查是 Consul 从它那边拨回来，和 Consul 同一张网的那块网卡正是它能拨到的；Docker 自定义网络里
/// 得到的是容器 IP，每个副本不同；换到 K8s 就是 Pod IP。OneApi.Gateway 的 <c>AdvertiseAddress</c> 是同一个做法。</para>
/// </remarks>
public sealed record LocalServiceInstance(string Key, string Id, string Host, Uri RestBaseUrl, Uri? GrpcBaseUrl)
{
    /// <summary>REST 端点名（HTTP/1）。网关反代与健康检查都打它。</summary>
    public const string HttpEndpoint = "Http";

    /// <summary>MagicOnion 端点名（HTTP/2）。没配就不发布 gRPC 地址——说明这个服务不接 RPC。</summary>
    public const string InternalRpcEndpoint = "InternalRpc";

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan DeregisterAfter = TimeSpan.FromMinutes(1);

    /// <param name="key">本服务的 <see cref="ServiceKeys"/>。</param>
    /// <param name="configuration">读 <c>Kestrel:Endpoints:*:Url</c> 与 <c>ConnectionStrings:Consul</c>。</param>
    /// <param name="hostName">测试注入用；缺省取本机连 Consul 时的源 IP。</param>
    public static LocalServiceInstance Resolve(string key, IConfiguration configuration, string? hostName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(configuration);

        var host = string.IsNullOrWhiteSpace(hostName) ? RouteToConsul(configuration) : hostName.Trim();

        var http = ReadEndpoint(configuration, HttpEndpoint)
            ?? throw new InvalidOperationException(
                $"服务 '{key}' 要向 Consul 注册，但没配 Kestrel:Endpoints:{HttpEndpoint}:Url，推不出对外地址。");
        var grpc = ReadEndpoint(configuration, InternalRpcEndpoint);

        return new LocalServiceInstance(
            key,
            // 同一地址下重启的进程沿用同一个 ID，Consul 里是覆盖而不是多出一条等着过期的死实例。
            $"{key}@{host}-{Environment.ProcessId}",
            host,
            Advertise(http, host),
            grpc is null ? null : Advertise(grpc, host));
    }

    /// <summary>Consul 侧的 HTTP 存活检查，打 <see cref="HealthChecksExtensions.LivePath"/>。</summary>
    public AgentServiceCheck CreateHealthCheck() => new()
    {
        HTTP = new Uri(RestBaseUrl, HealthChecksExtensions.LivePath).ToString(),
        Interval = CheckInterval,
        Timeout = CheckTimeout,
        DeregisterCriticalServiceAfter = DeregisterAfter,
    };

    /// <summary>
    /// 本机通往 Consul 的那块网卡的 IPv4。UDP 的 Connect 只让内核选路由、填本端地址，不发包，
    /// Consul 此刻不在线也取得到。不取「第一块非回环网卡」：容器挂多张网、宿主机有 docker0 / wg0 时，
    /// 枚举顺序和 Consul 能拨到哪张没有关系。
    /// <para>Consul 主机名解析不了（compose 里 consul 容器还没起来）时退回 hostname，不让注册这件尽力而为的事
    /// 把服务启动拦下；退回的地址在 Docker 自定义网络里照样可解析，跟改之前一样。</para>
    /// </summary>
    private static string RouteToConsul(IConfiguration configuration)
    {
        if (ConsulConnectionResolver.Resolve(configuration)?.Address is not { } consul)
            return Dns.GetHostName();

        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(consul.Host, consul.Port);
            return ((IPEndPoint)socket.LocalEndPoint!).Address.ToString();
        }
        catch (SocketException)
        {
            return Dns.GetHostName();
        }
    }

    private static Uri? ReadEndpoint(IConfiguration configuration, string name)
    {
        var raw = configuration[$"Kestrel:Endpoints:{name}:Url"];
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        // Kestrel 允许 http://*:7030 / http://+:7030 这类通配主机，Uri 不认；主机反正要换掉，先垫一个合法值。
        var normalized = raw.Trim()
            .Replace("://*:", "://0.0.0.0:", StringComparison.Ordinal)
            .Replace("://+:", "://0.0.0.0:", StringComparison.Ordinal);

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"Kestrel:Endpoints:{name}:Url 不是合法的 http(s) 地址：'{raw}'。");
        }

        return uri;
    }

    private static Uri Advertise(Uri bound, string host) =>
        new UriBuilder(bound.Scheme, host, bound.Port, "/").Uri;
}
