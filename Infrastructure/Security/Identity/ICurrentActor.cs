namespace Platform.Common.Identity;

/// <summary>
/// 调用者类型。
/// </summary>
public enum ActorType
{
    /// <summary>未识别 / 匿名。</summary>
    Anonymous = 0,
    /// <summary>IamUser（Personal / Org Owner / Org 子账号）。</summary>
    User = 1,
    /// <summary>API Key 调用，不映射到 IamUser。</summary>
    ApiKey = 2,
    /// <summary>平台员工（Staff 域）。</summary>
    Staff = 3,
}

/// <summary>
/// 下游业务服务对当前调用者的视图（基于 Gateway 注入的 header 构造）。
/// 故意 <b>不</b>暴露 IamUser.Uid。需要审计具体 IamUser 时由 Keystone 通过 Session 反查。
/// </summary>
public interface ICurrentActor
{
    bool IsAuthenticated { get; }
    ActorType ActorType { get; }

    /// <summary>Account.Uid，Account 域调用者必填。</summary>
    long? AccountUid { get; }

    /// <summary>personal / organization。</summary>
    string? AccountType { get; }

    /// <summary>角色名（仅 ActorType=User）。</summary>
    string? Role { get; }

    /// <summary>Scope 列表（仅 ActorType=ApiKey）。</summary>
    IReadOnlyCollection<string> Scopes { get; }

    /// <summary>ApiKey.Uid（仅 ActorType=ApiKey）。</summary>
    long? KeyUid { get; }

    /// <summary>StaffUser.Uid（仅 ActorType=Staff）。</summary>
    long? StaffUid { get; }

    /// <summary>superadmin / readonly（仅 ActorType=Staff）。</summary>
    string? StaffRole { get; }

    /// <summary>OTel trace id（透传）。</summary>
    string? TraceId { get; }
}
