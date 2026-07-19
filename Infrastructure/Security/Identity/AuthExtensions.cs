using Platform.Common.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Platform.Common.Identity;

public static class AuthExtensions
{
    /// <summary>
    /// Gateway 下游标准身份注册：<c>AddGatewayForwardedAuth</c> + <c>AddCurrentAuth</c>。
    /// </summary>
    public static IServiceCollection AddGatewayIdentity(this IServiceCollection services)
    {
        services.AddGatewayForwardedAuth();
        services.AddCurrentAuth();
        return services;
    }

    public static IServiceCollection AddCurrentAuth(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.TryAddScoped<ICurrentAuth, CurrentAuthAccessor>();
        return services;
    }

    public static IServiceCollection AddCurrentActor(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.TryAddScoped<ICurrentActor, CurrentActorAccessor>();
        return services;
    }
}
