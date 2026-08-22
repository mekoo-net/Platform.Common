namespace Platform.Common.Identity;

/// <summary>
/// 下游服务对当前请求身份的视图（Gateway 注入 header）。禁止再次校验 JWT。
/// </summary>
public interface ICurrentAuth
{
    bool IsAuthenticated { get; }
    ActorType ActorType { get; }

    /// <summary>Gateway <c>X-User-Id</c>：Account 域为 AccountUid，Staff 域为 StaffUid。</summary>
    long? PrincipalUid { get; }

    /// <summary>Gateway <c>X-Iam-Uid</c>（Account 域 IamUser 对外 UID）。</summary>
    long? IamUserUid { get; }

    string? TraceId { get; }
    IReadOnlyCollection<string> Roles { get; }
    string? StaffRole { get; }
    /// <summary>平台 API Key 开通的权限码（Gateway <c>X-Scopes</c>）。</summary>
    IReadOnlyCollection<string> Scopes { get; }
    string? TokenJti { get; }
    long? SessionId { get; }
    string? ClientIp { get; }

    bool IsInRole(string role);

    bool IsStaff => ActorType == ActorType.Staff;
}
