using Grpc.Core;
using MagicOnion.Client;
using Microsoft.Extensions.Options;

namespace Platform.Common.Auth.InternalRpc;

/// <summary>
/// 调用方侧：给每次 InternalRpc 调用带上凭据。
///
/// <para>每次调用现签一个新 secret（nonce 随机），而不是启动时签一次长期复用——
/// 成本是一次 HMAC，换来的是凭据不会在日志 / 抓包里长期有效。</para>
///
/// <para>未配置 key 或 ClientId 时什么都不加：与服务端的放行逻辑配对，让迁移期
/// 两边可以分别发布。</para>
/// </summary>
public sealed class InternalRpcCredentialFilter(
    InternalRpcCredentialSigner signer,
    IOptions<InternalRpcAuthOptions> options) : IClientFilter
{
    public async ValueTask<ResponseContext> SendAsync(
        RequestContext context,
        Func<RequestContext, ValueTask<ResponseContext>> next)
    {
        var clientId = options.Value.ClientId;
        if (signer.Enabled && !string.IsNullOrWhiteSpace(clientId))
        {
            var headers = context.CallOptions.Headers;
            if (headers is not null
                && !headers.Any(e => string.Equals(
                    e.Key, InternalRpcAuthOptions.ClientIdHeader, StringComparison.OrdinalIgnoreCase)))
            {
                headers.Add(InternalRpcAuthOptions.ClientIdHeader, clientId);
                headers.Add(InternalRpcAuthOptions.ClientSecretHeader, signer.IssueSecret(clientId));
            }
        }

        return await next(context);
    }
}
