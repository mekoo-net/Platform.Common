using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Common.Identity;

namespace Platform.Common.Auth;

/// <summary>
/// Gateway 下游 BFF 等：信任 Gateway 注入的 identity header，不再验 JWT。
/// </summary>
public static class GatewayForwardedAuthDefaults
{
    public const string AuthenticationScheme = "GatewayForwarded";
}

public static class GatewayForwardedAuthExtensions
{
    /// <summary>
    /// 将 Gateway 注入的 <c>X-User-Id</c> / <c>X-Actor</c> 等 header 转为 <see cref="ClaimsPrincipal"/>，
    /// 供 <c>[Authorize]</c> 使用；业务代码仍应通过 <see cref="ICurrentAuth"/> 读取 header。
    /// </summary>
    public static IServiceCollection AddGatewayForwardedAuth(this IServiceCollection services)
    {
        services.AddAuthentication(GatewayForwardedAuthDefaults.AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, GatewayForwardedAuthenticationHandler>(
                GatewayForwardedAuthDefaults.AuthenticationScheme,
                _ => { });

        services.AddAuthorization(o =>
        {
            o.AddPolicy(PlatformAuthPolicies.AccountUser, p =>
            {
                p.RequireAuthenticatedUser();
                p.RequireClaim("actor", "user");
            });
        });
        return services;
    }
}

internal sealed class GatewayForwardedAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public GatewayForwardedAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers[CurrentAuthAccessor.UserIdHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new("sub", userId),
        };

        var actor = Request.Headers[CurrentAuthAccessor.ActorHeader].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(actor))
        {
            claims.Add(new Claim("actor", actor));
        }

        var staffRole = Request.Headers[CurrentAuthAccessor.StaffRoleHeader].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(staffRole))
        {
            claims.Add(new Claim("role", staffRole));
        }

        foreach (var role in Request.Headers[CurrentAuthAccessor.RolesHeader].FirstOrDefault()
                     ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 ?? [])
        {
            claims.Add(new Claim("role", role));
        }

        var staffUid = Request.Headers[CurrentActorAccessor.StaffUidHeader].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(staffUid))
        {
            claims.Add(new Claim("stuid", staffUid));
        }

        var tokenJti = Request.Headers[CurrentAuthAccessor.TokenJtiHeader].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(tokenJti))
        {
            claims.Add(new Claim("jti", tokenJti));
        }

        var sessionId = Request.Headers[CurrentAuthAccessor.SessionIdHeader].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            claims.Add(new Claim("sid", sessionId));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
