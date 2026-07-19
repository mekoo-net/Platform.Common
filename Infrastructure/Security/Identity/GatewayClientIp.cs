using System.Net;
using Microsoft.AspNetCore.Http;
using Platform.Common.Discovery;

namespace Platform.Common.Identity;

/// <summary>
/// 下游服务读取 Gateway 确认后的客户端 IP（<see cref="GatewayHeaders.ClientIp"/>）。
/// </summary>
/// <remarks>
/// 经 Gateway 入站时只信 <c>X-Client-Ip</c>；勿再解析 <c>X-Forwarded-For</c> / CDN 头。
/// 本地直连或无 Gateway 时回落 <c>X-Real-IP</c>，再回落 <see cref="ConnectionInfo.RemoteIpAddress"/>。
/// </remarks>
public static class GatewayClientIp
{
    private const string LegacyRealIpHeader = "X-Real-IP";

    public static string? ResolveString(HttpContext http)
    {
        if (TryReadHeader(http, GatewayHeaders.ClientIp, out var gatewayIp))
        {
            return gatewayIp;
        }

        if (TryReadHeader(http, LegacyRealIpHeader, out var legacyIp))
        {
            return legacyIp;
        }

        return http.Connection.RemoteIpAddress?.ToString();
    }

    public static string ResolveStringOrDefault(HttpContext http, string fallback = "unknown")
        => ResolveString(http) ?? fallback;

    public static IPAddress? ResolveAddress(HttpContext http)
        => TryResolveAddress(http, out var address) ? address : null;

    public static bool TryResolveAddress(HttpContext http, out IPAddress address)
    {
        address = IPAddress.None;
        var raw = ResolveString(http);
        if (string.IsNullOrWhiteSpace(raw) || !IPAddress.TryParse(raw, out var parsed))
        {
            return false;
        }

        address = parsed;
        return true;
    }

    private static bool TryReadHeader(HttpContext http, string headerName, out string value)
    {
        value = string.Empty;
        if (!http.Request.Headers.TryGetValue(headerName, out var raw)
            || string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        value = raw.ToString();
        return true;
    }
}
