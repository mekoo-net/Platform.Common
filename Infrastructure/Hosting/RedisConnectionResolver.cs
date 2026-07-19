using System.Net;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace Platform.Common.Hosting;

/// <summary>
/// 解析 Redis 连接串：要求配置为 <c>redis://</c> 或 <c>rediss://</c> URI，再转为 StackExchange.Redis 格式连接。
/// </summary>
public static class RedisConnectionResolver
{
    public static string? Resolve(IConfiguration configuration)
    {
        var explicitConn = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(explicitConn))
            return ToStackExchangeFormat(explicitConn.Trim());

        return ToStackExchangeFormat(configuration.GetConnectionString("redis"));
    }

    public static string GetRequired(IConfiguration configuration) =>
        Resolve(configuration)
        ?? throw new InvalidOperationException(
            "Redis connection string is required. Configure ConnectionStrings:Redis as a redis:// URI, " +
            "e.g. Redis: \"redis://redis:6379\".");

    public static void ValidateConnectionString(string connectionString)
    {
        var opts = ConfigurationOptions.Parse(connectionString);
        if (opts.EndPoints.Count == 0
            || opts.EndPoints.All(static e =>
            {
                var text = e.ToString() ?? "";
                return string.IsNullOrWhiteSpace(text) || text.StartsWith(':');
            }))
        {
            throw new InvalidOperationException(
                $"Invalid Redis connection string (no host): '{RedactForLog(connectionString)}'. " +
                "Use a redis:// URI, e.g. Redis: \"redis://redis:6379\".");
        }
    }

    /// <summary>
    /// 将 <c>redis://</c> / <c>rediss://</c> URI 转为 StackExchange.Redis 连接串；非 URI 输入直接报错。
    /// </summary>
    internal static string? ToStackExchangeFormat(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        raw = raw.Trim();

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)
            || (!uri.Scheme.Equals("redis", StringComparison.OrdinalIgnoreCase)
                && !uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Redis connection string must be a redis:// or rediss:// URI, e.g. \"redis://redis:6379\". Got: '{RedactForLog(raw)}'.");
        }

        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 6379;
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException(
                $"Redis URI '{raw}' has no host. Use e.g. \"redis://redis:6379\".");
        }

        var password = uri.UserInfo;
        if (password.StartsWith(':'))
            password = password[1..];
        password = Uri.UnescapeDataString(password);

        var useTls = uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase);
        var parts = new List<string> { $"{host}:{port}" };
        if (!string.IsNullOrEmpty(password))
            parts.Add($"password={password}");
        parts.Add(useTls ? "ssl=true" : "ssl=false");
        parts.Add("abortConnect=false");
        return string.Join(',', parts);
    }

    public static string FormatEndPoints(IEnumerable<EndPoint> endPoints) =>
        string.Join(", ", endPoints.Select(FormatEndPoint));

    public static string FormatEndPoint(EndPoint endPoint)
    {
        if (endPoint is DnsEndPoint dns)
        {
            return $"{dns.Host}:{dns.Port}";
        }

        if (endPoint is IPEndPoint ip)
        {
            return ip.ToString();
        }

        var text = endPoint.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return "unknown";
        }

        var slash = text.LastIndexOf('/');
        if (slash >= 0 && slash < text.Length - 1)
        {
            return text[(slash + 1)..];
        }

        return text;
    }

    /// <summary>
    /// 连接 Redis。<paramref name="requireConnected"/> 为 true 时连不上直接抛异常（生产服务硬依赖）。
    /// </summary>
    public static IConnectionMultiplexer ConnectMultiplexer(string connectionString, bool requireConnected = false)
    {
        ValidateConnectionString(connectionString);
        var opts = ConfigurationOptions.Parse(connectionString);
        opts.AbortOnConnectFail = requireConnected;
        opts.ConnectRetry = requireConnected ? 3 : 5;
        opts.ConnectTimeout = 10_000;
        opts.SyncTimeout = 10_000;
        opts.AsyncTimeout = 10_000;
        var mux = ConnectionMultiplexer.Connect(opts);
        if (requireConnected)
        {
            mux.GetDatabase().Ping();
        }

        return mux;
    }

    public static IConnectionMultiplexer ConnectMultiplexer(IConfiguration configuration, bool requireConnected = false) =>
        ConnectMultiplexer(GetRequired(configuration), requireConnected);

    public static string DescribeEndpoints(string connectionString)
    {
        var opts = ConfigurationOptions.Parse(connectionString);
        return opts.EndPoints.Count == 0
            ? "(none)"
            : FormatEndPoints(opts.EndPoints);
    }

    internal static string RedactForLog(string connectionString)
    {
        var idx = connectionString.IndexOf("password=", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
            return connectionString[..idx] + "password=***";

        var at = connectionString.IndexOf('@');
        if (at > 0 && connectionString.Contains("://", StringComparison.Ordinal))
        {
            var schemeEnd = connectionString.IndexOf("://", StringComparison.Ordinal) + 3;
            return connectionString[..schemeEnd] + "***@" + connectionString[(at + 1)..];
        }

        return connectionString;
    }
}
