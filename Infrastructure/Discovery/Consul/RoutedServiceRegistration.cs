using Consul;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Common.Observability;

namespace Platform.Common.Discovery;

/// <summary>
/// 挂在平台网关后面的服务的注册——平台各服务和各产品服务是同一种：HTTP 存活检查，Meta 带路由、计费产品、
/// 定时任务清单和业务树上的位置。平台网关据此建路由，Billing 建产品目录，Jobs 排任务，遥测控制台拼业务树。
/// </summary>
public sealed class RoutedServiceRegistration(
    IConsulClient consul,
    LocalServiceInstance self,
    IOptions<ServiceDiscoveryOptions> discovery,
    IConfiguration configuration,
    ILogger<RoutedServiceRegistration> logger,
    ObservabilityOptions? observability = null) : ConsulRegistration(consul, logger)
{
    // 内容在进程生命周期里不变，构造时算一次：树上的声明写错在这里就抛，宿主起不来，不会带着错的注册跑。
    private readonly AgentServiceRegistration _registration =
        Build(self, discovery.Value, configuration, observability);

    protected override AgentServiceRegistration Describe() => _registration;

    private static AgentServiceRegistration Build(
        LocalServiceInstance self,
        ServiceDiscoveryOptions discovery,
        IConfiguration configuration,
        ObservabilityOptions? observability)
    {
        var meta = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ConsulServiceMeta.Routes] = ServiceRouteManifest.Encode(discovery.Gateway.Routes),
            [ConsulServiceMeta.BaseUrl] = self.RestBaseUrl.ToString(),
        };
        if (self.GrpcBaseUrl is { } grpc)
            meta[ConsulServiceMeta.GrpcUrl] = grpc.GetLeftPart(UriPartial.Authority);
        if (discovery.Products.Count > 0)
            meta[ConsulServiceMeta.Products] = ProductCatalogManifest.Encode(discovery.Products);
        if (discovery.Schedules.Count > 0)
            meta[ConsulServiceMeta.Schedules] = ScheduleManifest.Encode(discovery.Schedules);
        WorldServiceMeta.Write(meta, configuration, observability);

        return new AgentServiceRegistration
        {
            ID = self.Id,
            Name = self.Key,
            Address = self.Host,
            Port = self.RestBaseUrl.Port,
            Tags = ["meeko"],
            Meta = meta,
            Check = self.CreateHealthCheck(),
        };
    }
}
