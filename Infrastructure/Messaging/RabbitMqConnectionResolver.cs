using System.Security.Authentication;
using Microsoft.Extensions.Configuration;
using Platform.Common.Hosting;
using Platform.Common.Observability;

namespace Platform.Common.Messaging;

/// <summary>
/// 解析完成、各项均已定值的 RabbitMQ 连接参数，供 MassTransit <c>cfg.Host(...)</c> 使用。
/// 可空项表示"未配置，用 MassTransit / RabbitMQ.Client 自己的默认值"。
/// </summary>
public sealed record RabbitMqSettings(
    string Host,
    ushort Port,
    string VirtualHost,
    string Username,
    string Password)
{
    public string? ConnectionName { get; init; }

    public bool UseSsl { get; init; }
    public string? SslServerName { get; init; }
    public SslProtocols? SslProtocol { get; init; }
    public string? SslCertificatePath { get; init; }
    public string? SslCertificatePassphrase { get; init; }

    public ushort? Heartbeat { get; init; }
    public int? RequestedConnectionTimeout { get; init; }
    public ushort? RequestedChannelMax { get; init; }
    public uint? RequestedFrameMax { get; init; }
    public uint? MaxMessageSize { get; init; }
    public TimeSpan? ContinuationTimeout { get; init; }
    public bool? PublisherConfirmation { get; init; }
}

/// <summary>
/// 读 <c>ConnectionStrings:RabbitMq</c>。判定规则复用
/// <see cref="ConnectionSectionReader"/>（标量整串 / 结构化映射 / 未配置），与 PG、Redis 同一套。
/// </summary>
public static class RabbitMqConnectionResolver
{
    private const ushort DefaultPort = 5672;
    private const ushort DefaultTlsPort = 5671;

    public static RabbitMqSettings? Resolve(IConfiguration configuration)
    {
        var options = ConnectionSectionReader.TryRead(
            configuration,
            RabbitMqConnectionOptions.SectionName,
            static url => new RabbitMqConnectionOptions { Url = url },
            static o => !string.IsNullOrWhiteSpace(o.Url) || !string.IsNullOrWhiteSpace(o.Host));

        return options is null ? null : Build(options, configuration.ResolveServiceName());
    }

    public static RabbitMqSettings GetRequired(IConfiguration configuration) =>
        Resolve(configuration)
        ?? throw new InvalidOperationException(
            $"缺少 RabbitMQ 连接配置。请在本服务的 yaml 里配置 {RabbitMqConnectionOptions.SectionName}"
            + "（Host / Port / VirtualHost / Username / Password）。");

    internal static RabbitMqSettings Build(RabbitMqConnectionOptions options, string? defaultConnectionName = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        string? host = null;
        ushort? port = null;
        string? virtualHost = null;
        string? username = null;
        string? password = null;
        var useSsl = false;

        if (!string.IsNullOrWhiteSpace(options.Url))
            (host, port, virtualHost, username, password, useSsl) = ParseUri(options.Url.Trim());

        if (!string.IsNullOrWhiteSpace(options.Host))
            host = options.Host.Trim();
        if (options.Port is int explicitPort)
            port = ToUInt16(explicitPort, nameof(RabbitMqConnectionOptions.Port));
        if (!string.IsNullOrWhiteSpace(options.VirtualHost))
            virtualHost = options.VirtualHost.Trim();
        if (!string.IsNullOrWhiteSpace(options.Username))
            username = options.Username.Trim();
        if (options.Password is not null)
            password = options.Password;
        if (options.Ssl is bool explicitSsl)
            useSsl = explicitSsl;

        Validate(host, username, password);

        return new RabbitMqSettings(
            host!,
            port ?? (useSsl ? DefaultTlsPort : DefaultPort),
            string.IsNullOrWhiteSpace(virtualHost) ? "/" : virtualHost,
            username!,
            password!)
        {
            ConnectionName = options.ConnectionName ?? defaultConnectionName,
            UseSsl = useSsl,
            SslServerName = Trimmed(options.SslServerName),
            SslProtocol = options.SslProtocol,
            SslCertificatePath = Trimmed(options.SslCertificatePath),
            SslCertificatePassphrase = options.SslCertificatePassphrase,
            Heartbeat = options.Heartbeat is int hb
                ? ToUInt16(hb, nameof(RabbitMqConnectionOptions.Heartbeat))
                : null,
            RequestedConnectionTimeout = options.RequestedConnectionTimeout,
            RequestedChannelMax = options.RequestedChannelMax is int cm
                ? ToUInt16(cm, nameof(RabbitMqConnectionOptions.RequestedChannelMax))
                : null,
            RequestedFrameMax = options.RequestedFrameMax is long fm
                ? ToUInt32(fm, nameof(RabbitMqConnectionOptions.RequestedFrameMax))
                : null,
            MaxMessageSize = options.MaxMessageSize is long ms
                ? ToUInt32(ms, nameof(RabbitMqConnectionOptions.MaxMessageSize))
                : null,
            ContinuationTimeout = options.ContinuationTimeout,
            PublisherConfirmation = options.PublisherConfirmation,
        };
    }

