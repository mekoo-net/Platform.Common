using StackExchange.Redis;

namespace Platform.Common.Hosting;

/// <summary>
/// 把 <see cref="RedisConnectionOptions"/> 组装成 StackExchange.Redis 的 <see cref="ConfigurationOptions"/>。
/// </summary>
/// <remarks>
/// 合并顺序同 PostgreSQL：<c>Url</c> 底座 → 各显式字段 → <c>Parameters</c> 透传项。
/// <c>Url</c> 同时接受 <c>redis://</c> / <c>rediss://</c> URI 和 StackExchange 原生串
/// （<c>host:port,password=…</c>）——历史 yaml 两种都有，在这里收敛，不要再散落到各服务。
/// </remarks>
public static class RedisConnectionBuilder
{
    /// <summary>连接 / 命令超时的平台默认值（毫秒）。容器内网络抖动时 5s 太紧。</summary>
    private const int DefaultTimeoutMilliseconds = 10_000;

    private const int DefaultConnectRetry = 5;

    /// <param name="defaultClientName">
    /// <c>ClientName</c> 未配置时的兜底值，一般传服务名——CLIENT LIST 里能认出是谁。
    /// </param>
    public static ConfigurationOptions Build(RedisConnectionOptions options, string? defaultClientName = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var opts = string.IsNullOrWhiteSpace(options.Url)
            ? new ConfigurationOptions()
            : ParseBase(options.Url.Trim());

        ApplyPlatformDefaults(opts);

        if (options.Endpoints.Count > 0)
        {
            opts.EndPoints.Clear();
            foreach (var endpoint in options.Endpoints.Where(e => !string.IsNullOrWhiteSpace(e)))
                opts.EndPoints.Add(endpoint.Trim());
        }
        else if (!string.IsNullOrWhiteSpace(options.Host))
        {
            opts.EndPoints.Clear();
            opts.EndPoints.Add(options.Host.Trim(), options.Port ?? 6379);
        }
        else if (options.Port is int port && opts.EndPoints.Count == 1)
        {
            // 只覆写端口：Url 给了 host，yaml 又单独改了 port。
            var host = RedisEndPointFormatter.Format(opts.EndPoints[0]).Split(':')[0];
            opts.EndPoints.Clear();
            opts.EndPoints.Add(host, port);
        }

        if (options.User is not null)
            opts.User = options.User;
        if (options.Password is not null)
            opts.Password = options.Password;
        if (options.Database is int database)
            opts.DefaultDatabase = database;

        if (options.Ssl is bool ssl)
            opts.Ssl = ssl;
        if (!string.IsNullOrWhiteSpace(options.SslHost))
            opts.SslHost = options.SslHost.Trim();
        if (options.SslProtocols is { } sslProtocols)
            opts.SslProtocols = sslProtocols;
        if (options.CheckCertificateRevocation is bool checkCertificateRevocation)
            opts.CheckCertificateRevocation = checkCertificateRevocation;

        var clientName = options.ClientName ?? defaultClientName;
        if (!string.IsNullOrWhiteSpace(clientName))
            opts.ClientName = clientName;

        if (options.ConnectTimeout is int connectTimeout)
            opts.ConnectTimeout = connectTimeout;
        if (options.SyncTimeout is int syncTimeout)
            opts.SyncTimeout = syncTimeout;
        if (options.AsyncTimeout is int asyncTimeout)
            opts.AsyncTimeout = asyncTimeout;
        if (options.ConnectRetry is int connectRetry)
            opts.ConnectRetry = connectRetry;
        if (options.KeepAlive is int keepAlive)
            opts.KeepAlive = keepAlive;
        if (options.TcpKeepAlive is bool tcpKeepAlive)
            opts.TcpKeepAlive = tcpKeepAlive;
        if (!string.IsNullOrWhiteSpace(options.ChannelPrefix))
            opts.ChannelPrefix = RedisChannel.Literal(options.ChannelPrefix.Trim());
        if (options.AbortOnConnectFail is bool abortOnConnectFail)
            opts.AbortOnConnectFail = abortOnConnectFail;
        if (options.AllowAdmin is bool allowAdmin)
            opts.AllowAdmin = allowAdmin;
        if (!string.IsNullOrWhiteSpace(options.ServiceName))
            opts.ServiceName = options.ServiceName.Trim();

        if (options.Protocol is { } protocol)
            opts.Protocol = protocol;
        if (options.Proxy is { } proxy)
            opts.Proxy = proxy;
        if (!string.IsNullOrWhiteSpace(options.TieBreaker))
            opts.TieBreaker = options.TieBreaker.Trim();
        if (!string.IsNullOrWhiteSpace(options.ConfigurationChannel))
            opts.ConfigurationChannel = options.ConfigurationChannel.Trim();
        if (options.ConfigCheckSeconds is int configCheckSeconds)
            opts.ConfigCheckSeconds = configCheckSeconds;
        if (options.ResolveDns is bool resolveDns)
            opts.ResolveDns = resolveDns;
        if (options.DefaultVersion is { } defaultVersion)
            opts.DefaultVersion = defaultVersion;
        if (options.HighIntegrity is bool highIntegrity)
            opts.HighIntegrity = highIntegrity;
        if (options.SetClientLibrary is bool setClientLibrary)
            opts.SetClientLibrary = setClientLibrary;

        opts = ApplyParameters(opts, options.Parameters);
        Validate(opts);

        return opts;
    }

