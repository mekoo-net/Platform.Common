using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Serilog;
using ILogger = Serilog.ILogger;
using VaultSharp;
using VaultSharp.Core;
using VaultSharp.V1.AuthMethods.AppRole;

namespace Platform.Common.Hosting;

/// <summary>
/// 把 OpenBao 里的 KV v2 密钥叠加到配置上。一般不直接用，经 <see cref="OpenBaoConfigurationExtensions.AddOpenBao"/> 接入。
/// </summary>
public sealed class OpenBaoConfigurationSource(OpenBaoSettings settings, IReadOnlyList<string> secrets) : IConfigurationSource
{
    /// <summary>挂载点下放共享密钥的那一层。这下面的条目在配置里保留 <c>Shared:</c> 前缀，见 README。</summary>
    public const string SharedFolder = "Shared";

    public OpenBaoSettings Settings { get; } = settings;

    /// <summary>要读的 KV v2 路径，写成 <c>挂载/路径</c>：<c>Platform/Keystone</c>。按顺序叠加，后面的覆盖前面的同名键。</summary>
    public IReadOnlyList<string> Secrets { get; } = secrets;

    /// <summary>测试用：替换 VaultSharp 内部的 HttpClient（参数是 VaultSharp 准备好的 handler）。</summary>
    internal Func<HttpMessageHandler, HttpClient>? HttpClientFactory { get; init; }

    public IConfigurationProvider Build(IConfigurationBuilder builder) => new OpenBaoConfigurationProvider(this);
}

