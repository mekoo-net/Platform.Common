using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Platform.Common.Hosting;

public static class RedisHostingExtensions
{
    /// <summary>
    /// 注册 Redis 硬依赖：<see cref="IConnectionMultiplexer"/> 在 Host 启动时建立连接并 Ping，失败则进程不启动。
    /// </summary>
    public static IServiceCollection AddRequiredRedis(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            RedisConnectionResolver.ConnectMultiplexer(configuration, requireConnected: true));
        services.AddHostedService<RedisConnectionStartupHostedService>();
        return services;
    }

    private sealed class RedisConnectionStartupHostedService : IHostedService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisConnectionStartupHostedService> _logger;

        public RedisConnectionStartupHostedService(
            IConnectionMultiplexer redis,
            ILogger<RedisConnectionStartupHostedService> logger)
        {
            _redis = redis;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Redis connected -> {Endpoints}",
                RedisConnectionResolver.FormatEndPoints(_redis.GetEndPoints()));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
