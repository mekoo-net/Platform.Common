using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Platform.Common.HealthChecks;

public static class HealthChecksExtensions
{
    /// <summary>存活探针路径。Consul 健康检查也打这里，两边必须是同一个常量。</summary>
    public const string LivePath = "/health/live";

    /// <summary>
    /// 注册 /health/live + /health/ready 标准端点。每个服务必有。
    /// </summary>
    public static IEndpointRouteBuilder MapHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(LivePath, new HealthCheckOptions
        {
            Predicate = _ => false,
        }).AllowAnonymous();

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
        }).AllowAnonymous();

        return endpoints;
    }
}
