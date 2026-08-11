using System.Net;

namespace Platform.Common.Hosting;

/// <summary>把 <see cref="EndPoint"/> 打成 <c>host:port</c>，日志里直接可读。</summary>
public static class RedisEndPointFormatter
{
    public static string Format(EndPoint endPoint)
    {
        if (endPoint is DnsEndPoint dns)
            return $"{dns.Host}:{dns.Port}";

        if (endPoint is IPEndPoint ip)
            return ip.ToString();

        var text = endPoint.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return "unknown";

        // 非 DNS/IP 的实现（如 UnixDomainSocketEndPoint）ToString() 带前缀，取最后一段。
        var slash = text.LastIndexOf('/');
        return slash >= 0 && slash < text.Length - 1 ? text[(slash + 1)..] : text;
    }

    public static string Format(IEnumerable<EndPoint> endPoints) =>
        string.Join(", ", endPoints.Select(Format));
}