    private static (string Host, ushort Port, string VirtualHost, string? Username, string? Password, bool UseSsl)
        ParseUri(string raw)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)
            || (!uri.Scheme.Equals("amqp", StringComparison.OrdinalIgnoreCase)
                && !uri.Scheme.Equals("amqps", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"{RabbitMqConnectionOptions.SectionName}:Url 必须是 amqp:// 或 amqps:// URI，"
                + $"如 \"amqp://meeko:***@rabbitmq:5672/\"。实际收到：'{Redact(raw)}'。");
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
            throw new InvalidOperationException($"RabbitMQ URI '{Redact(raw)}' 没有主机。");

        var useSsl = uri.Scheme.Equals("amqps", StringComparison.OrdinalIgnoreCase);
        var port = uri.Port > 0 ? (ushort)uri.Port : (useSsl ? DefaultTlsPort : DefaultPort);

        // 路径首字符是 '/' 分隔符，其后才是 vhost；整个路径只有 "/" 时表示默认 vhost。
        var path = uri.AbsolutePath;
        var vhost = path.Length <= 1 ? "/" : Uri.UnescapeDataString(path[1..]);

        string? username = null;
        string? password = null;
        var userInfo = uri.UserInfo;
        if (!string.IsNullOrEmpty(userInfo))
        {
            var separator = userInfo.IndexOf(':');
            if (separator < 0)
            {
                username = Uri.UnescapeDataString(userInfo);
            }
            else
            {
                username = Uri.UnescapeDataString(userInfo[..separator]);
                password = Uri.UnescapeDataString(userInfo[(separator + 1)..]);
            }
        }

        return (uri.Host, port, vhost, username, password, useSsl);
    }

    private static void Validate(string? host, string? username, string? password)
    {
        List<string>? missing = null;
        void Require(bool ok, string field)
        {
            if (!ok)
                (missing ??= []).Add($"{RabbitMqConnectionOptions.SectionName}:{field}");
        }

        Require(!string.IsNullOrWhiteSpace(host), nameof(RabbitMqConnectionOptions.Host));
        Require(!string.IsNullOrWhiteSpace(username), nameof(RabbitMqConnectionOptions.Username));
        Require(password is not null, nameof(RabbitMqConnectionOptions.Password));

        if (missing is not null)
        {
            throw new InvalidOperationException(
                $"RabbitMQ 连接配置缺少：{string.Join("、", missing)}。"
                + " 无密码请显式写 Password: \"\"，不要留空。");
        }
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ushort ToUInt16(int value, string field) =>
        value is >= ushort.MinValue and <= ushort.MaxValue
            ? (ushort)value
            : throw new InvalidOperationException(
                $"{RabbitMqConnectionOptions.SectionName}:{field} 超出范围（0..{ushort.MaxValue}）：{value}。");

    private static uint ToUInt32(long value, string field) =>
        value is >= uint.MinValue and <= uint.MaxValue
            ? (uint)value
            : throw new InvalidOperationException(
                $"{RabbitMqConnectionOptions.SectionName}:{field} 超出范围（0..{uint.MaxValue}）：{value}。");

    private static string? ResolveServiceName(this IConfiguration configuration) =>
        configuration[$"{ObservabilityOptions.SectionName}:{nameof(ObservabilityOptions.ServiceName)}"];

    private static string Redact(string raw)
    {
        var at = raw.IndexOf('@');
        var schemeEnd = raw.IndexOf("://", StringComparison.Ordinal);
        return at > 0 && schemeEnd > 0
            ? string.Concat(raw.AsSpan(0, schemeEnd + 3), "***@", raw.AsSpan(at + 1))
            : raw;
    }
}
