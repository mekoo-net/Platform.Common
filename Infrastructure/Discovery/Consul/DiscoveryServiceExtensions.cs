using Consul;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Common.Discovery;

/// <summary>
/// Consul 接入：客户端，以及挂在平台网关后面的服务向 Consul 的注册。
/// </summary>
public static class DiscoveryServiceExtensions
{
    /// <summary>
    /// 本服务以 <paramref name="serviceKey"/> 注册进 Consul，并一直守着这条注册（<see cref="RoutedServiceRegistration"/>）。
    /// 地址取本机连 Consul 的源 IP 与 Kestrel 端点；路由、产品、定时任务读 <c>Discovery</c> 节，宿主可再 PostConfigure
    /// <see cref="ServiceDiscoveryOptions"/>。须在 <see cref="AddServiceDiscovery"/> 之后调用。
    /// </summary>
    /// <param name="serviceKey">本服务的 <see cref="ServiceKeys"/>，调用方按它找实例。</param>
    public static WebApplicationBuilder AddRoutedServiceRegistration(this WebApplicationBuilder builder, string serviceKey)
    {
        builder.Services.Configure<ServiceDiscoveryOptions>(
            builder.Configuration.GetSection(ServiceDiscoveryOptions.SectionName));
        builder.Services.AddSingleton(LocalServiceInstance.Resolve(serviceKey, builder.Configuration));
        builder.Services.AddHostedService<RoutedServiceRegistration>();
        return builder;
    }

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
