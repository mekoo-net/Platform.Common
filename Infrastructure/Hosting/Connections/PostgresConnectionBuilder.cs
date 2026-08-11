using Npgsql;

namespace Platform.Common.Hosting;

/// <summary>
/// 把 <see cref="PostgresConnectionOptions"/> 组装成 Npgsql 连接串。
/// </summary>
/// <remarks>
/// 合并顺序：<c>Url</c> 整串底座 → 各显式字段 → <c>Parameters</c> 透传项，后者覆盖前者。
/// 转义交给 <see cref="NpgsqlConnectionStringBuilder"/>：密码里的 <c>;</c> <c>'</c> <c>=</c>
/// 由它按 ADO 规则处理，手写字符串拼接迟早会在某个密码上崩掉。
/// </remarks>
public static class PostgresConnectionBuilder
{
    /// <param name="defaultApplicationName">
    /// <c>ApplicationName</c> 未配置时的兜底值，一般传服务名——pg_stat_activity 里能认出是谁占着连接。
    /// </param>
    public static string Build(PostgresConnectionOptions options, string? defaultApplicationName = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var builder = string.IsNullOrWhiteSpace(options.Url)
            ? new NpgsqlConnectionStringBuilder()
            : ParseBase(options.Url.Trim());

        // Url 里可能已经带了密码；显式字段没给时不算"未配置"。
        var passwordProvided = options.Password is not null || builder.Password is not null;

        if (!string.IsNullOrWhiteSpace(options.Host))
            builder.Host = options.Host.Trim();
        if (options.Port is int port)
            builder.Port = port;
        if (!string.IsNullOrWhiteSpace(options.Database))
            builder.Database = options.Database.Trim();
        if (!string.IsNullOrWhiteSpace(options.Username))
            builder.Username = options.Username.Trim();
        if (options.Password is not null)
            builder.Password = options.Password;
        if (!string.IsNullOrWhiteSpace(options.SearchPath))
            builder.SearchPath = options.SearchPath.Trim();

        var applicationName = options.ApplicationName ?? defaultApplicationName;
        if (!string.IsNullOrWhiteSpace(applicationName))
            builder.ApplicationName = applicationName;

        if (options.SslMode is { } sslMode)
            builder.SslMode = sslMode;
        if (!string.IsNullOrWhiteSpace(options.RootCertificate))
            builder.RootCertificate = options.RootCertificate.Trim();
        if (!string.IsNullOrWhiteSpace(options.SslCertificate))
            builder.SslCertificate = options.SslCertificate.Trim();
        if (!string.IsNullOrWhiteSpace(options.SslKey))
            builder.SslKey = options.SslKey.Trim();
        if (options.SslPassword is not null)
            builder.SslPassword = options.SslPassword;
        if (options.CheckCertificateRevocation is bool checkCertificateRevocation)
            builder.CheckCertificateRevocation = checkCertificateRevocation;
        if (options.SslNegotiation is { } sslNegotiation)
            builder.SslNegotiation = sslNegotiation;
        if (options.ChannelBinding is { } channelBinding)
            builder.ChannelBinding = channelBinding;
        if (!string.IsNullOrWhiteSpace(options.RequireAuth))
            builder.RequireAuth = options.RequireAuth.Trim();
        if (!string.IsNullOrWhiteSpace(options.Passfile))
            builder.Passfile = options.Passfile.Trim();

        if (options.Timeout is int timeout)
            builder.Timeout = timeout;
        if (options.CommandTimeout is int commandTimeout)
            builder.CommandTimeout = commandTimeout;
        if (options.CancellationTimeout is int cancellationTimeout)
            builder.CancellationTimeout = cancellationTimeout;
        if (options.KeepAlive is int keepAlive)
            builder.KeepAlive = keepAlive;
        if (options.TcpKeepAlive is bool tcpKeepAlive)
            builder.TcpKeepAlive = tcpKeepAlive;
        if (options.TcpKeepAliveTime is int tcpKeepAliveTime)
            builder.TcpKeepAliveTime = tcpKeepAliveTime;
        if (options.TcpKeepAliveInterval is int tcpKeepAliveInterval)
            builder.TcpKeepAliveInterval = tcpKeepAliveInterval;

        if (options.Pooling is bool pooling)
            builder.Pooling = pooling;
        if (options.MinPoolSize is int minPoolSize)
            builder.MinPoolSize = minPoolSize;
        if (options.MaxPoolSize is int maxPoolSize)
            builder.MaxPoolSize = maxPoolSize;
        if (options.ConnectionIdleLifetime is int connectionIdleLifetime)
            builder.ConnectionIdleLifetime = connectionIdleLifetime;
        if (options.ConnectionPruningInterval is int connectionPruningInterval)
            builder.ConnectionPruningInterval = connectionPruningInterval;
        if (options.ConnectionLifetime is int connectionLifetime)
            builder.ConnectionLifetime = connectionLifetime;

        if (options.Multiplexing is bool multiplexing)
            builder.Multiplexing = multiplexing;
        if (options.NoResetOnClose is bool noResetOnClose)
            builder.NoResetOnClose = noResetOnClose;
        if (options.IncludeErrorDetail is bool includeErrorDetail)
            builder.IncludeErrorDetail = includeErrorDetail;
        if (options.MaxAutoPrepare is int maxAutoPrepare)
            builder.MaxAutoPrepare = maxAutoPrepare;
        if (options.AutoPrepareMinUsages is int autoPrepareMinUsages)
            builder.AutoPrepareMinUsages = autoPrepareMinUsages;
        if (!string.IsNullOrWhiteSpace(options.TargetSessionAttributes))
            builder.TargetSessionAttributes = options.TargetSessionAttributes.Trim();
        if (options.LoadBalanceHosts is bool loadBalanceHosts)
            builder.LoadBalanceHosts = loadBalanceHosts;
        if (options.HostRecheckSeconds is int hostRecheckSeconds)
            builder.HostRecheckSeconds = hostRecheckSeconds;
        if (options.ReadBufferSize is int readBufferSize)
            builder.ReadBufferSize = readBufferSize;
        if (options.WriteBufferSize is int writeBufferSize)
            builder.WriteBufferSize = writeBufferSize;
        if (!string.IsNullOrWhiteSpace(options.Options))
            builder.Options = options.Options.Trim();
        if (!string.IsNullOrWhiteSpace(options.Timezone))
            builder.Timezone = options.Timezone.Trim();

        ApplyParameters(builder, options.Parameters);
        Validate(builder, passwordProvided || options.Parameters.ContainsKey("Password"));

        return builder.ConnectionString;
    }

