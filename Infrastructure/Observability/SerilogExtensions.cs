using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;

namespace Platform.Common.Observability;

public static class SerilogExtensions
{
    /// <summary>
    /// 把 Serilog 接入 ASP.NET Core，主 sink 为 OTLP；可选 Console。
    /// 必须在 AddPlatformObservability 之后调用。
    /// <c>Logging:Console</c> 仅控制是否写 stdout，与 <c>Observability:OtlpEndpoint</c> 无关；默认 false。
    /// </summary>
    public static WebApplicationBuilder UsePlatformSerilog(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, lc) =>
        {
            var observability = services.GetRequiredService<ObservabilityOptions>();
            var configuration = context.Configuration;

            lc.ReadFrom.Configuration(configuration)
              .Enrich.FromLogContext()
              .Enrich.WithProperty("service.name", observability.ServiceName)
              .Filter.ByExcluding(IsKestrelClientDisconnectNoise);

            ApplyLoggingLogLevels(lc, configuration);

            var consoleEnabled = configuration.GetValue("Logging:Console", defaultValue: false);
            var otlpEnabled = !string.IsNullOrWhiteSpace(observability.OtlpEndpoint);

            // Console 先于 OTLP 注册，避免远端 sink 异常影响本地可见性。
            // 兜底：未显式开 Console 且没有配置 OTLP 时，仍写 stdout，避免日志进入黑洞、排障无据可查。
            if (consoleEnabled || !otlpEnabled)
            {
                lc.WriteTo.Console();
            }

            if (otlpEnabled)
            {
                lc.WriteTo.OpenTelemetry(otlp =>
                {
                    otlp.Endpoint = observability.OtlpEndpoint;
                    otlp.Protocol = OtlpProtocol.Grpc;
                    otlp.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = observability.ServiceName,
                        ["host.name"] = Environment.MachineName,
                    };
                });
            }
        });

        return builder;
    }

    private static void ApplyLoggingLogLevels(LoggerConfiguration lc, IConfiguration configuration)
    {
        var section = configuration.GetSection("Logging:LogLevel");
        var defaultLevel = ParseLogLevel(section["Default"]) ?? LogEventLevel.Information;
        lc.MinimumLevel.Is(defaultLevel);

        if (section.Exists())
        {
            foreach (var child in section.GetChildren())
            {
                if (child.Key.Equals("Default", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var level = ParseLogLevel(child.Value);
                if (level.HasValue)
                {
                    lc.MinimumLevel.Override(child.Key, level.Value);
                }
            }
        }

        if (section["Microsoft.EntityFrameworkCore.Database.Command"] is null)
        {
            lc.MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning);
        }

        if (section["System.Net.Http.HttpClient"] is null)
        {
            lc.MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning);
        }
    }

    private static LogEventLevel? ParseLogLevel(string? value) =>
        Enum.TryParse<LogEventLevel>(value, ignoreCase: true, out var level) ? level : null;

    /// <summary>
    /// 判定是否为 Kestrel 连接层因「客户端中途断连」产生的无害异常日志。
    /// 典型场景：浏览器/反代/LB 在 TLS 会话正常关闭前 RST 连接，底层 OpenSSL 报
    /// SSL_ERROR_SYSCALL、broken pipe、connection reset 等。这类事件与具体请求逻辑无关，
    /// 不代表服务故障，仅是噪音，故从日志中剔除。注意：仅对 Kestrel 来源生效，避免误伤业务异常。
    /// </summary>
    private static bool IsKestrelClientDisconnectNoise(LogEvent logEvent)
    {
        if (logEvent.Exception is null)
        {
            return false;
        }

        if (!logEvent.Properties.TryGetValue("SourceContext", out var sourceContext)
            || sourceContext is not ScalarValue { Value: string source }
            || !source.StartsWith("Microsoft.AspNetCore.Server.Kestrel", StringComparison.Ordinal))
        {
            return false;
        }

        return IsClientDisconnectException(logEvent.Exception);
    }

    private static bool IsClientDisconnectException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var typeName = current.GetType().Name;

            // 连接被对端重置 / 取消是最常见的断连信号。
            if (typeName is "ConnectionResetException" or "ConnectionAbortedException"
                or "OperationCanceledException" or "TaskCanceledException")
            {
                return true;
            }

            // OpenSSL / socket 层的系统调用失败与管道断裂（Linux 上的典型表现）。
            var message = current.Message;
            if (message.Contains("SSL_ERROR_SYSCALL", StringComparison.Ordinal)
                || message.Contains("The encryption operation failed", StringComparison.Ordinal)
                || message.Contains("Broken pipe", StringComparison.OrdinalIgnoreCase)
                || message.Contains("connection reset", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Connection reset by peer", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
