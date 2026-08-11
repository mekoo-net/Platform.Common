using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Platform.Common.Auth.InternalRpc;

/// <summary>
/// InternalRpc 端口的凭据校验。挂在端口上而不是单个服务上——那个端口整体就是管理面，
/// 逐个服务加注解迟早会漏掉新加的那个。
///
/// <para><b>未配置 key 时放行。</b> 见 <see cref="InternalRpcAuthOptions.HmacKeyBase64"/>：
/// 这是为了让服务端能先于调用方发布。放行时每次都记 warning，避免"忘了填"悄无声息地
/// 长期停在无鉴权状态。</para>
///
/// <para>失败返回的是 gRPC 的 <c>Unauthenticated</c>（HTTP 200 + status trailer），
/// 不是 HTTP 401——MagicOnion 客户端只认前者，返回 401 那边会解析失败而不是收到明确的拒绝。</para>
/// </summary>
public sealed class InternalRpcAuthMiddleware(
    RequestDelegate next,
    InternalRpcCredentialSigner signer,
    ILogger<InternalRpcAuthMiddleware> logger)
{
    /// <summary>中间件是单例，用它保证"未鉴权"警告整个进程只记一次。</summary>
    private static int _unauthenticatedWarned;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!signer.Enabled)
        {
            // 只警告一次。放行态下每次调用都记一条会把日志刷爆（Bff 打开日志页就是一串），
            // 而刷爆的直接后果是没人再看这条 warning——正好和它存在的目的相反。
            if (Interlocked.Exchange(ref _unauthenticatedWarned, 1) == 0)
            {
                logger.LogWarning(
                    "InternalRpc 未配置 {Path}:HmacKeyBase64，该端口当前**不鉴权**，全部放行。" +
                    "这是迁移态，上线前必须填。本条只记一次。",
                    InternalRpcAuthOptions.SectionName);
            }

            await next(context);
            return;
        }

        var clientId = context.Request.Headers[InternalRpcAuthOptions.ClientIdHeader].ToString();
        var clientSecret = context.Request.Headers[InternalRpcAuthOptions.ClientSecretHeader].ToString();

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            await WriteGrpcErrorAsync(context, "InternalRpc credentials are required.");
            return;
        }

        if (!signer.VerifySecret(clientId, clientSecret))
        {
            // 不回显 clientId 之外的任何东西，也不区分"id 不认识"和"签名不对"——
            // 区分开等于告诉调用方哪一半猜对了。
            logger.LogWarning("InternalRpc 凭据校验失败（clientId={ClientId}）", clientId);
            await WriteGrpcErrorAsync(context, "InternalRpc credentials are invalid.");
            return;
        }

        await next(context);
    }

    private static async Task WriteGrpcErrorAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/grpc";
        context.Response.AppendTrailer("grpc-status", ((int)StatusCode.Unauthenticated).ToString());
        context.Response.AppendTrailer("grpc-message", Uri.EscapeDataString(message));
        await context.Response.CompleteAsync();
    }
}
