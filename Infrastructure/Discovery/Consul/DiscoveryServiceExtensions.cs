using Consul;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Platform.Common.Discovery;

namespace Platform.Common.Discovery;

/// <summary>
/// 通用 Consul 客户端接入（不含 Meeko 平台 manifest / gRPC 身份透传）。
/// </summary>
public static class DiscoveryServiceExtensions
{
    public static WebApplicationBuilder AddServiceDiscovery(this WebApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection(DiscoveryOptions.SectionName);
        builder.Services.Configure<DiscoveryOptions>(section);

        builder.Services.AddSingleton<IConsulClient>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<DiscoveryOptions>>().Value.Consul;
            if (string.IsNullOrWhiteSpace(opts.Address))
            {
                throw new InvalidOperationException(
                    "Discovery:Consul:Address is empty. Configure it in the service YAML.");
            }

            return new ConsulClient(cfg =>
            {
                cfg.Address = new Uri(opts.Address);
                if (!string.IsNullOrWhiteSpace(opts.Datacenter))
                {
                    cfg.Datacenter = opts.Datacenter;
                }
                if (!string.IsNullOrWhiteSpace(opts.Token))
                {
                    cfg.Token = opts.Token;
                }
            });
        });

        return builder;
    }
}
