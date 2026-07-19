using Platform.Common.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Platform.Common.Caching;

public static class CacheExtensions
{
    /// <summary>
    /// 注册平台标准 FusionCache（L1 + 可选 L2 Redis + Backplane）。
    /// serviceName 用作缓存键前缀的中间段：meeko:&lt;serviceName&gt;:&lt;entity&gt;:&lt;id&gt;。
    /// </summary>
    public static IServiceCollection AddPlatformCache(
        this IServiceCollection services,
        string serviceName,
        IConfiguration configuration)
    {
        var options = new CacheOptions { ServiceName = serviceName };
        configuration.GetSection(CacheOptions.SectionName).Bind(options);
        options.ServiceName = serviceName;
        options.RedisConnectionString ??= configuration.GetRedisConnectionString();
        services.AddSingleton(options);

        var fusion = services.AddFusionCache()
            .WithDefaultEntryOptions(new FusionCacheEntryOptions
            {
                Duration = options.DefaultDuration,
                IsFailSafeEnabled = options.EnableFailSafe,
                FailSafeMaxDuration = options.FailSafeMaxDuration,
                FailSafeThrottleDuration = options.FailSafeThrottleDuration,
                FactorySoftTimeout = options.FactorySoftTimeout,
                FactoryHardTimeout = options.FactoryHardTimeout,
                AllowBackgroundDistributedCacheOperations = true,
            });

        if (!string.IsNullOrWhiteSpace(options.RedisConnectionString))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                RedisConnectionResolver.ConnectMultiplexer(options.RedisConnectionString!));

            services.AddStackExchangeRedisCache(o =>
            {
                o.Configuration = options.RedisConnectionString;
                o.InstanceName = $"meeko:{options.ServiceName}:";
            });

            fusion
                .WithSerializer(new FusionCacheSystemTextJsonSerializer())
                .WithRegisteredDistributedCache()
                .WithBackplane(new RedisBackplane(new RedisBackplaneOptions
                {
                    Configuration = options.RedisConnectionString,
                }));
        }

        return services;
    }
}
