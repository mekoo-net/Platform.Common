using MassTransit;

namespace Platform.Common.Messaging;

/// <summary>
/// 把 <see cref="RabbitMqSettings"/> 落到 MassTransit 的 host 配置上。
/// </summary>
/// <remarks>
/// 这个方法存在的理由：在此之前 Billing 和 Keystone 各自抄了一段 <c>cfg.Host(...)</c>，
/// 两段都只设了用户名密码——配 <c>amqps://</c> 时 TLS 从来没被打开过，
/// 解析出来的 5671 端口对着一个不启用 TLS 的客户端。收敛成一处才不会再漏。
/// </remarks>
public static class RabbitMqMassTransitExtensions
{
    public static void ApplyPlatformRabbitMqHost(
        this IRabbitMqBusFactoryConfigurator cfg,
        RabbitMqSettings settings)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        ArgumentNullException.ThrowIfNull(settings);

        cfg.Host(settings.Host, settings.Port, settings.VirtualHost, h =>
        {
            h.Username(settings.Username);
            h.Password(settings.Password);

            if (settings.PublisherConfirmation is bool publisherConfirmation)
                h.PublisherConfirmation = publisherConfirmation;

            if (!string.IsNullOrWhiteSpace(settings.ConnectionName))
                h.ConnectionName(settings.ConnectionName);

            if (settings.Heartbeat is ushort heartbeat)
                h.Heartbeat(heartbeat);
            if (settings.RequestedConnectionTimeout is int connectionTimeout)
                h.RequestedConnectionTimeout(connectionTimeout);
            if (settings.RequestedChannelMax is ushort channelMax)
                h.RequestedChannelMax(channelMax);
            if (settings.RequestedFrameMax is uint frameMax)
                h.RequestedFrameMax(frameMax);
            if (settings.MaxMessageSize is uint maxMessageSize)
                h.MaxMessageSize(maxMessageSize);
            if (settings.ContinuationTimeout is TimeSpan continuationTimeout)
                h.ContinuationTimeout(continuationTimeout);

            if (!settings.UseSsl)
                return;

            h.UseSsl(ssl =>
            {
                if (!string.IsNullOrWhiteSpace(settings.SslServerName))
                    ssl.ServerName = settings.SslServerName;
                if (settings.SslProtocol is { } protocol)
                    ssl.Protocol = protocol;
                if (!string.IsNullOrWhiteSpace(settings.SslCertificatePath))
                    ssl.CertificatePath = settings.SslCertificatePath;
                if (!string.IsNullOrWhiteSpace(settings.SslCertificatePassphrase))
                    ssl.CertificatePassphrase = settings.SslCertificatePassphrase;
            });
        });
    }
}
