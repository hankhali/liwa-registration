using Liwa.Application.Abstractions;
using Liwa.Domain.Users;
using Microsoft.Extensions.Logging;

namespace Liwa.Application.Auth;

public sealed class AuthService(
    IUserRepository users,
    IPasswordHasher hasher,
    ITokenIssuer tokens,
    TimeProvider clock,
    ILogger<AuthService> logger) : IAuthService
{
    // Verifying against a throwaway hash when the user doesn't exist keeps response time similar,
    // so timing doesn't reveal which usernames are real.
    private readonly Lazy<string> _dummyHash = new(() => hasher.Hash(Guid.NewGuid().ToString("N")));

    public async Task<SignInResult> SignInWithPasswordAsync(string username, string password, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return SignInResult.Failed;

        var user = await users.FindByUsernameAsync(User.NormalizeUsername(username), ct);

        if (user is null || user.PasswordHash is null)
        {
            hasher.Verify(_dummyHash.Value, password);
            logger.LogInformation("Sign-in failed: unknown user or no local password.");
            return SignInResult.Failed;
        }

        var verification = hasher.Verify(user.PasswordHash, password);
        if (verification == PasswordVerification.Failed || !user.IsActive)
        {
            logger.LogInformation("Sign-in failed for user {UserId}.", user.Id);
            return SignInResult.Failed;
        }

        if (verification == PasswordVerification.SuccessRehashNeeded)
            user.SetPasswordHash(hasher.Hash(password));

        user.RecordSignIn(clock.GetUtcNow());
        await users.SaveChangesAsync(ct);

        var token = tokens.Issue(user);
        logger.LogInformation("User {UserId} signed in.", user.Id);
        return new SignInResult(new SignInSuccess(token.Value, token.ExpiresAt, UserProfile.From(user)));
    }

    public async Task<UserProfile?> GetProfileAsync(Guid userId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId, ct);
        return user is { IsActive: true } ? UserProfile.From(user) : null;
    }
}
