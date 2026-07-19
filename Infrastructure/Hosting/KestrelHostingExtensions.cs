using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Platform.Common.Hosting;

/// <summary>
/// Kestrel helpers for Meeko platform services.
/// Prefer <see cref="ConfigurePlatformKestrel"/> + yaml <c>Kestrel:Endpoints</c>.
/// Product 三端口约定见 <c>Product.Common</c>。
/// </summary>
public static class KestrelHostingExtensions
{
    public const string BindHostConfigKey = "Meeko:Bind:Host";

    /// <summary>
    /// Load listen URLs / limits from <c>Kestrel:*</c> in the service yaml.
    /// </summary>
    public static IWebHostBuilder ConfigurePlatformKestrel(this IWebHostBuilder host)
        => host.PreferHostingUrls(false)
            .ConfigureKestrel((context, serverOptions) =>
            {
                var kestrelSection = context.Configuration.GetSection("Kestrel");
                if (kestrelSection.Exists())
                    serverOptions.Configure(kestrelSection);

                serverOptions.Limits.MinResponseDataRate = null;
                serverOptions.Limits.MinRequestBodyDataRate = null;
            });

    /// <summary>
    /// Legacy: hard-coded HTTP/1 + HTTP/2 ports. Prefer yaml <c>Kestrel:Endpoints</c>.
    /// </summary>
    [Obsolete("Use ConfigurePlatformKestrel() and Kestrel:Endpoints in service yaml.")]
    public static IWebHostBuilder UsePlatformKestrel(
        this IWebHostBuilder host,
        int http1Port,
        int http2Port)
    {
        return host.PreferHostingUrls(false)
            .UseUrls()
            .ConfigureKestrel((context, k) =>
            {
                var bind = ResolveBindAddress(context.Configuration[BindHostConfigKey]);
                k.Listen(bind, http1Port, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
                k.Listen(bind, http2Port, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
            });
    }

    private static System.Net.IPAddress ResolveBindAddress(string? host)
    {
        if (string.IsNullOrWhiteSpace(host) || host is "127.0.0.1" or "localhost")
            return System.Net.IPAddress.Loopback;

        if (host.Equals("any", StringComparison.OrdinalIgnoreCase)
            || host == "0.0.0.0"
            || host == "*")
            return System.Net.IPAddress.Any;

        return System.Net.IPAddress.Parse(host);
    }
}