/// <summary>
/// 每一轮：读 AppRole 凭据 → 登录 → 读每个配置路径的整棵子树 → 吊销本轮令牌。不留长期令牌，也就不用管续期；
/// 登录本身很便宜，五分钟一轮可以忽略。
/// <para>
/// 路径层级就是配置层级（见 <see cref="ReadTreeAsync"/>）；一条密钥的 JSON 用框架自带的 JSON 配置解析器展开（见 <see cref="SecretJsonParser"/>）。
/// <b>任何时候都不把值写进日志</b>，只记路径和条数。
/// </para>
/// </summary>
internal sealed class OpenBaoConfigurationProvider : ConfigurationProvider, IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<OpenBaoConfigurationProvider>();
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(10);

    private readonly OpenBaoConfigurationSource _source;
    private readonly (string Mount, string Path)[] _secrets;
    private readonly object _timerGate = new();
    private Timer? _timer;
    private bool _disposed;

    public OpenBaoConfigurationProvider(OpenBaoConfigurationSource source)
    {
        _source = source;
        _secrets = source.Secrets.Select(ParseSecretPath).ToArray();
        if (_secrets.Length == 0)
            throw new InvalidOperationException("AddOpenBao(...) needs the KV paths this service reads, e.g. \"Platform/Keystone\"");
    }

    /// <summary>
    /// 启动时同步拉取：配置在这一刻就要齐，后面的连接串构建等都依赖它。连不上按退避重试到
    /// <see cref="OpenBaoSettings.StartupTimeout"/>（宿主机重启时给 OpenBao 解封的时间），再不行就<b>退回配置文件</b>：
    /// 记一条错误、用 yaml 里的值起来，之后每轮刷新照常重试，拉到了触发重载。
    /// yaml 里留 <c>null</c> 的项（生产的密码）照样由用到它的组件报"未配置"，不会带着空密码悄悄连上。
    /// </summary>
    public override void Load()
    {
        Data = FetchWithRetry() ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        StartRefreshTimer();
    }

    private Dictionary<string, string?>? FetchWithRetry()
    {
        var deadline = DateTime.UtcNow + _source.Settings.StartupTimeout;
        var delay = TimeSpan.FromSeconds(1);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return FetchAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    Logger.Error(ex,
                        "Could not load configuration from OpenBao at {Address} after {Attempt} attempt(s); " +
                        "falling back to the configuration files until a refresh succeeds",
                        _source.Settings.Address, attempt);
                    return null;
                }

                Logger.Warning(ex, "OpenBao not ready (attempt {Attempt}), retrying", attempt);
                Thread.Sleep(delay < remaining ? delay : remaining);
                delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxRetryDelay.Ticks));
            }
        }
    }

    /// <summary>
    /// 登录失败、OpenBao 不可达这类整轮失败抛出（启动时重试、刷新时保留旧值）。单个条目的问题——
    /// 路径下什么都没有、策略不放行——只让<b>这个条目</b>退回配置文件，其余条目照常生效：
    /// <c>Platform/Shared</c> 还没建好，不该连带 <c>Platform/Keystone</c> 里的数据库密码也读不到。
    /// </summary>
    private async Task<Dictionary<string, string?>> FetchAsync(CancellationToken ct)
    {
        var options = _source.Settings;
        var credentials = options.HasInlineCredentials
            ? new AppRoleCredentials(options.RoleId, options.SecretId)
            : await ReadAppRoleFileAsync(options.AppRoleFile, ct);
        var settings = new VaultClientSettings(
            options.Address.GetLeftPart(UriPartial.Authority),
            new AppRoleAuthMethodInfo(options.AppRoleMount, credentials.RoleId, credentials.SecretId));
        if (_source.HttpClientFactory is { } factory)
            settings.MyHttpClientProviderFunc = factory;

        var client = new VaultClient(settings);
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            // 先显式登录：不然登录在第一次读 KV 时才发生，凭据错误的 403 会被当成某个条目没权限。
            await client.V1.Auth.PerformImmediateLogin();

            foreach (var (mount, path) in _secrets)
            {
                var entry = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                int found;
                try
                {
                    found = await ReadTreeAsync(client, mount, path, relative: ConfigPrefix(path), entry);
                }
                catch (EntryDeniedException ex)
                {
                    Logger.Error("OpenBao {Mount}/{Path}: {Reason}; this entry falls back to the configuration files",
                        mount, path, ex.Message);
                    continue;
                }

                if (found == 0)
                {
                    Logger.Warning(
                        "OpenBao {Mount}/{Path}: nothing found (check the path, and that the policy grants read on data/ " +
                        "and list on metadata/); this entry falls back to the configuration files", mount, path);
                    continue;
                }

                foreach (var (key, value) in entry)
                    data[key] = value;
                Logger.Debug("OpenBao {Mount}/{Path}: {Count} secret(s)", mount, path, found);
            }
        }
        finally
        {
            // 本轮令牌用完即弃。吊销失败不影响结果，令牌到 TTL 自己会失效。
            try
            {
                await client.V1.Auth.Token.RevokeSelfAsync();
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, "OpenBao token revoke-self failed");
            }
        }
        return data;
    }

    /// <summary>
    /// 条目落到配置的哪一层。服务自己的树（<c>Platform/Keystone</c>）落在根上：<c>Platform/Keystone/Connection/Database</c>
    /// → <c>Connection:Database</c>。共享树（<c>Platform/Shared/…</c>）保留 <c>Shared</c> 及其后的路径：
    /// <c>Platform/Shared/Jwt/PublicKeys</c> → <c>Shared:Jwt:PublicKeys</c>——共享的东西在配置里也挂在 <c>Shared:</c> 下，
    /// 不和服务自己的同名节（Keystone 的 <c>Jwt</c> 放着私钥）混在一起，配置路径和 OpenBao 路径一一对应。
    /// </summary>
    private static string ConfigPrefix(string path)
        => path == OpenBaoConfigurationSource.SharedFolder
           || path.StartsWith(OpenBaoConfigurationSource.SharedFolder + "/", StringComparison.Ordinal)
            ? path.Replace('/', ':')
            : "";

    /// <summary>
    /// 读 <paramref name="path"/> 本身和它下面的每一条，返回读到的条数。路径本身那条的键原样用；
    /// 下面每一条的<b>相对路径就是配置前缀</b>：<c>Keystone/Connection/Database</c> 里的 <c>Password</c>
    /// → <c>Connection:Database:Password</c>。这样 UI 里逐层点进去看到的，就是 yaml 的层级。
    /// </summary>
    private static async Task<int> ReadTreeAsync(
        IVaultClient client, string mount, string path, string relative, Dictionary<string, string?> into)
    {
        var count = await ReadLeafAsync(client, mount, path, relative, into) ? 1 : 0;
        return count + await ReadChildrenAsync(client, mount, path, relative, into);
    }

    /// <summary>
    /// 同一层按名字排序，同名的叶子（<c>Jwt</c>）排在同名目录（<c>Jwt/</c>）前面，所以更深的路径覆盖较浅的同名键。
    /// 两处写同一个键本身就是录入错误，这里只保证结果确定。
    /// </summary>
    private static async Task<int> ReadChildrenAsync(
        IVaultClient client, string mount, string folder, string prefix, Dictionary<string, string?> into)
    {
        var count = 0;
        foreach (var child in (await ListOrEmptyAsync(client, mount, folder)).Order(StringComparer.Ordinal))
        {
            var name = child.TrimEnd('/');
            var childPath = $"{folder}/{name}";
            var childPrefix = prefix.Length == 0 ? name : ConfigurationPath.Combine(prefix, name);
            if (child.EndsWith('/'))
                count += await ReadChildrenAsync(client, mount, childPath, childPrefix, into);
            else if (await ReadLeafAsync(client, mount, childPath, childPrefix, into))
                count++;
        }
        return count;
    }

    /// <summary>
    /// 不存在返回 false（路径可能只是个目录）；403 让整个条目退回配置文件；其余错误抛出。都带上路径，
    /// 只看到一个 403 猜不出是哪条。
    /// </summary>
    private static async Task<bool> ReadLeafAsync(
        IVaultClient client, string mount, string path, string prefix, Dictionary<string, string?> into)
    {
        IDictionary<string, object>? values;
        try
        {
            values = (await client.V1.Secrets.KeyValue.V2.ReadSecretAsync(path: path, mountPoint: mount)).Data?.Data;
        }
        catch (Exception ex) when (IsNotFound(ex))
        {
            return false;
        }
        catch (Exception ex) when (IsDenied(ex))
        {
            throw new EntryDeniedException($"permission denied reading {mount}/{path} (policy needs read on data/)");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"reading {mount}/{path}: {ex.Message}", ex);
        }

        foreach (var (key, value) in SecretJsonParser.Parse(values))
            into[prefix.Length == 0 ? key : ConfigurationPath.Combine(prefix, key)] = value;
        return true;
    }

    /// <summary>
    /// 列不到子项（404）当作没有子项；403 不能当成没有——策略忘了给 list，整棵子树会被悄悄跳过、只剩一部分键，
    /// 所以整个条目退回配置文件，并记一条错误。
    /// </summary>
    private static async Task<IReadOnlyList<string>> ListOrEmptyAsync(IVaultClient client, string mount, string folder)
    {
        try
        {
            return (await client.V1.Secrets.KeyValue.V2.ReadSecretPathsAsync(path: folder, mountPoint: mount)).Data?.Keys?.ToList()
                   ?? [];
        }
        catch (Exception ex) when (IsNotFound(ex))
        {
            return [];
        }
        catch (Exception ex) when (IsDenied(ex))
        {
            throw new EntryDeniedException($"permission denied listing {mount}/{folder}/ (policy needs list on metadata/)");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"listing {mount}/{folder}/: {ex.Message}", ex);
        }
    }

    private static bool IsNotFound(Exception ex) => ex is VaultApiException { HttpStatusCode: HttpStatusCode.NotFound };

    private static bool IsDenied(Exception ex) => ex is VaultApiException { HttpStatusCode: HttpStatusCode.Forbidden };

    /// <summary>条目级的配置问题（策略不放行），不是 OpenBao 不可用：重试也没用，只让这个条目退回配置文件。</summary>
    private sealed class EntryDeniedException(string message) : Exception(message);

    private void StartRefreshTimer()
    {
        var interval = _source.Settings.RefreshInterval;
        if (interval <= TimeSpan.Zero)
            return;
        lock (_timerGate)
        {
            if (_disposed || _timer is not null)
                return;
            _timer = new Timer(_ => _ = RefreshAsync(), null, interval, Timeout.InfiniteTimeSpan);
        }
    }

    private async Task RefreshAsync()
    {
        try
        {
            var next = await FetchAsync(CancellationToken.None);
            if (!SameData(Data, next))
            {
                Data = next;
                OnReload();
                Logger.Information("OpenBao configuration changed, reloaded {Count} key(s)", next.Count);
            }
        }
        catch (Exception ex)
        {
            // 刷新失败保留手里那份：OpenBao 短暂不可达不该让运行中的服务丢掉密码。
            Logger.Warning(ex, "OpenBao refresh failed, keeping the current configuration");
        }
        finally
        {
            lock (_timerGate)
            {
                if (!_disposed)
                    _timer?.Change(_source.Settings.RefreshInterval, Timeout.InfiniteTimeSpan);
            }
        }
    }

    public void Dispose()
    {
        lock (_timerGate)
        {
            _disposed = true;
            _timer?.Dispose();
        }
    }

    private static (string Mount, string Path) ParseSecretPath(string entry)
    {
        var trimmed = entry.Trim().Trim('/');
        var slash = trimmed.IndexOf('/');
        if (slash <= 0 || slash == trimmed.Length - 1)
            throw new InvalidOperationException(
                $"OpenBao secret path '{entry}' must be '<mount>/<path>', e.g. Platform/Keystone");
        return (trimmed[..slash], trimmed[(slash + 1)..]);
    }

    private static async Task<AppRoleCredentials> ReadAppRoleFileAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var credentials = await JsonSerializer.DeserializeAsync<AppRoleCredentials>(stream, cancellationToken: ct);
        if (string.IsNullOrWhiteSpace(credentials?.RoleId) || string.IsNullOrWhiteSpace(credentials.SecretId))
            throw new InvalidOperationException($"OpenBao AppRole file {path} must contain role_id and secret_id");
        return credentials;
    }

    private static bool SameData(IDictionary<string, string?> current, IDictionary<string, string?> next)
        => current.Count == next.Count
           && next.All(kv => current.TryGetValue(kv.Key, out var value) && string.Equals(value, kv.Value, StringComparison.Ordinal));

    /// <summary>
    /// 一条密钥的 JSON 交给框架自带的 JSON 配置解析器展开——就是读 appsettings.json 用的那一套。
    /// OpenBao 只是换了个地方取 JSON，嵌套、数组、数字、null 怎么变成配置键，和读配置文件完全一样，不另写一份。
    /// </summary>
    private sealed class SecretJsonParser() : JsonStreamConfigurationProvider(new JsonStreamConfigurationSource())
    {
        public static IDictionary<string, string?> Parse(IDictionary<string, object>? values)
        {
            if (values is null || values.Count == 0)
                return new Dictionary<string, string?>();
            using var stream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(values));
            var parser = new SecretJsonParser();
            parser.Load(stream);
            return parser.Data;
        }
    }

    private sealed record AppRoleCredentials(
        [property: System.Text.Json.Serialization.JsonPropertyName("role_id")] string? RoleId,
        [property: System.Text.Json.Serialization.JsonPropertyName("secret_id")] string? SecretId);
}
