using Microsoft.Extensions.Configuration;

namespace Platform.Common.Hosting;

public static class ConfigurationConnectionExtensions
{
    /// <summary>
    /// PostgreSQL 连接串：优先 <see cref="NpgsqlConnectionResolver.ConnectionStringsDatabaseSection"/> 结构化节点；
    /// 否则读取 <c>ConnectionStrings:Database</c> 字符串。
    /// </summary>
    public static string? GetDbConnectionString(this IConfiguration configuration)
    {
        var fromSection = NpgsqlConnectionResolver.TryFromConfiguration(configuration);
        if (!string.IsNullOrWhiteSpace(fromSection))
            return fromSection;

        return configuration.GetConnectionString("Database");
    }

    public static string GetRequiredDbConnectionString(this IConfiguration configuration) =>
        configuration.GetDbConnectionString()
        ?? throw new InvalidOperationException(
            "PostgreSQL connection string is required. Configure ConnectionStrings:Database in the service config file.");

    /// <inheritdoc cref="RedisConnectionResolver.Resolve"/>
    public static string? GetRedisConnectionString(this IConfiguration configuration) =>
        RedisConnectionResolver.Resolve(configuration);

    public static string GetRequiredRedisConnectionString(this IConfiguration configuration) =>
        RedisConnectionResolver.GetRequired(configuration);
}
