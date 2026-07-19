using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Platform.Common.Authorization;
using Platform.Common.Caching;
using ZiggyCreatures.Caching.Fusion;

namespace Platform.Common.Identity;

/// <summary>
/// Account 域 IamUser 权限校验。基于 <see cref="ICurrentActor.Role"/> +
/// <see cref="IAccountRolePermissionSource"/> + FusionCache。
/// </summary>
public interface IAccountPermissionAuthorizer
{
    Task<bool> HasPermissionAsync(string permissionCode, CancellationToken ct = default);
}

/// <summary>Account 角色 → permission codes 数据源（由调用方注册）。</summary>
public interface IAccountRolePermissionSource
{
    Task<IReadOnlyCollection<string>> GetPermissionsForAccountRoleAsync(string roleName, CancellationToken ct = default);
}

internal sealed class FusionCacheAccountPermissionAuthorizer : IAccountPermissionAuthorizer
{
    private readonly IFusionCache _cache;
    private readonly IAccountRolePermissionSource _source;
    private readonly ICurrentActor _actor;
    private readonly CacheOptions _cacheOptions;
    private readonly ILogger<FusionCacheAccountPermissionAuthorizer> _logger;

    public FusionCacheAccountPermissionAuthorizer(
        IFusionCache cache,
        IAccountRolePermissionSource source,
        ICurrentActor actor,
        CacheOptions cacheOptions,
        ILogger<FusionCacheAccountPermissionAuthorizer> logger)
    {
        _cache = cache;
        _source = source;
        _actor = actor;
        _cacheOptions = cacheOptions;
        _logger = logger;
    }

    public Task<bool> HasPermissionAsync(string permissionCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(permissionCode)) return Task.FromResult(false);
        if (_actor.ActorType != ActorType.User) return Task.FromResult(false);
        if (string.IsNullOrWhiteSpace(_actor.Role)) return Task.FromResult(false);

        return HasPermissionForRoleAsync(_actor.Role.Trim(), permissionCode, ct);
    }

    private async Task<bool> HasPermissionForRoleAsync(string roleName, string permissionCode, CancellationToken ct)
    {
        var perms = await GetPermissionsAsync(roleName, ct);
        return perms.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyCollection<string>> GetPermissionsAsync(string roleName, CancellationToken ct)
    {
        var key = $"meeko:rbac:role-perms:{roleName.ToLowerInvariant()}";
        return await _cache.GetOrSetAsync<IReadOnlyCollection<string>>(
            key,
            async (_, innerCt) =>
            {
                try
                {
                    return await _source.GetPermissionsForAccountRoleAsync(roleName, innerCt);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load account role permissions for {Role}; falling back to empty set (not cached).", roleName);
                    throw;
                }
            },
            failSafeDefaultValue: Array.Empty<string>(),
            setupAction: opt => opt.ApplyProfile(_cacheOptions, _cacheOptions.Profiles.RbacUser),
            token: ct);
    }
}

internal sealed class AccountPermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IAccountPermissionAuthorizer _authz;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AccountPermissionAuthorizationHandler(
        IAccountPermissionAuthorizer authz,
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
            context.Succeed(requirement);
    }
}

public static class AccountPermissionAuthorizerExtensions
{
    /// <summary>
    /// 注册 Account 域 permission 校验 + handler（与 Staff 的 <see cref="StaffPermissionAuthorizerExtensions.AddStaffPermissions"/> 并行）。
    /// </summary>
    public static IServiceCollection AddAccountPermissions(this IServiceCollection services)
    {
        services.TryAddScoped<IAccountPermissionAuthorizer, FusionCacheAccountPermissionAuthorizer>();
        services.AddHttpContextAccessor();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAuthorizationHandler, AccountPermissionAuthorizationHandler>());
        return services;
    }
}
