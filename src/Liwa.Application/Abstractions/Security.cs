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
/// The signed-in caller, independent of how they authenticated (local password today, SSO later).
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid UserId { get; }

    UserRole Role { get; }
}
