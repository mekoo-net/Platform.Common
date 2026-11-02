using Consul;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Common.Discovery;

/// <summary>
/// 通用 Consul 客户端接入（不含 Meeko 平台 manifest / gRPC 身份透传）。
/// </summary>
public static class DiscoveryServiceExtensions
{
    /// <summary>
    /// 按 <c>ConnectionStrings:Consul</c> 注册 <see cref="IConsulClient"/> 单例。
    /// 连接配置在这里就读，缺了启动即失败，而不是拖到第一次 RPC 才在解析器里报"找不到实例"。
    /// </summary>
    public static WebApplicationBuilder AddServiceDiscovery(this WebApplicationBuilder builder)
    {
        var settings = ConsulConnectionResolver.GetRequired(builder.Configuration);

        builder.Services.AddSingleton<IConsulClient>(_ => new ConsulClient(cfg =>
        {
            cfg.Address = settings.Address;
            if (settings.Datacenter is not null)
                cfg.Datacenter = settings.Datacenter;
            if (settings.Token is not null)
                cfg.Token = settings.Token;
        }));

        return builder;
    }
}
