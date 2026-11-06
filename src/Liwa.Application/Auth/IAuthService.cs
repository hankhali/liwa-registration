namespace Liwa.Application.Auth;

/// <summary>
/// Sign-in entry points. Local password sign-in exists today; an SSO method (exchanging a validated
/// identity-provider token for our access token) will be added here without changing other callers,
/// which only ever see <see cref="Abstractions.ICurrentUser"/>.
/// </summary>
public interface IAuthService
{
    Task<SignInResult> SignInWithPasswordAsync(string username, string password, CancellationToken ct);

    Task<UserProfile?> GetProfileAsync(Guid userId, CancellationToken ct);
}
