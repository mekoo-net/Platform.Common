namespace Platform.Common.Discovery;

/// <summary>Gateway → 下游服务身份 header 名（wire 常量，Platform / Meeko / Product 共用）。</summary>
public static class GatewayHeaders
{
    public const string UserId = "X-User-Id";
    public const string IamUid = "X-Iam-Uid";
    public const string Roles = "X-Roles";
    public const string TraceId = "X-Trace-Id";
    public const string Actor = "X-Actor";
    public const string StaffRole = "X-Staff-Role";
    public const string AccountUid = "X-Account-Uid";
    public const string AccountType = "X-Account-Type";
    public const string Role = "X-Role";
    public const string Scopes = "X-Scopes";
    public const string KeyUid = "X-Actor-Key-Uid";
    public const string StaffUid = "X-Staff-Uid";
    /// <summary>Access JWT 的 jti（注销 / 吊销）。</summary>
    public const string TokenJti = "X-Token-Jti";
    /// <summary>登录会话 id（JWT claim <c>sid</c>）。</summary>
    public const string SessionId = "X-Session-Id";
    /// <summary>Gateway 边缘解析后的真实客户端 IP（CDN 场景下由 CF-Connecting-IP 等归一化）。</summary>
    public const string ClientIp = "X-Client-Ip";
}
