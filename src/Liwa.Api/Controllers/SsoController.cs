using System.ComponentModel.DataAnnotations;
using Liwa.Api.Infrastructure;
using Liwa.Application.Abstractions;
using Liwa.Application.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Liwa.Api.Controllers;

/// <summary>
/// "Sign in with your Liwa account" (Microsoft Entra ID). Flow:
/// start → Microsoft sign-in → callback (handled by the OpenID Connect middleware) → complete → the app
/// redeems a one-time code at exchange for our usual access token. Microsoft's tokens are never kept,
/// and our token never appears in a URL.
/// </summary>
[ApiController]
[Route("api/auth/sso")]
[AllowAnonymous]
public sealed class SsoController(
    IOptions<SsoOptions> options,
    IAuthService auth,
    ISignInHandoff handoff,
    ILogger<SsoController> logger) : ControllerBase
{
    private SsoOptions Sso => options.Value;

    public sealed record SsoConfig(bool Enabled, bool LocalSignInEnabled);

    public sealed record ExchangeRequest([Required, MaxLength(100)] string Code);

    /// <summary>Which sign-in options the sign-in page should show.</summary>
    [HttpGet("config")]
    public SsoConfig Config() => new(Sso.Enabled, Sso.LocalSignInAllowed);

    /// <summary>Sends the browser to the university Microsoft sign-in page.</summary>
    [HttpGet("start")]
    public async Task<IActionResult> Start([FromServices] IOptionsMonitor<OpenIdConnectOptions> oidc, CancellationToken ct)
    {
        if (!Sso.Enabled) return NotFound();

        // Check Microsoft's sign-in settings first (cached after the first call). If they can't be loaded
        // (wrong tenant ID, network down), people get a plain message instead of an error page.
        try
        {
            var manager = oidc.Get(SsoSchemes.Entra).ConfigurationManager;
            if (manager is not null) await manager.GetConfigurationAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "SSO is unavailable: could not load the identity provider's configuration.");
            return Redirect("/login?sso=unavailable");
        }

        return Challenge(new AuthenticationProperties { RedirectUri = SsoSchemes.CompletePath }, SsoSchemes.Entra);
    }

    /// <summary>Back from Microsoft: find the person's account and hand the sign-in to the app.</summary>
    [HttpGet("complete")]
    public async Task<IActionResult> Complete(CancellationToken ct)
    {
        if (!Sso.Enabled) return NotFound();

        var result = await HttpContext.AuthenticateAsync(SsoSchemes.Cookie);
        await HttpContext.SignOutAsync(SsoSchemes.Cookie);
        if (!result.Succeeded || result.Principal is null) return Redirect("/login?sso=failed");

        string? Claim(string type) => result.Principal.FindFirst(type)?.Value;
        var tenantId = Claim("tid");
        var objectId = Claim("oid");

        // Defence in depth: the token was already validated against Liwa's tenant by the middleware.
        if (tenantId is null || objectId is null || !string.Equals(tenantId, Sso.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("SSO sign-in refused: token is missing tid/oid or is from another tenant.");
            return Redirect("/login?sso=failed");
        }

        var identity = new ExternalIdentity(
            Provider: "entra",
            TenantId: tenantId.ToLowerInvariant(),
            ObjectId: objectId.ToLowerInvariant(),
            Email: Claim("email"),
            PreferredUsername: Claim("preferred_username"),
            DisplayName: Claim("name"));

        var signIn = await auth.SignInWithExternalIdentityAsync(identity, ct);
        if (!signIn.Succeeded)
        {
            var reason = signIn.Failure == ExternalSignInFailure.Inactive ? "inactive" : "not_registered";
            return Redirect($"/login?sso={reason}");
        }

        return Redirect($"/login?sso_code={Uri.EscapeDataString(handoff.Create(signIn.Success!))}");
    }

    /// <summary>Trades the one-time code from <see cref="Complete"/> for an access token. Valid once, for 60 seconds.</summary>
    [HttpPost("exchange")]
    [EnableRateLimiting(RateLimits.SignIn)]
    [ProducesResponseType<SignInSuccess>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public IActionResult Exchange(ExchangeRequest request)
    {
        var signIn = handoff.Redeem(request.Code);
        return signIn is null
            ? Problem(statusCode: StatusCodes.Status400BadRequest, title: "This sign-in has expired. Please sign in again.")
            : Ok(signIn);
    }
}
