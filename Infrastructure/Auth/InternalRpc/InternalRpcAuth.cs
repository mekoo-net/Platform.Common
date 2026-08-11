using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Platform.Common.Auth.InternalRpc;

/// <summary>
/// InternalRpc 面（平台内部服务互调，MagicOnion）的凭据配置。
///
/// <para><b>为什么不和 BackendRpc 共用一把 key。</b> BackendRpc 的凭据是签发给**外部运行时**的
/// （独立部署的网关回调进来），而 InternalRpc 是平台管理面——上面挂着账单查询、供应商管理
/// 这些东西。两边共用密钥等于让任何持有 BackendRpc 凭据的外部进程都能调管理面。
/// 算法一样、信任域不同，所以是两把 key、两套 header。</para>
/// </summary>
public sealed class InternalRpcAuthOptions
{
    /// <summary>配置路径，供各服务 yaml 对齐。</summary>
    public const string SectionName = "InternalRpc:Auth";

    public const string ClientIdHeader = "x-meeko-internal-client-id";
    public const string ClientSecretHeader = "x-meeko-internal-client-secret";

    /// <summary>
    /// 验签用的 HMAC key（Base64，至少 16 字节）。
    ///
    /// <para><b>留空 = 不校验，只记一条 warning。</b> 这是故意的：服务端和所有调用方不可能
    /// 同时发布，先发服务端会把 Bff / Jobs / Observability 全打断。留空放行让服务端可以先上，
    /// 各调用方陆续配好之后再统一把 key 填上，enforcement 才真正生效。
    /// <b>上线后必须填</b>——空值是迁移态，不是终态。</para>
    /// </summary>
    public string HmacKeyBase64 { get; set; } = "";

    /// <summary>
    /// 本服务作为**调用方**时用的 id（如 <c>bff</c> / <c>jobs</c> / <c>observability</c>）。
    /// secret 由它和 key 现算，所以新增一个调用方不需要改任何服务端配置。
    /// </summary>
    public string ClientId { get; set; } = "";

    public bool Enforced => !string.IsNullOrWhiteSpace(HmacKeyBase64);
}

/// <summary>
/// 无状态凭据：<c>cs-{nonce}.{signature}</c>，signature = HMACSHA256(key, "{clientId}.{nonce}")。
/// 格式与 <c>Auth/BackendRpc</c> 那套一致（同一个已验证过的形态），但 key 与 header 分开，
/// 理由见 <see cref="InternalRpcAuthOptions"/>。
/// </summary>
public sealed class InternalRpcCredentialSigner
{
    private readonly byte[]? _key;

    public InternalRpcCredentialSigner(IOptions<InternalRpcAuthOptions> options)
    {
        var raw = options.Value.HmacKeyBase64;
        if (string.IsNullOrWhiteSpace(raw)) return;

        _key = Convert.FromBase64String(raw);
        if (_key.Length < 16)
            throw new InvalidOperationException(
                $"{InternalRpcAuthOptions.SectionName}:HmacKeyBase64 must decode to at least 16 bytes.");
    }

    public bool Enabled => _key is not null;

    public string IssueSecret(string clientId)
    {
        if (_key is null) throw new InvalidOperationException("InternalRpc HMAC key is not configured.");
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("clientId required", nameof(clientId));

        var nonce = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return $"cs-{nonce}.{Sign(clientId, nonce)}";
    }

    public bool VerifySecret(string clientId, string clientSecret)
    {
        if (_key is null) return false;
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret)) return false;
        if (!clientSecret.StartsWith("cs-", StringComparison.Ordinal)) return false;

        var body = clientSecret[3..];
        var separator = body.IndexOf('.');
        if (separator <= 0 || separator == body.Length - 1) return false;

        var expected = Encoding.ASCII.GetBytes(Sign(clientId, body[..separator]));
        var actual = Encoding.ASCII.GetBytes(body[(separator + 1)..]);
        // 定长比较：签名校验不能因为长度或前缀提前返回，那会泄漏时序信息。
        return expected.Length == actual.Length
               && CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private string Sign(string clientId, string nonce)
        => Base64UrlEncode(HMACSHA256.HashData(_key!, Encoding.UTF8.GetBytes($"{clientId}.{nonce}")));

    private static string Base64UrlEncode(byte[] data)
        => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
