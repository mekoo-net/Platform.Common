using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace Platform.Common.Web;

/// <summary>
/// 前端 docs 单层信封 <c>{ success, data, message? }</c>。
/// 凡是面向前端 console 的 REST 端点（Demux `/demux/api/*`、Keystone `/api/user/*` 等）
/// 都用这种 shape，避免前端 <c>apiCall</c> 还要兼容 RFC 7807 ProblemDetails 分支。
/// HTTP 状态码默认 200（除非真正传输层异常），失败语义由 <c>success=false + message</c> 表达。
/// <para>
/// <c>message</c> 只在失败时承载错误文案；成功时为 <c>null</c>，并通过
/// <see cref="JsonIgnoreCondition.WhenWritingNull"/> 从 wire 中省略，避免下发无意义的空串。
/// </para>
/// </summary>
public sealed record ApiEnvelope<T>(
    bool Success,
    T? Data,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Message = null);

public static class ApiEnvelope
{
    // 注意：这里要写完整的 Microsoft.AspNetCore.Http.Results 限定，否则 C# 名称解析会被
    // 同程序集里相邻的 Platform.Common.Results 命名空间会遮蔽 Microsoft.AspNetCore.Http.Results。
    public static IResult Ok<T>(T data, string? message = null) =>
        Microsoft.AspNetCore.Http.Results.Ok(new ApiEnvelope<T>(true, data, message));

    public static IResult Failure<T>(string message, T? data = default) =>
        Microsoft.AspNetCore.Http.Results.Ok(new ApiEnvelope<T>(false, data, message));

    public static IResult Failure(string message) =>
        Microsoft.AspNetCore.Http.Results.Ok(new ApiEnvelope<object?>(false, null, message));

    public static IResult NotImplemented(string operation) =>
        Failure($"{operation} is scaffolded but not implemented yet.");

    public static IResult NotImplemented<T>(string operation, T? data = default) =>
        Failure<T>($"{operation} is scaffolded but not implemented yet.", data);
}
