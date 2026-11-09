using Liwa.Domain.Users;

namespace Liwa.Application.Abstractions;

public enum PasswordVerification
{
    Failed = 0,
    Success = 1,
    /// <summary>Correct password, but the stored hash uses outdated parameters and should be replaced.</summary>
    SuccessRehashNeeded = 2,
}

public interface IPasswordHasher
{
    string Hash(string password);

    PasswordVerification Verify(string passwordHash, string password);
}

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public interface ITokenIssuer
{
    AccessToken Issue(User user);
}

/// <summary>
/// Hands a finished SSO sign-in from the identity-provider redirect to the browser app without putting
/// the access token in a URL: the redirect carries a short-lived, single-use code instead.
/// </summary>
public interface ISignInHandoff
{
    /// <summary>Stores the sign-in and returns an unguessable code that can be redeemed once.</summary>
    string Create(Auth.SignInSuccess signIn);

    /// <summary>Returns the sign-in for a valid, unexpired code and invalidates it; otherwise null.</summary>
    Auth.SignInSuccess? Redeem(string code);
}

/// <summary>
/// The signed-in caller, independent of how they authenticated (local password today, SSO later).
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid UserId { get; }

    UserRole Role { get; }
}
