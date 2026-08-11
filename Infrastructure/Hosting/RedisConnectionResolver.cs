using System.Net;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace Platform.Common.Hosting;

/// <summary>
/// Redis 连接的建立与诊断。解析规则在 <see cref="RedisConnectionBuilder"/>，这里只负责连上去。
/// </summary>
public static class RedisConnectionResolver
{
    /// <inheritdoc cref="ConfigurationConnectionExtensions.GetRedisConnectionString"/>
    public static string? Resolve(IConfiguration configuration) =>
        configuration.GetRedisConnectionString();

    /// <inheritdoc cref="ConfigurationConnectionExtensions.GetRequiredRedisConnectionString"/>
    public static string GetRequired(IConfiguration configuration) =>
        configuration.GetRequiredRedisConnectionString();

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
                $"Redis 连接串没有主机：'{RedactForLog(connectionString)}'。"
                + $" 请配置 {RedisConnectionOptions.SectionName}:Host。");
        }
    }

    public static string FormatEndPoints(IEnumerable<EndPoint> endPoints) =>
        RedisEndPointFormatter.Format(endPoints);

    public static string FormatEndPoint(EndPoint endPoint) =>
        RedisEndPointFormatter.Format(endPoint);

    /// <summary>
    /// 连接 Redis。<paramref name="requireConnected"/> 为 true 时连不上直接抛异常（生产服务硬依赖）。
    /// </summary>
    public static IConnectionMultiplexer ConnectMultiplexer(string connectionString, bool requireConnected = false)
    {
        ValidateConnectionString(connectionString);
        var opts = RedisConnectionBuilder.ApplyPlatformDefaults(ConfigurationOptions.Parse(connectionString));
        return ConnectMultiplexer(opts, requireConnected);
    }

    /// <inheritdoc cref="ConnectMultiplexer(string, bool)"/>
    public static IConnectionMultiplexer ConnectMultiplexer(ConfigurationOptions options, bool requireConnected = false)
    {
        options.AbortOnConnectFail = requireConnected;
        var mux = ConnectionMultiplexer.Connect(options);
        if (requireConnected)
            mux.GetDatabase().Ping();

        return mux;
    }

    public static IConnectionMultiplexer ConnectMultiplexer(IConfiguration configuration, bool requireConnected = false) =>
        ConnectMultiplexer(
            configuration.GetRedisConfigurationOptions()
            ?? throw new InvalidOperationException(
                $"缺少 Redis 连接配置。请在本服务的 yaml 里配置 {RedisConnectionOptions.SectionName}。"),
            requireConnected);

    public static string DescribeEndpoints(string connectionString) =>
        ConfigurationOptions.Parse(connectionString) is { EndPoints.Count: > 0 } opts
            ? FormatEndPoints(opts.EndPoints)
            : "(none)";

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
