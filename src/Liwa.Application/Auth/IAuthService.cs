namespace Liwa.Application.Auth;

/// <summary>
/// Sign-in entry points: local password, and university SSO (a validated identity-provider identity
/// exchanged for our access token). Other callers only ever see <see cref="Abstractions.ICurrentUser"/>.
/// </summary>
public interface IAuthService
{
    Task<SignInResult> SignInWithPasswordAsync(string username, string password, CancellationToken ct);

    /// <summary>
    /// Signs in someone the identity provider has already authenticated. Only people who already have an
    /// account (from registrar data) get in: matched first by linked subject, then by university email.
    /// </summary>
    Task<ExternalSignInResult> SignInWithExternalIdentityAsync(ExternalIdentity identity, CancellationToken ct);

    Task<UserProfile?> GetProfileAsync(Guid userId, CancellationToken ct);
}