    /// <inheritdoc cref="Build"/>
    public static string BuildConnectionString(RedisConnectionOptions options, string? defaultClientName = null) =>
        Build(options, defaultClientName).ToString();

    /// <summary>
    /// 平台默认值。SE.Redis 自带的 5s 超时在容器网络里偏紧，统一放宽到 10s。
    /// 直接拿现成连接串（没走 <see cref="RedisConnectionOptions"/>）的调用方也要经过这里，
    /// 否则同一个集群会因为入口不同而有两套超时。
    /// </summary>
    public static ConfigurationOptions ApplyPlatformDefaults(ConfigurationOptions opts)
    {
        opts.AbortOnConnectFail = false;
        opts.ConnectTimeout = DefaultTimeoutMilliseconds;
        opts.SyncTimeout = DefaultTimeoutMilliseconds;
        opts.AsyncTimeout = DefaultTimeoutMilliseconds;
        opts.ConnectRetry = DefaultConnectRetry;
        return opts;
    }

    private static ConfigurationOptions ParseBase(string url)
    {
        if (!url.StartsWith("redis://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("rediss://", StringComparison.OrdinalIgnoreCase))
        {
            return Parse(url);
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new InvalidOperationException(
                $"{RedisConnectionOptions.SectionName}:Url '{Redact(url)}' 不是合法的 redis:// URI。");
        }

        var opts = new ConfigurationOptions
        {
            Ssl = uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase),
        };
        opts.EndPoints.Add(uri.Host, uri.Port > 0 ? uri.Port : 6379);

        // userInfo 形如 user:password / :password / password（后者是历史写法，整段当密码）。
        var userInfo = uri.UserInfo;
        if (!string.IsNullOrEmpty(userInfo))
        {
            var separator = userInfo.IndexOf(':');
            if (separator < 0)
            {
                opts.Password = Uri.UnescapeDataString(userInfo);
            }
            else
            {
                var user = Uri.UnescapeDataString(userInfo[..separator]);
                if (!string.IsNullOrEmpty(user))
                    opts.User = user;
                opts.Password = Uri.UnescapeDataString(userInfo[(separator + 1)..]);
            }
        }

        // 路径段可带 db 编号：redis://redis:6379/2
        var path = uri.AbsolutePath.Trim('/');
        if (!string.IsNullOrEmpty(path) && int.TryParse(path, out var db))
            opts.DefaultDatabase = db;

        return opts;
    }

    private static ConfigurationOptions ApplyParameters(
        ConfigurationOptions opts,
        Dictionary<string, string?> parameters)
    {
        if (parameters.Count == 0)
            return opts;

        // ConfigurationOptions 没有按 key 赋值的入口，只能拼回连接串重新 Parse。
        // Parse 不认识的 key 会静默忽略，所以密码等关键项在 Parse 之后重新按对象覆盖回去。
        var extra = string.Join(',', parameters.Select(kv => $"{kv.Key}={kv.Value}"));
        var user = opts.User;
        var password = opts.Password;

        var merged = Parse($"{opts},{extra}");
        merged.User = user;
        merged.Password = password;
        return merged;
    }

    private static ConfigurationOptions Parse(string raw)
    {
        try
        {
            return ConfigurationOptions.Parse(raw);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            throw new InvalidOperationException(
                $"Redis 连接配置无法解析：'{Redact(raw)}'。{ex.Message}", ex);
        }
    }

    private static void Validate(ConfigurationOptions opts)
    {
        if (opts.EndPoints.Count == 0)
        {
            throw new InvalidOperationException(
                $"Redis 连接配置缺少 {RedisConnectionOptions.SectionName}:Host（或 Endpoints / Url）。");
        }
    }

    private static string Redact(string raw)
    {
        var at = raw.IndexOf('@');
        if (at > 0 && raw.Contains("://", StringComparison.Ordinal))
        {
            var schemeEnd = raw.IndexOf("://", StringComparison.Ordinal) + 3;
            return string.Concat(raw.AsSpan(0, schemeEnd), "***@", raw.AsSpan(at + 1));
        }

        var idx = raw.IndexOf("password=", StringComparison.OrdinalIgnoreCase);
        return idx >= 0 ? string.Concat(raw.AsSpan(0, idx), "password=***") : raw;
    }
}
