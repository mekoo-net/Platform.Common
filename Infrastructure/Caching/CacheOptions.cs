namespace Platform.Common.Caching;

/// <summary>平台 FusionCache 注册选项（yaml: <c>FusionCache:</c>）。</summary>
public sealed class CacheOptions
{
    public const string SectionName = "FusionCache";

    /// <summary>缓存键前缀（meeko:&lt;service&gt;:）。</summary>
    public string ServiceName { get; set; } = "default";

    /// <summary>Redis L2 / Backplane 连接串；为空时仅启用 L1。</summary>
    public string? RedisConnectionString { get; set; }

    /// <summary>默认条目 L1 TTL。</summary>
    public TimeSpan DefaultDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>FailSafe：上游不可用时返回过期值并后台刷新。</summary>
    public bool EnableFailSafe { get; set; } = true;

    public TimeSpan FailSafeMaxDuration { get; set; } = TimeSpan.FromHours(2);
    public TimeSpan FailSafeThrottleDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>缓存 factory 软超时（超时后返回 stale / fail-safe，后台继续加载）。</summary>
    public TimeSpan FactorySoftTimeout { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>缓存 factory 硬超时（超时后取消加载并抛错 / 走 fail-safe）。</summary>
    public TimeSpan FactoryHardTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>分场景 profile；未在 yaml 中设置的项使用上方全局默认 + 代码内 profile 默认值。</summary>
    public FusionCacheProfilesOptions Profiles { get; set; } = new();
}
