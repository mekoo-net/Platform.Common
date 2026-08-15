namespace Platform.Common.Caching;

/// <summary>平台 FusionCache 分场景 profile（yaml: <c>FusionCache:Profiles:</c>）。</summary>
public sealed class FusionCacheProfilesOptions
{
    /// <summary>Staff RBAC：<c>meeko:rbac:staff-role-perms:{role}</c>。</summary>
    public FusionCacheProfileOptions RbacStaff { get; set; } = new()
    {
        FailSafeMaxDuration = TimeSpan.FromMinutes(30),
    };

    /// <summary>User RBAC：<c>meeko:rbac:role-perms:{role}</c>。</summary>
    public FusionCacheProfileOptions RbacUser { get; set; } = new();

    /// <summary>JWT 黑名单：<c>meeko:jwt:revoked:{jti}</c>（Gateway 高频路径）。</summary>
    public FusionCacheProfileOptions JwtRevoked { get; set; } = new()
    {
        Duration = TimeSpan.FromSeconds(30),
        EnableFailSafe = false,
    };

    /// <summary>API Key 解析：<c>meeko:apikey:hash:{hash}</c>（Gateway 高频路径）。</summary>
    public FusionCacheProfileOptions ApiKey { get; set; } = new()
    {
        DistributedDuration = TimeSpan.FromHours(1),
        NegativeDuration = TimeSpan.FromSeconds(5),
    };

    /// <summary>账户限流配额：<c>meeko:gateway:ratelimit:{accountUid}</c>。</summary>
    public FusionCacheProfileOptions RateLimit { get; set; } = new();

    /// <summary>账户封禁状态：<c>meeko:gateway:suspended:{accountUid}</c>。</summary>
    public FusionCacheProfileOptions AccountSuspended { get; set; } = new()
    {
        Duration = TimeSpan.FromMinutes(1),
    };
}
