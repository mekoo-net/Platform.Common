using System.Net;
using Consul;
using Microsoft.Extensions.Configuration;
using Platform.Common.HealthChecks;

namespace Platform.Common.Discovery;

/// <summary>
/// 本进程向 Consul 登记的身份与地址。Key 由宿主在代码里写死（<see cref="ServiceKeys"/>），
/// 地址不读配置：主机取容器 hostname，端口取 Kestrel 的 <c>Http</c> / <c>InternalRpc</c> 端点。
/// </summary>
/// <remarks>
/// 以前 yaml 里另写一份 <c>Discovery:Service:Address: http://billing:7030/</c>，跟 Kestrel 端口是同一个事实
/// 抄了两遍，改端口只改一处就会注册出一个打不通的地址。
/// <para>hostname 的前提是同网段可解析。compose 里<b>不要设 <c>hostname:</c></b>：不设时 Docker 用容器短 ID，
/// 在自定义网络里可解析，且每个副本不同；写死成 Key 的话多副本 ID 和地址都相同，后注册的覆盖先注册的。
/// 换到 K8s 时 Pod 名不可解析，应改为注册 Pod IP。</para>
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
    /// <param name="configuration">读 <c>Kestrel:Endpoints:*:Url</c>。</param>
    /// <param name="hostName">测试注入用；缺省取 <see cref="Dns.GetHostName"/>。</param>
    public static LocalServiceInstance Resolve(string key, IConfiguration configuration, string? hostName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(configuration);

        var host = string.IsNullOrWhiteSpace(hostName) ? Dns.GetHostName() : hostName.Trim();

        var http = ReadEndpoint(configuration, HttpEndpoint)
            ?? throw new InvalidOperationException(
                $"服务 '{key}' 要向 Consul 注册，但没配 Kestrel:Endpoints:{HttpEndpoint}:Url，推不出对外地址。");
        var grpc = ReadEndpoint(configuration, InternalRpcEndpoint);

        return new LocalServiceInstance(
            key,
            // 同一 hostname 下重启的进程沿用同一个 ID，Consul 里是覆盖而不是多出一条等着过期的死实例。
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
