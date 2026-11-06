using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using Liwa.Application.Abstractions;
using Liwa.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Liwa.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    [Required] public string Issuer { get; set; } = "liwa-registration";

    [Required] public string Audience { get; set; } = "liwa-registration";

    /// <summary>HMAC-SHA256 key, at least 32 bytes. Must come from secrets/environment outside Development.</summary>
    [Required, MinLength(32)] public string SigningKey { get; set; } = string.Empty;

    [Range(5, 24 * 60)] public int LifetimeMinutes { get; set; } = 480;

    public SymmetricSecurityKey CreateKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}

public static class LiwaClaims
{
    public const string Subject = JwtRegisteredClaimNames.Sub;
    public const string Name = "name";
    public const string Role = "role";
    public const string Username = "preferred_username";
}

public sealed class JwtTokenIssuer(IOptions<JwtOptions> options, TimeProvider clock) : ITokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Issue(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var o = options.Value;
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(o.LifetimeMinutes);

        // Only identifiers needed for authorization go in the token: no email or university ID.
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = o.Issuer,
            Audience = o.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(LiwaClaims.Subject, user.Id.ToString()),
                new Claim(LiwaClaims.Username, user.Username),
                new Claim(LiwaClaims.Name, user.DisplayName),
                new Claim(LiwaClaims.Role, RoleNames.Of(user.Role)),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            ]),
            SigningCredentials = new SigningCredentials(o.CreateKey(), SecurityAlgorithms.HmacSha256),
        };

        return new AccessToken(_handler.CreateToken(descriptor), expires);
    }
}
