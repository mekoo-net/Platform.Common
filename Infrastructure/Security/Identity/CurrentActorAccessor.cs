using Microsoft.AspNetCore.Http;
using Platform.Common.Discovery;

namespace Platform.Common.Identity;

/// <summary>
/// 从 Gateway 注入的 header 解析当前调用者。
/// </summary>
public sealed class CurrentActorAccessor : ICurrentActor
{
    public const string AccountUidHeader  = GatewayHeaders.AccountUid;
    public const string AccountTypeHeader = GatewayHeaders.AccountType;
    public const string ActorHeader       = GatewayHeaders.Actor;
    public const string RoleHeader        = GatewayHeaders.Role;
    public const string ScopesHeader      = GatewayHeaders.Scopes;
    public const string KeyUidHeader      = GatewayHeaders.KeyUid;
    public const string StaffUidHeader    = GatewayHeaders.StaffUid;
    public const string StaffRoleHeader   = GatewayHeaders.StaffRole;
    public const string TraceIdHeader     = GatewayHeaders.TraceId;

    private readonly IHttpContextAccessor _accessor;

    public CurrentActorAccessor(IHttpContextAccessor accessor) => _accessor = accessor;

    private HttpContext? Ctx => _accessor.HttpContext;

    public bool IsAuthenticated => ActorType != ActorType.Anonymous;

    public ActorType ActorType
    {
        get
        {
            var raw = Ctx?.Request.Headers[ActorHeader].FirstOrDefault();
            return raw switch
            {
                "user"   => ActorType.User,
                "apikey" => ActorType.ApiKey,
                "staff"  => ActorType.Staff,
                _        => ActorType.Anonymous,
            };
        }
    }

    public long? AccountUid
        => long.TryParse(Ctx?.Request.Headers[AccountUidHeader].FirstOrDefault(), out var v) ? v : null;

    public string? AccountType
        => Ctx?.Request.Headers[AccountTypeHeader].FirstOrDefault();

    public string? Role
        => Ctx?.Request.Headers[RoleHeader].FirstOrDefault();

    public IReadOnlyCollection<string> Scopes
    {
        get
        {
            var raw = Ctx?.Request.Headers[ScopesHeader].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Array.Empty<string>();
            }
            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }

    public long? KeyUid
        => long.TryParse(Ctx?.Request.Headers[KeyUidHeader].FirstOrDefault(), out var v) ? v : null;

    public long? StaffUid
        => long.TryParse(Ctx?.Request.Headers[StaffUidHeader].FirstOrDefault(), out var v) ? v : null;

    public string? StaffRole
        => Ctx?.Request.Headers[StaffRoleHeader].FirstOrDefault();

    public string? TraceId
        => Ctx?.Request.Headers[TraceIdHeader].FirstOrDefault();
}
