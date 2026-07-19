using Microsoft.Extensions.Configuration;

namespace Platform.Common.Hosting;

/// <summary>
/// 从 <c>ConnectionStrings:Database</c> 结构化节点组装 PostgreSQL 连接串（密码用单引号包裹，避免 <c>#</c> 被截断）。
/// </summary>
public static class NpgsqlConnectionResolver
{
    public const string ConnectionStringsDatabaseSection = "ConnectionStrings:Database";

    public static string Build(
        string host,
        int port,
        string database,
        string username,
        string password,
        string? searchPath = null)
    {
        var pwd = password.Replace("'", "''", StringComparison.Ordinal);
        var sp = string.IsNullOrWhiteSpace(searchPath) ? "" : $";Search Path={searchPath}";
        return $"Host={host};Port={port};Database={database};Username={username};Password='{pwd}'{sp}";
    }

    public static string? TryFromConfiguration(IConfiguration configuration)
    {
        var fromConnectionStrings = TryBuildFromSection(configuration.GetSection(ConnectionStringsDatabaseSection));
        if (fromConnectionStrings is not null)
            return fromConnectionStrings;

        return TryBuildFromSection(configuration.GetSection("Meeko:Database"));
    }

    private static string? TryBuildFromSection(IConfigurationSection section)
    {
        var host = section["Host"];
        if (string.IsNullOrWhiteSpace(host))
            return null;

        var sectionName = section.Path;
        return Build(
            host,
            section.GetValue("Port", 5432),
            section["Database"] ?? throw new InvalidOperationException($"{sectionName}:Database is required."),
            section["Username"] ?? "meeko",
            section["Password"] ?? throw new InvalidOperationException($"{sectionName}:Password is required."),
            section["SearchPath"]);
    }
}
