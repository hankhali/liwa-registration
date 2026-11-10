namespace Liwa.Domain.Users;

/// <summary>
/// A person who can sign in. Authentication method is deliberately not part of the model's identity:
/// local accounts carry a <see cref="PasswordHash"/>, SSO accounts carry an <see cref="ExternalSubject"/>.
/// </summary>
public sealed class User
{
    private User() { } // EF Core

    public User(Guid id, string username, string displayName, string? email, UserRole role, string? universityId, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("Username is required.", nameof(username));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name is required.", nameof(displayName));

        Id = id;
        Username = NormalizeUsername(username);
        DisplayName = displayName.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        Role = role;
        UniversityId = string.IsNullOrWhiteSpace(universityId) ? null : universityId.Trim();
        CreatedAt = createdAt;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    /// <summary>Lower-case, trimmed. Unique.</summary>
    public string Username { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public UserRole Role { get; private set; }

    /// <summary>The PeopleSoft EMPLID for students and staff, once known.</summary>
    public string? UniversityId { get; private set; }

    /// <summary>Null for SSO-only accounts.</summary>
    public string? PasswordHash { get; private set; }

    /// <summary>Identity-provider subject (e.g. Entra ID object id) once SSO is enabled.</summary>
    public string? ExternalSubject { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastSignInAt { get; private set; }

    /// <summary>"en" or "ar": the language of emails and other messages generated for this person.</summary>
    public string PreferredLanguage { get; private set; } = "en";

    /// <summary>When the first-login walkthrough was dismissed (students), or null if never shown/dismissed.</summary>
    public DateTimeOffset? WalkthroughDismissedAt { get; private set; }

    public static readonly IReadOnlySet<string> Languages = new HashSet<string>(StringComparer.Ordinal) { "en", "ar" };

    public void SetPreferredLanguage(string language)
    {
        var lang = (language ?? string.Empty).Trim().ToLowerInvariant();
        if (!Languages.Contains(lang)) throw new ArgumentException($"Unsupported language '{language}'.", nameof(language));
        PreferredLanguage = lang;
    }

    public void DismissWalkthrough(DateTimeOffset at) => WalkthroughDismissedAt ??= at;

    public static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();

    /// <summary>Profile fields that come from registrar data (imports).</summary>
    public void UpdateProfile(string displayName, string? email, string? universityId)
    {
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name is required.", nameof(displayName));
        DisplayName = displayName.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        UniversityId = string.IsNullOrWhiteSpace(universityId) ? null : universityId.Trim();
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new ArgumentException("Hash is required.", nameof(passwordHash));
        PasswordHash = passwordHash;
    }

    /// <summary>
    /// Ties this account to an identity-provider subject on first SSO sign-in. Once linked, it is never
    /// silently re-pointed to another subject (an email address can be reassigned to a different person).
    /// </summary>
    public void LinkExternalSubject(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject)) throw new ArgumentException("Subject is required.", nameof(subject));
        if (ExternalSubject is not null && ExternalSubject != subject)
            throw new InvalidOperationException("This account is already linked to a different identity.");
        ExternalSubject = subject;
    }

    public void RecordSignIn(DateTimeOffset at) => LastSignInAt = at;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
