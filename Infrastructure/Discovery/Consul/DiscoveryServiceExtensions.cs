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
        RejectRetiredSections(builder.Configuration);
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

    // 部署机上的 yaml 是手工维护的，升级镜像时最容易漏改。旧节若静默忽略：Consul 那节只会报成"缺少
    // ConnectionStrings:Consul"，看不出是搬了家；Service 那节会让人以为改 Address 还能改注册地址。
    // 所以见到旧节直接拒绝启动，并把新写法写进报错。
    private static void RejectRetiredSections(IConfiguration configuration)
    {
        List<string>? retired = null;

        if (configuration.GetSection("Discovery:Consul").Exists())
            (retired ??= []).Add("Discovery:Consul → 改写到 ConnectionStrings:Consul（Host / Port / Datacenter / Token）");

        if (configuration.GetSection("Discovery:Service").Exists())
            (retired ??= []).Add("Discovery:Service → 整段删除：Key 写死在代码（ServiceKeys），地址由 hostname + Kestrel 端点推导");

        if (retired is not null)
        {
            throw new InvalidOperationException(
                "服务发现配置已迁移，以下节不再读取：" + string.Join("；", retired)
                + "。仓库里的 yaml 和部署机上的 /opt/meeko/<服务>.yaml 都要改。");
        }
    }
}
