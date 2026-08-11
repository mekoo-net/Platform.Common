using Microsoft.Extensions.Configuration;
using Platform.Common.Observability;
using StackExchange.Redis;

namespace Platform.Common.Hosting;

/// <summary>
/// 服务读连接配置的唯一入口。写法规范见 <see cref="ConnectionSectionReader"/>。
/// </summary>
public static class ConfigurationConnectionExtensions
{
    /// <summary>结构化的 PostgreSQL 配置；未配置时返回 <c>null</c>。</summary>
    public static PostgresConnectionOptions? GetPostgresOptions(this IConfiguration configuration) =>
        ConnectionSectionReader.TryReadPostgres(configuration);

    /// <summary>结构化的 Redis 配置；未配置时返回 <c>null</c>。</summary>
    public static RedisConnectionOptions? GetRedisOptions(this IConfiguration configuration) =>
        ConnectionSectionReader.TryReadRedis(configuration);

    /// <summary>
    /// PostgreSQL 连接串。未配置返回 <c>null</c>；配了但不完整（如缺 Password）直接抛错，
    /// 不静默降级成"没配"——那会让健康检查悄悄少一项。
    /// </summary>
    public static string? GetDbConnectionString(this IConfiguration configuration)
    {
        var options = configuration.GetPostgresOptions();
        return options is null
            ? null
            : PostgresConnectionBuilder.Build(options, configuration.ResolveServiceName());
    }

    public static string GetRequiredDbConnectionString(this IConfiguration configuration) =>
        configuration.GetDbConnectionString()
        ?? throw new InvalidOperationException(
            $"缺少 PostgreSQL 连接配置。请在本服务的 yaml 里配置 {PostgresConnectionOptions.SectionName}"
            + "（Host / Port / Database / Username / Password）。");

    /// <inheritdoc cref="GetDbConnectionString"/>
    public static ConfigurationOptions? GetRedisConfigurationOptions(this IConfiguration configuration)
    {
        var options = configuration.GetRedisOptions();
        return options is null
            ? null
            : RedisConnectionBuilder.Build(options, configuration.ResolveServiceName());
    }

    public static string? GetRedisConnectionString(this IConfiguration configuration) =>
        configuration.GetRedisConfigurationOptions()?.ToString();

    public static string GetRequiredRedisConnectionString(this IConfiguration configuration) =>
        configuration.GetRedisConnectionString()
        ?? throw new InvalidOperationException(
            $"缺少 Redis 连接配置。请在本服务的 yaml 里配置 {RedisConnectionOptions.SectionName}"
            + "（Host / Port，可选 Password / Database）。");

    /// <summary>连接标识（PG 的 application_name、Redis 的 client name）统一取遥测服务名。</summary>
    private static string? ResolveServiceName(this IConfiguration configuration) =>
        configuration[$"{ObservabilityOptions.SectionName}:{nameof(ObservabilityOptions.ServiceName)}"];
}
