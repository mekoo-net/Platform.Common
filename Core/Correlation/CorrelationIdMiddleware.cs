using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Platform.Common.Correlation;

/// <summary>
/// 读取或生成 X-Correlation-Id，写入响应、AsyncLocal、Activity baggage、Serilog LogContext。
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(CorrelationContext.HeaderName, out var values)
                            && !string.IsNullOrWhiteSpace(values.ToString())
            ? values.ToString()
            : Guid.NewGuid().ToString("N");

        CorrelationContext.Current = correlationId;
        context.Request.Headers[CorrelationContext.HeaderName] = correlationId;
        context.Response.Headers[CorrelationContext.HeaderName] = correlationId;

        Activity.Current?.SetBaggage("meeko.correlation_id", correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TraceId", Activity.Current?.TraceId.ToString()))
        {
            await _next(context);
        }
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();
}
