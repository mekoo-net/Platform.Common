using ZiggyCreatures.Caching.Fusion;

namespace Platform.Common.Caching;

public static class FusionCacheProfileExtensions
{
    public static FusionCacheEntryOptions ApplyProfile(
        this FusionCacheEntryOptions options,
        CacheOptions defaults,
        FusionCacheProfileOptions profile)
    {
        var duration = profile.Duration ?? defaults.DefaultDuration;
        options.Duration = duration;
        options.DistributedCacheDuration = profile.DistributedDuration ?? duration;

        options.IsFailSafeEnabled = profile.EnableFailSafe ?? defaults.EnableFailSafe;
        options.FailSafeMaxDuration = profile.FailSafeMaxDuration ?? defaults.FailSafeMaxDuration;
        options.FailSafeThrottleDuration = profile.FailSafeThrottleDuration ?? defaults.FailSafeThrottleDuration;

        return options;
    }
}