    private static NpgsqlConnectionStringBuilder ParseBase(string url)
    {
        if (url.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{PostgresConnectionOptions.SectionName}:Url 不支持 postgresql:// URI，只接受 Npgsql 关键字格式 " +
                "（Host=…;Port=…;Database=…）。拆成 Host / Port / Database / Username / Password 字段是首选写法。");
        }

        try
        {
            return new NpgsqlConnectionStringBuilder(url);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            throw new InvalidOperationException(
                $"{PostgresConnectionOptions.SectionName}:Url 不是合法的 Npgsql 连接串：{ex.Message}", ex);
        }
    }

    private static void ApplyParameters(
        NpgsqlConnectionStringBuilder builder,
        Dictionary<string, string?> parameters)
    {
        foreach (var (key, value) in parameters)
        {
            try
            {
                builder[key] = value;
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException)
            {
                throw new InvalidOperationException(
                    $"{PostgresConnectionOptions.SectionName}:Parameters 里的 '{key}' 不是 Npgsql 认识的关键字：{ex.Message}", ex);
            }
        }
    }

    private static void Validate(NpgsqlConnectionStringBuilder builder, bool passwordProvided)
    {
        List<string>? missing = null;
        void Require(bool ok, string field)
        {
            if (!ok)
                (missing ??= []).Add($"{PostgresConnectionOptions.SectionName}:{field}");
        }

        Require(!string.IsNullOrWhiteSpace(builder.Host), "Host");
        Require(!string.IsNullOrWhiteSpace(builder.Database), "Database");
        Require(!string.IsNullOrWhiteSpace(builder.Username), "Username");
        // 空串是"显式声明无密码"（trust / peer 认证），只有 null 才算漏配。
        Require(passwordProvided, "Password");

        if (missing is not null)
        {
            throw new InvalidOperationException(
                $"PostgreSQL 连接配置缺少：{string.Join("、", missing)}。"
                + " 无密码认证请显式写 Password: \"\"，不要留空。");
        }
    }
}
