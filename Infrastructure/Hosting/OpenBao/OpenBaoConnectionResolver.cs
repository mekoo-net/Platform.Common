using Microsoft.Extensions.Configuration;

namespace Platform.Common.Hosting;

/// <summary>解析完成的 OpenBao 接入参数。凭据二选一：<see cref="RoleId"/> / <see cref="SecretId"/>，或 <see cref="AppRoleFile"/>。</summary>
public sealed record OpenBaoSettings(Uri Address)
{
    public const string DefaultAppRoleFile = "/run/secrets/openbao-approle.json";

    public string? RoleId { get; init; }
    public string? SecretId { get; init; }
    public string AppRoleFile { get; init; } = DefaultAppRoleFile;
    public string AppRoleMount { get; init; } = "approle";
    public TimeSpan RefreshInterval { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromMinutes(2);

    public bool HasInlineCredentials => RoleId is not null && SecretId is not null;
}

/// <summary>
/// 读顶层 <c>OpenBao</c> 节。判定规则复用 <see cref="ConnectionSectionReader"/>（标量整串 / 结构化映射 / 未配置），
/// 地址写法与 Consul、PG、Redis 同一套。
/// </summary>
public static class OpenBaoConnectionResolver
{
    private const int DefaultPort = 8200;

    /// <summary>没配（没有 Host / Url）返回 null：只读配置文件。配了但写错直接抛。</summary>
    public static OpenBaoSettings? Resolve(IConfiguration configuration)
    {
        var options = ConnectionSectionReader.TryRead(
            configuration,
            OpenBaoOptions.SectionName,
            static url => new OpenBaoOptions { Url = url },
            static o => !string.IsNullOrWhiteSpace(o.Url) || !string.IsNullOrWhiteSpace(o.Host));

        return options is null ? null : Build(options);
    }

    internal static OpenBaoSettings Build(OpenBaoOptions options)
    {
        const string section = OpenBaoOptions.SectionName;
        string? host = null;
        int? port = null;
        var useSsl = false;

        if (!string.IsNullOrWhiteSpace(options.Url))
        {
            if (!Uri.TryCreate(options.Url.Trim(), UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || !string.IsNullOrEmpty(uri.UserInfo))
                throw new InvalidOperationException(
                    $"{section}:Url 必须是不带账号密码的 http:// 或 https:// URI，如 \"http://openbao:8200\"。实际收到：'{options.Url}'。");
            (host, port, useSsl) = (uri.Host, uri.Port, uri.Scheme == Uri.UriSchemeHttps);
        }

        if (!string.IsNullOrWhiteSpace(options.Host))
            host = options.Host.Trim();
        if (options.Port is int explicitPort)
            port = explicitPort;
        if (options.Ssl is bool explicitSsl)
            useSsl = explicitSsl;

        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException($"{section} 缺少 Host（或 Url）。");
        var effectivePort = port ?? DefaultPort;
        if (effectivePort is < 1 or > ushort.MaxValue)
            throw new InvalidOperationException($"{section}:Port 超出范围（1..{ushort.MaxValue}）：{effectivePort}。");

        var roleId = Trimmed(options.RoleId);
        var secretId = Trimmed(options.SecretId);
        if ((roleId is null) != (secretId is null))
            throw new InvalidOperationException($"{section}:RoleId 和 SecretId 要么都写，要么都不写（不写就用 AppRoleFile）。");
        if (roleId is not null && Trimmed(options.AppRoleFile) is not null)
            throw new InvalidOperationException($"{section}：RoleId / SecretId 和 AppRoleFile 只写一种。");

        var settings = new OpenBaoSettings(new UriBuilder(useSsl ? Uri.UriSchemeHttps : Uri.UriSchemeHttp, host, effectivePort).Uri)
        {
            RoleId = roleId,
            SecretId = secretId,
        };
        return settings with
        {
            AppRoleFile = Trimmed(options.AppRoleFile) ?? settings.AppRoleFile,
            AppRoleMount = Trimmed(options.AppRoleMount) ?? settings.AppRoleMount,
            RefreshInterval = options.RefreshInterval ?? settings.RefreshInterval,
            StartupTimeout = options.StartupTimeout ?? settings.StartupTimeout,
        };
    }

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
