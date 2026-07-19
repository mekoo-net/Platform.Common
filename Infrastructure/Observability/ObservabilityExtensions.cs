using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
    /// </summary>
    public static IHostApplicationBuilder AddPlatformObservability(
        this IHostApplicationBuilder builder,
        Action<ObservabilityOptions>? configure = null)
    {
        var options = new ObservabilityOptions();
        builder.Configuration.GetSection(ObservabilityOptions.SectionName).Bind(options);
        configure?.Invoke(options);

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

            if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
            {
                tracing.AddOtlpExporter(o => o.Endpoint = new Uri(options.OtlpEndpoint));
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

            if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
            {
                metrics.AddOtlpExporter(o => o.Endpoint = new Uri(options.OtlpEndpoint));
            }
        });

        otel.WithLogging(
            configureBuilder: null,
            configureOptions: logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;

                if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
                {
                    logging.AddOtlpExporter(o => o.Endpoint = new Uri(options.OtlpEndpoint));
                }
            });

        return builder;
    }
}
