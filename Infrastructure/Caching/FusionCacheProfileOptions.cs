namespace Platform.Common.Caching;

/// <summary>
/// FusionCache 分场景配置；未设置的项回落到 <see cref="CacheOptions"/> 全局默认。
/// </summary>
public sealed class FusionCacheProfileOptions
{
    /// <summary>L1（内存）TTL。</summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>L2（Redis）TTL；未设置时与 <see cref="Duration"/> 相同。</summary>
    public TimeSpan? DistributedDuration { get; set; }

    /// <summary>是否启用 FailSafe；未设置时用全局 <see cref="CacheOptions.EnableFailSafe"/>。</summary>
    public bool? EnableFailSafe { get; set; }

    /// <summary>FailSafe 最长 stale 窗口。</summary>
    public TimeSpan? FailSafeMaxDuration { get; set; }

    /// <summary>FailSafe 后台刷新节流间隔。</summary>
    public TimeSpan? FailSafeThrottleDuration { get; set; }

    /// <summary>
    /// 负缓存 TTL（未命中 / 无效结果）。未设置时调用方自行决定；ApiKey 等解析路径常用短 TTL 防穿透。
    /// </summary>
    public TimeSpan? NegativeDuration { get; set; }
}
