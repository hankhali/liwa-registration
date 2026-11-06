using Liwa.Domain.Users;
using Liwa.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Liwa.UnitTests.Security;

public sealed class JwtTokenIssuerTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = "unit-test-signing-key-0123456789abcdef-0123",
        LifetimeMinutes = 60,
    };

    private static readonly DateTimeOffset Now = new(2027, 1, 10, 8, 0, 0, TimeSpan.Zero);

    private static User Student() =>
        new(Guid.NewGuid(), "student.new", "Mariam Al Mansoori", "mariam@example.test", UserRole.Student, "202700101", Now);

    [Fact]
    public async Task Token_validates_and_carries_role_and_subject()
    {
        var user = Student();
        var issuer = new JwtTokenIssuer(Microsoft.Extensions.Options.Options.Create(Options), new FakeTimeProvider(Now));

        var token = issuer.Issue(user);

        Assert.Equal(Now.AddMinutes(60), token.ExpiresAt);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, new TokenValidationParameters
        {
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            IssuerSigningKey = Options.CreateKey(),
            ValidateLifetime = false,
        });
        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal(user.Id.ToString(), result.Claims[LiwaClaims.Subject]);
        Assert.Equal("Student", result.Claims[LiwaClaims.Role]);
        Assert.Equal("student.new", result.Claims[LiwaClaims.Username]);
    }

    [Fact]
    public void Token_contains_no_email_or_university_id()
    {
        var issuer = new JwtTokenIssuer(Microsoft.Extensions.Options.Options.Create(Options), new FakeTimeProvider(Now));
        var jwt = new JsonWebToken(issuer.Issue(Student()).Value);

        Assert.DoesNotContain(jwt.Claims, c => c.Value.Contains("mariam@example.test", StringComparison.Ordinal));
        Assert.DoesNotContain(jwt.Claims, c => c.Value == "202700101");
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        var issuer = new JwtTokenIssuer(Microsoft.Extensions.Options.Options.Create(Options), new FakeTimeProvider(Now));
        var token = issuer.Issue(Student());

        var other = new JwtOptions { SigningKey = "a-completely-different-signing-key-9876543210" };
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, new TokenValidationParameters
        {
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            IssuerSigningKey = other.CreateKey(),
            ValidateLifetime = false,
        });
        Assert.False(result.IsValid);
    }
}
