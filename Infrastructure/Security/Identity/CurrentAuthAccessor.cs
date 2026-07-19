using Microsoft.AspNetCore.Http;
using Platform.Common.Discovery;

namespace Platform.Common.Identity;

public sealed class CurrentAuthAccessor : ICurrentAuth
{
    public const string UserIdHeader     = GatewayHeaders.UserId;
    public const string IamUidHeader     = GatewayHeaders.IamUid;
    public const string RolesHeader      = GatewayHeaders.Roles;
    public const string TraceIdHeader    = GatewayHeaders.TraceId;
    public const string ActorHeader      = GatewayHeaders.Actor;
    public const string StaffRoleHeader  = GatewayHeaders.StaffRole;
    public const string TokenJtiHeader   = GatewayHeaders.TokenJti;
    public const string SessionIdHeader  = GatewayHeaders.SessionId;
    public const string ClientIpHeader   = GatewayHeaders.ClientIp;

    private readonly IHttpContextAccessor _accessor;

    public CurrentAuthAccessor(IHttpContextAccessor accessor) => _accessor = accessor;

    private HttpContext? Ctx => _accessor.HttpContext;

    public bool IsAuthenticated => PrincipalUid is > 0;

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

    public long? PrincipalUid
        => long.TryParse(Ctx?.Request.Headers[UserIdHeader].FirstOrDefault(), out var v) && v > 0 ? v : null;

    public long? IamUserUid
        => long.TryParse(Ctx?.Request.Headers[IamUidHeader].FirstOrDefault(), out var v) && v > 0 ? v : null;

    public string? TraceId    => Ctx?.Request.Headers[TraceIdHeader].FirstOrDefault();
    public string? StaffRole => Ctx?.Request.Headers[StaffRoleHeader].FirstOrDefault();
    public string? TokenJti  => Ctx?.Request.Headers[TokenJtiHeader].FirstOrDefault();

    public long? SessionId
        => long.TryParse(Ctx?.Request.Headers[SessionIdHeader].FirstOrDefault(), out var v) && v > 0 ? v : null;

    public string? ClientIp => Ctx?.Request.Headers[ClientIpHeader].FirstOrDefault();

    public IReadOnlyCollection<string> Roles
    {
        get
        {
            var raw = Ctx?.Request.Headers[RolesHeader].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Array.Empty<string>();
            }

            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }

    public bool IsInRole(string role)
        => Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
}
