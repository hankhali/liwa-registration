using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Liwa.Api.Infrastructure;

/// <summary>
/// "Sign in with your Liwa account": Microsoft Entra ID, the same university account used for the LMS
/// (Blackboard "LU-AZURE") and email. Off until university IT registers the app (see docs/sso.md).
/// </summary>
public sealed class SsoOptions
{
    public const string Section = "Sso";

    public bool Enabled { get; set; }

    /// <summary>Entra sign-in host. Change only for sovereign clouds.</summary>
    public string Instance { get; set; } = "https://login.microsoftonline.com/";

    /// <summary>Liwa's Entra tenant (directory) ID, a GUID. Single-tenant: other organisations can't sign in.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Application (client) ID of the app registration.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client secret of the app registration. Secret: supply via environment, never commit.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Keep username/password sign-in available next to SSO (demo accounts, break-glass admin).</summary>
    public bool LocalSignInEnabled { get; set; } = true;

    /// <summary>Local sign-in is always available while SSO is off.</summary>
    public bool LocalSignInAllowed => !Enabled || LocalSignInEnabled;
}

public static class SsoSchemes
{
    /// <summary>Short-lived cookie that holds the identity between Microsoft's redirect and our token handoff.</summary>
    public const string Cookie = "sso";

    public const string Entra = "entra";

    public const string BasePath = "/api/auth/sso";

    /// <summary>Redirect URI to register in Entra: https://&lt;site&gt;/api/auth/sso/callback</summary>
    public const string CallbackPath = BasePath + "/callback";

    public const string CompletePath = BasePath + "/complete";
}

public static class SsoSetup
{
    public static AuthenticationBuilder AddLiwaSso(this AuthenticationBuilder auth, IConfiguration config)
    {
        auth.Services.Configure<SsoOptions>(config.GetSection(SsoOptions.Section));

        var o = config.GetSection(SsoOptions.Section).Get<SsoOptions>() ?? new SsoOptions();
        if (!o.Enabled) return auth;

        Validate(o);

        auth.AddCookie(SsoSchemes.Cookie, c =>
        {
            c.Cookie.Name = "liwa.sso";
            c.Cookie.Path = SsoSchemes.BasePath;
            c.Cookie.HttpOnly = true;
            c.Cookie.SameSite = SameSiteMode.Lax;
            c.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            c.ExpireTimeSpan = TimeSpan.FromMinutes(5);
            c.SlidingExpiration = false;
            // This cookie only carries the identity to /complete; it never guards an API endpoint.
            c.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        });

        auth.AddOpenIdConnect(SsoSchemes.Entra, oidc =>
        {
            oidc.Authority = $"{o.Instance.TrimEnd('/')}/{o.TenantId}/v2.0";
            oidc.ClientId = o.ClientId;
            oidc.ClientSecret = o.ClientSecret;
            oidc.ResponseType = OpenIdConnectResponseType.Code;
            // Query (not form_post) so the correlation/nonce cookies can stay SameSite=Lax.
            oidc.ResponseMode = OpenIdConnectResponseMode.Query;
            oidc.UsePkce = true;
            oidc.CallbackPath = SsoSchemes.CallbackPath;
            oidc.SignInScheme = SsoSchemes.Cookie;
            oidc.Scope.Clear();
            oidc.Scope.Add("openid");
            oidc.Scope.Add("profile");
            oidc.Scope.Add("email");
            oidc.MapInboundClaims = false;
            oidc.SaveTokens = false; // we never keep Microsoft's tokens
            oidc.GetClaimsFromUserInfoEndpoint = false;
            oidc.TokenValidationParameters.NameClaimType = "name";
            oidc.CorrelationCookie.SameSite = SameSiteMode.Lax;
            oidc.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            oidc.NonceCookie.SameSite = SameSiteMode.Lax;
            oidc.NonceCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            oidc.Events.OnRedirectToIdentityProvider = ctx =>
            {
                // Shared campus computers: always let people choose which account to use.
                ctx.ProtocolMessage.Prompt = "select_account";
                return Task.CompletedTask;
            };
            oidc.Events.OnRemoteFailure = ctx =>
            {
                // Cancelled, denied consent, expired correlation, etc.: back to sign-in with a plain message.
                ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Liwa.Sso").LogWarning(ctx.Failure, "SSO sign-in failed at the identity provider step.");
                ctx.Response.Redirect("/login?sso=failed");
                ctx.HandleResponse();
                return Task.CompletedTask;
            };
        });

        return auth;
    }

    private static void Validate(SsoOptions o)
    {
        var problems = new List<string>();
        if (!Guid.TryParse(o.TenantId, out _)) problems.Add("Sso:TenantId must be Liwa's directory (tenant) ID, a GUID.");
        if (!Guid.TryParse(o.ClientId, out _)) problems.Add("Sso:ClientId must be the app registration's client ID, a GUID.");
        if (string.IsNullOrWhiteSpace(o.ClientSecret)) problems.Add("Sso:ClientSecret is required.");
        if (problems.Count > 0)
            throw new InvalidOperationException("SSO is enabled but not configured: " + string.Join(" ", problems) + " See docs/sso.md.");
    }
}
