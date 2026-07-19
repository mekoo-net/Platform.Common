namespace Platform.Common.Results;

public sealed record Error(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? Details = null,
    string? Reason = null)
{
    public static Error Unknown(string message = "Unknown error.", string? reason = null)
        => new(ErrorCode.Unknown, message, Reason: reason);

    public static Error Validation(
        string message,
        IReadOnlyDictionary<string, string[]>? details = null,
        string? reason = null)
        => new(ErrorCode.Validation, message, details, reason);

    public static Error NotFound(string message = "Resource not found.", string? reason = null)
        => new(ErrorCode.NotFound, message, Reason: reason);

    public static Error Conflict(string message, string? reason = null)
        => new(ErrorCode.Conflict, message, Reason: reason);

    public static Error Unauthorized(string message = "Authentication required.", string? reason = null)
        => new(ErrorCode.Unauthorized, message, Reason: reason);

    public static Error Forbidden(string message = "Access denied.", string? reason = null)
        => new(ErrorCode.Forbidden, message, Reason: reason);

    public static Error TooManyRequests(string message = "Rate limit exceeded.", string? reason = null)
        => new(ErrorCode.TooManyRequests, message, Reason: reason);

    public static Error Upstream(string message, string? reason = null)
        => new(ErrorCode.Upstream, message, Reason: reason);

    public static Error DependencyDown(string message, string? reason = null)
        => new(ErrorCode.DependencyDown, message, Reason: reason);

    public static Error Timeout(string message = "Operation timed out.", string? reason = null)
        => new(ErrorCode.Timeout, message, Reason: reason);

    public static Error NotImplemented(
        string message = "Endpoint scaffolded but not implemented yet.",
        string? reason = null)
        => new(ErrorCode.NotImplemented, message, Reason: reason);
}
