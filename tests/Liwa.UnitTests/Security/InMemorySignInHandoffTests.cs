using Liwa.Application.Auth;
using Liwa.Infrastructure.Security;
using Microsoft.Extensions.Time.Testing;

namespace Liwa.UnitTests.Security;

public sealed class InMemorySignInHandoffTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2027, 1, 10, 8, 0, 0, TimeSpan.Zero));

    private static readonly SignInSuccess SignIn =
        new("token", DateTimeOffset.MaxValue, new UserProfile(Guid.NewGuid(), "student.new", "Mariam", "Student", null));

    [Fact]
    public void A_code_can_be_redeemed_exactly_once()
    {
        var handoff = new InMemorySignInHandoff(_clock);
        var code = handoff.Create(SignIn);

        Assert.Same(SignIn, handoff.Redeem(code));
        Assert.Null(handoff.Redeem(code));
    }

    [Fact]
    public void A_code_expires_after_60_seconds()
    {
        var handoff = new InMemorySignInHandoff(_clock);
        var code = handoff.Create(SignIn);

        _clock.Advance(InMemorySignInHandoff.Lifetime);

        Assert.Null(handoff.Redeem(code));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-real-code")]
    public void Unknown_codes_are_rejected(string code) =>
        Assert.Null(new InMemorySignInHandoff(_clock).Redeem(code));

    [Fact]
    public void Codes_are_long_random_and_url_safe()
    {
        var handoff = new InMemorySignInHandoff(_clock);
        var a = handoff.Create(SignIn);
        var b = handoff.Create(SignIn);

        Assert.NotEqual(a, b);
        Assert.True(a.Length >= 43); // 256 bits
        Assert.Matches("^[A-Za-z0-9_-]+$", a);
    }
}
