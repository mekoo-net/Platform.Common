using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.Common.Identity;

namespace Platform.Common.Authorization;

/// <summary>
/// 平台 Staff permission 授权要求。配合 <see cref="PermissionAuthorizationHandler"/> 使用，
/// 通过 <see cref="IStaffPermissionAuthorizer"/> 查 Redis 缓存（命中时不打 Keystone）。
/// </summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Permission code is required.", nameof(code));
        Code = code;
    }

    public string Code { get; }
}

/// <summary>
/// 直接在 controller / action 上声明 permission 校验。基于 .NET 8+
/// <see cref="IAuthorizationRequirementData"/>，无需注册具名 policy。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute, IAuthorizationRequirementData
{
    public RequirePermissionAttribute(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Permission code is required.", nameof(code));
        Code = code;
    }

    public string Code { get; }

    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return new PermissionRequirement(Code);
    }
}

internal sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IStaffPermissionAuthorizer _authz;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PermissionAuthorizationHandler(
        IStaffPermissionAuthorizer authz,
        IHttpContextAccessor httpContextAccessor)
    {
        _authz = authz;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var ct = _httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        if (await _authz.HasPermissionAsync(requirement.Code, ct))
        {
            context.Succeed(requirement);
        }
    }
}

public static class PermissionEndpointExtensions
{
    /// <summary>
    /// 注册基于 permission code 的 endpoint 授权约束。
    /// </summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string code)
        where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(p => p.AddRequirements(new PermissionRequirement(code)));

    /// <summary>
    /// 注册 permission authorization handler（与 <see cref="IStaffPermissionAuthorizer"/> 一起）。
    /// </summary>
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAuthorizationHandler, PermissionAuthorizationHandler>());
        return services;
    }
}
