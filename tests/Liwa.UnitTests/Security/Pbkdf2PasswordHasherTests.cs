using Liwa.Application.Abstractions;
using Liwa.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace Liwa.UnitTests.Security;

public sealed class Pbkdf2PasswordHasherTests
{
    private static Pbkdf2PasswordHasher Hasher(int iterations = 10_000) =>
        new(Options.Create(new PasswordHashingOptions { Iterations = iterations }));

    [Fact]
    public void Correct_password_verifies()
    {
        var hasher = Hasher();
        var hash = hasher.Hash("Liwa#2027");
        Assert.Equal(PasswordVerification.Success, hasher.Verify(hash, "Liwa#2027"));
    }

    [Theory]
    [InlineData("liwa#2027")]
    [InlineData("Liwa#2027 ")]
    [InlineData("")]
    public void Wrong_password_fails(string attempt)
    {
        var hasher = Hasher();
        Assert.Equal(PasswordVerification.Failed, hasher.Verify(hasher.Hash("Liwa#2027"), attempt));
    }

    [Fact]
    public void Same_password_gets_a_different_salt_each_time()
    {
        var hasher = Hasher();
        Assert.NotEqual(hasher.Hash("Liwa#2027"), hasher.Hash("Liwa#2027"));
    }

    [Fact]
    public void Hash_records_scheme_and_iterations()
    {
        var parts = Hasher(12_345).Hash("x").Split('$');
        Assert.Equal(4, parts.Length);
        Assert.Equal("PBKDF2-SHA256", parts[0]);
        Assert.Equal("12345", parts[1]);
    }

    [Fact]
    public void Hash_made_with_fewer_iterations_than_current_policy_needs_rehash()
    {
        var old = Hasher(10_000).Hash("Liwa#2027");
        Assert.Equal(PasswordVerification.SuccessRehashNeeded, Hasher(20_000).Verify(old, "Liwa#2027"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("MD5$1000$abc$def")]
    [InlineData("PBKDF2-SHA256$abc$AAAA$AAAA")]
    [InlineData("PBKDF2-SHA256$1000$not base64!$AAAA")]
    [InlineData("PBKDF2-SHA256$-5$AAAA$AAAA")]
    public void Malformed_hash_fails_without_throwing(string stored)
    {
        Assert.Equal(PasswordVerification.Failed, Hasher().Verify(stored, "anything"));
    }

    [Fact]
    public void Tampered_hash_fails()
    {
        var hasher = Hasher();
        var parts = hasher.Hash("Liwa#2027").Split('$');
        var bytes = Convert.FromBase64String(parts[3]);
        bytes[0] ^= 0xFF;
        parts[3] = Convert.ToBase64String(bytes);
        Assert.Equal(PasswordVerification.Failed, hasher.Verify(string.Join('$', parts), "Liwa#2027"));
    }
}
