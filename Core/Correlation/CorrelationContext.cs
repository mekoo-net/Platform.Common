namespace Platform.Common.Correlation;

/// <summary>
/// 进程内访问当前请求的 Correlation Id（沿请求 / Activity 流转）。
/// </summary>
public static class CorrelationContext
{
    public const string HeaderName = "X-Correlation-Id";
    public const string TraceHeaderName = "X-Trace-Id";

    private static readonly AsyncLocal<string?> _current = new();

    public static string? Current
    {
        get => _current.Value;
        internal set => _current.Value = value;
    }
}
