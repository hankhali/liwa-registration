namespace Liwa.Application.Auth;

/// <summary>
/// A person already authenticated by the university identity provider (Microsoft Entra ID).
/// Built only from a validated ID token; never from anything the browser sends directly.
/// </summary>
public sealed record ExternalIdentity(
    string Provider,
    string TenantId,
    string ObjectId,
    string? Email,
    string? PreferredUsername,
    string? DisplayName)
{
    /// <summary>Stable, globally unique key: provider + tenant + immutable object id.</summary>
    public string Subject => $"{Provider}:{TenantId}:{ObjectId}";

    /// <summary>Email-like values to match against registrar records, lower-cased, without duplicates.</summary>
    public IEnumerable<string> CandidateEmails() =>
        new[] { Email, PreferredUsername }
            .Where(e => !string.IsNullOrWhiteSpace(e) && e.Contains('@'))
            .Select(e => e!.Trim().ToLowerInvariant())
            .Distinct();
}

public enum ExternalSignInFailure
{
    /// <summary>No account in Smart Registration matches this university account.</summary>
    NotRegistered,

    /// <summary>The matching account has been switched off.</summary>
    Inactive,

    /// <summary>The matching account is already linked to a different university identity.</summary>
    AccountMismatch,
}

public sealed record ExternalSignInResult(SignInSuccess? Success, ExternalSignInFailure? Failure)
{
    public bool Succeeded => Success is not null;

    public static ExternalSignInResult Ok(SignInSuccess success) => new(success, null);

    public static ExternalSignInResult Fail(ExternalSignInFailure failure) => new(null, failure);
}
