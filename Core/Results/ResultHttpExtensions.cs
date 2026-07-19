using Microsoft.AspNetCore.Http;

namespace Platform.Common.Results;

/// <summary>
/// 把 Result&lt;T&gt; 适配成 ASP.NET Core 的 IResult（RFC 7807 ProblemDetails）。
/// 业务边界（Bff/Keystone）一律用这个，避免每个 endpoint 重复判别。
/// </summary>
public static class ResultHttpExtensions
{
    public static IResult ToHttp<T>(this Result<T> result)
        => result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : ToProblem(result.Error);

    public static IResult ToHttp(this Result result)
        => result.IsSuccess
            ? TypedResults.NoContent()
            : ToProblem(result.Error);

    public static IResult ToCreatedHttp<T>(this Result<T> result, string location)
        => result.IsSuccess
            ? TypedResults.Created(location, result.Value)
            : ToProblem(result.Error);

    private static IResult ToProblem(Error error)
    {
        var (status, title) = error.Code switch
        {
            ErrorCode.Validation       => (StatusCodes.Status400BadRequest, "Validation failed"),
            ErrorCode.Unauthorized     => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            ErrorCode.Forbidden        => (StatusCodes.Status403Forbidden, "Forbidden"),
            ErrorCode.NotFound         => (StatusCodes.Status404NotFound, "Not Found"),
            ErrorCode.Conflict         => (StatusCodes.Status409Conflict, "Conflict"),
            ErrorCode.TooManyRequests  => (StatusCodes.Status429TooManyRequests, "Too Many Requests"),
            ErrorCode.Upstream         => (StatusCodes.Status502BadGateway, "Upstream Error"),
            ErrorCode.DependencyDown   => (StatusCodes.Status503ServiceUnavailable, "Dependency Unavailable"),
            ErrorCode.Timeout          => (StatusCodes.Status504GatewayTimeout, "Timeout"),
            ErrorCode.NotImplemented   => (StatusCodes.Status501NotImplemented, "Not Implemented"),
            _                          => (StatusCodes.Status500InternalServerError, "Internal Server Error"),
        };

        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["code"] = error.Code,
        };

        if (error.Details is { Count: > 0 })
        {
            extensions["errors"] = error.Details;
        }

        if (!string.IsNullOrWhiteSpace(error.Reason))
        {
            extensions["reason"] = error.Reason;
        }

        return TypedResults.Problem(
            detail: error.Message,
            statusCode: status,
            title: title,
            type: $"https://meeko.dev/errors/{error.Code}",
            extensions: extensions);
    }
}
