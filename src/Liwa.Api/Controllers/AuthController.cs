using System.ComponentModel.DataAnnotations;
using Liwa.Api.Infrastructure;
using Liwa.Application.Abstractions;
using Liwa.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Liwa.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService auth, ICurrentUser currentUser, IOptions<SsoOptions> sso) : ControllerBase
{
    public sealed record SignInRequest([Required, MaxLength(100)] string Username, [Required, MaxLength(200)] string Password);

    /// <summary>Sign in with a local account. Returns a bearer token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.SignIn)]
    [ProducesResponseType<SignInSuccess>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(SignInRequest request, CancellationToken ct)
    {
        // With SSO on, username/password can be switched off entirely (Sso:LocalSignInEnabled=false).
        if (!sso.Value.LocalSignInAllowed) return NotFound();

        var result = await auth.SignInWithPasswordAsync(request.Username, request.Password, ct);
        if (!result.Succeeded)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Username or password is incorrect.");
        return Ok(result.Success);
    }

    /// <summary>The signed-in user's profile.</summary>
    [HttpGet("me")]
    [ProducesResponseType<UserProfile>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var profile = await auth.GetProfileAsync(currentUser.UserId, ct);
        // A valid token for a deactivated/removed account is treated as signed out.
        return profile is null ? Unauthorized() : Ok(profile);
    }
}
