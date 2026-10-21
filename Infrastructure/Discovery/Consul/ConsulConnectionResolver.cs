using Microsoft.Extensions.Configuration;
using Platform.Common.Hosting;

namespace Platform.Common.Discovery;

/// <summary>解析完成的 Consul agent 接入参数，直接喂给 <c>ConsulClientConfiguration</c>。</summary>
public sealed record ConsulSettings(Uri Address)
{
    public string? Datacenter { get; init; }
    public string? Token { get; init; }
}

/// <summary>
/// 读 <c>ConnectionStrings:Consul</c>。判定规则复用 <see cref="ConnectionSectionReader"/>
/// （标量整串 / 结构化映射 / 未配置），与 PG、Redis、RabbitMQ 同一套。
/// </summary>
public static class ConsulConnectionResolver
{
    private const int DefaultPort = 8500;
    private const int DefaultTlsPort = 8501;

    public static ConsulSettings? Resolve(IConfiguration configuration)
    {
        var options = ConnectionSectionReader.TryRead(
            configuration,
            ConsulConnectionOptions.SectionName,
            static url => new ConsulConnectionOptions { Url = url },
            static o => !string.IsNullOrWhiteSpace(o.Url) || !string.IsNullOrWhiteSpace(o.Host));

        return options is null ? null : Build(options);
    }

    public static ConsulSettings GetRequired(IConfiguration configuration) =>
        Resolve(configuration)
        ?? throw new InvalidOperationException(
            $"缺少 Consul 连接配置。请在本服务的 yaml 里配置 {ConsulConnectionOptions.SectionName}（Host / Port）。");

    internal static ConsulSettings Build(ConsulConnectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string? host = null;
        int? port = null;
        var useSsl = false;

        if (!string.IsNullOrWhiteSpace(options.Url))
            (host, port, useSsl) = ParseUri(options.Url.Trim());

        if (!string.IsNullOrWhiteSpace(options.Host))
            host = options.Host.Trim();
        if (options.Port is int explicitPort)
            port = explicitPort;
        if (options.Ssl is bool explicitSsl)
            useSsl = explicitSsl;

        if (string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException(
                $"Consul 连接配置缺少 {ConsulConnectionOptions.SectionName}:Host（或 Url）。");
        }

        // 端口缺省只在拆字段写法里生效；Url 写法按 URL 本身的语义走（http://consul 就是 80），
        // 否则反代在 443 后面的 agent 会被悄悄改成 8501。
        var effectivePort = port ?? (useSsl ? DefaultTlsPort : DefaultPort);
        if (effectivePort is < 1 or > ushort.MaxValue)
        {
            throw new InvalidOperationException(
                $"{ConsulConnectionOptions.SectionName}:Port 超出范围（1..{ushort.MaxValue}）：{effectivePort}。");
        }

        var address = new UriBuilder(useSsl ? Uri.UriSchemeHttps : Uri.UriSchemeHttp, host, effectivePort).Uri;
        return new ConsulSettings(address)
        {
            Datacenter = Trimmed(options.Datacenter),
            Token = Trimmed(options.Token),
        };
    }

    private static (string Host, int Port, bool UseSsl) ParseUri(string raw)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"{ConsulConnectionOptions.SectionName}:Url 必须是 http:// 或 https:// URI，"
                + $"如 \"http://consul:8500\"。实际收到：'{raw}'。");
        }

        // agent 的 HTTP API 不认 URL 里的账号密码；ACL 走 Token。静默丢掉会让人以为鉴权已生效。
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidOperationException(
                $"{ConsulConnectionOptions.SectionName}:Url 不支持内嵌账号密码，ACL 请填 Token。");
        }

        return (uri.Host, uri.Port, uri.Scheme == Uri.UriSchemeHttps);
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
