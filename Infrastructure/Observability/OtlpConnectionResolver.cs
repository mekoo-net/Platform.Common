using Microsoft.Extensions.Configuration;
using Platform.Common.Hosting;

namespace Platform.Common.Observability;

/// <summary>
/// 读 <c>Connection:Otlp</c>。判定规则复用 <see cref="ConnectionSectionReader"/>
/// （标量整串 / 结构化映射 / 未配置），与 PG、Redis、Consul 同一套。
/// </summary>
public static class OtlpConnectionResolver
{
    // collector 开 TLS 时监听的通常还是 4317，所以不像 Consul 那样随 Ssl 切默认端口。
    private const int DefaultPort = 4317;

    /// <summary>OTLP/gRPC 端点。未配置返回 <c>null</c>：遥测是可选的，没配就是不导出。</summary>
    public static Uri? Resolve(IConfiguration configuration)
    {
        var options = ConnectionSectionReader.TryRead(
            configuration,
            OtlpConnectionOptions.SectionName,
            static url => new OtlpConnectionOptions { Url = url },
            static o => !string.IsNullOrWhiteSpace(o.Url) || !string.IsNullOrWhiteSpace(o.Host));

        return options is null ? null : Build(options);
    }

    internal static Uri Build(OtlpConnectionOptions options)
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
                $"OTLP 连接配置缺少 {OtlpConnectionOptions.SectionName}:Host（或 Url）。");
        }

        // 端口缺省只在拆字段写法里生效；Url 写法按 URL 本身的语义走（http://collector 就是 80）。
        var effectivePort = port ?? DefaultPort;
        if (effectivePort is < 1 or > ushort.MaxValue)
        {
            throw new InvalidOperationException(
                $"{OtlpConnectionOptions.SectionName}:Port 超出范围（1..{ushort.MaxValue}）：{effectivePort}。");
        }

        return new UriBuilder(useSsl ? Uri.UriSchemeHttps : Uri.UriSchemeHttp, host, effectivePort).Uri;
    }

    private static (string Host, int Port, bool UseSsl) ParseUri(string raw)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"{OtlpConnectionOptions.SectionName}:Url 必须是 http:// 或 https:// URI，"
                + $"如 \"http://collector:4317\"。实际收到：'{raw}'。");
        }

        // collector 的鉴权走请求头，URL 里的账号密码导出器不会带上。静默丢掉会让人以为鉴权已生效。
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidOperationException(
                $"{OtlpConnectionOptions.SectionName}:Url 不支持内嵌账号密码。");
        }

        // 带路径的（…:4318/v1/traces）是 OTLP/HTTP 的地址；gRPC 导出器拿去连只会一直失败，且不报到日志里。
        if (uri.AbsolutePath != "/")
        {
            throw new InvalidOperationException(
                $"{OtlpConnectionOptions.SectionName}:Url 只认 OTLP/gRPC 端点，不带路径（如 \"http://collector:4317\"）。"
                + $"实际收到：'{raw}'。");
        }

        return (uri.Host, uri.Port, uri.Scheme == Uri.UriSchemeHttps);
    }
}
