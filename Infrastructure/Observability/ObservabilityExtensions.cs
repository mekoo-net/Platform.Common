using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Instrumentation.EntityFrameworkCore;
using OpenTelemetry.Instrumentation.GrpcNetClient;
using OpenTelemetry.Instrumentation.Hangfire;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Platform.Common.Observability;

public static class ObservabilityExtensions
{
    /// <summary>
    /// 注册平台标准 OpenTelemetry 管线（Logs/Traces/Metrics → OTLP）。
    /// 调用顺序：先 AddPlatformObservability，再 builder.Host.UsePlatformSerilog。
    /// 配置全部来自各服务 YAML，不读 OTEL_* / ASPNETCORE_* 环境变量。
    /// 采集端地址读 <c>ConnectionStrings:Otlp</c>（<see cref="OtlpConnectionResolver"/>），没配就不导出。
    /// </summary>
    public static IHostApplicationBuilder AddPlatformObservability(
        this IHostApplicationBuilder builder,
        Action<ObservabilityOptions>? configure = null)
    {
        RejectRetiredOtlpEndpoint(builder.Configuration);

        var options = new ObservabilityOptions();
        builder.Configuration.GetSection(ObservabilityOptions.SectionName).Bind(options);
        options.OtlpEndpoint = OtlpConnectionResolver.Resolve(builder.Configuration);
        configure?.Invoke(options);

        // service.name 由各服务在 configure 里写死，yaml 不必再写；但 PG application_name、Redis client name、
        // RabbitMQ 连接名都是从配置读它的。把生效值放进优先级最高的配置源，那几处才认得出是哪个服务。
        builder.Configuration.AddInMemoryCollection(
        [
            new($"{ObservabilityOptions.SectionName}:{nameof(ObservabilityOptions.ServiceName)}", options.ServiceName),
        ]);

        builder.Services.AddSingleton(options);

        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(options.ServiceName)
                .AddAttributes(new KeyValuePair<string, object>[]
                {
                    new("host.name", Environment.MachineName),
                }));

        otel.WithTracing(tracing =>
        {
            tracing
                .AddSource(PlatformActivitySource.Name)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation();

            if (options.EnableGrpcClientInstrumentation)
            {
                tracing.AddGrpcClientInstrumentation();
            }

            if (options.EnableEntityFrameworkInstrumentation)
            {
                tracing.AddEntityFrameworkCoreInstrumentation();
            }

            if (options.EnableRedisInstrumentation)
            {
                tracing.AddRedisInstrumentation();
            }

            if (options.EnableHangfireInstrumentation)
            {
                tracing.AddHangfireInstrumentation();
            }

            foreach (var src in options.AdditionalSources)
            {
                tracing.AddSource(src);
            }

            tracing.AddProcessor(new MagicOnionSpanNameProcessor());

            if (options.OtlpEndpoint is { } endpoint)
            {
                tracing.AddOtlpExporter(o => o.Endpoint = endpoint);
            }
        });

        otel.WithMetrics(metrics =>
        {
            metrics
                .AddMeter("Microsoft.AspNetCore.Hosting")
                .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
                .AddMeter("System.Net.Http")
                .AddRuntimeInstrumentation();

            foreach (var meter in options.AdditionalMeters)
            {
                metrics.AddMeter(meter);
            }

            if (options.OtlpEndpoint is { } endpoint)
            {
                metrics.AddOtlpExporter(o => o.Endpoint = endpoint);
            }
        });

        otel.WithLogging(
            configureBuilder: null,
            configureOptions: logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;

                if (options.OtlpEndpoint is { } endpoint)
                {
                    logging.AddOtlpExporter(o => o.Endpoint = endpoint);
                }
            });

        return builder;
    }

    // 部署机上的 yaml 是手工维护的，升级镜像时最容易漏改。旧键有值却静默忽略的话，遥测会无声无息地断掉，
    // 所以直接拒绝启动并把新写法写进报错。值为空本来就是"不导出"，跟新写法不配等价，放过。
    private static void RejectRetiredOtlpEndpoint(IConfiguration configuration)
    {
        var retired = configuration[$"{ObservabilityOptions.SectionName}:OtlpEndpoint"];
        if (string.IsNullOrWhiteSpace(retired))
            return;

        throw new InvalidOperationException(
            $"{ObservabilityOptions.SectionName}:OtlpEndpoint 已迁到 {OtlpConnectionOptions.SectionName}，不再读取。"
            + $"把 '{retired}' 改写成 {OtlpConnectionOptions.SectionName}:Host / Port（拆字段写法），并删掉旧键。"
            + "仓库里的 yaml 和部署机上的 /opt/meeko/<服务>.yaml 都要改。");
    }
}
