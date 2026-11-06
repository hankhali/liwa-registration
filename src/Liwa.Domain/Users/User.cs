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

    public static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new ArgumentException("Hash is required.", nameof(passwordHash));
        PasswordHash = passwordHash;
    }

    public void RecordSignIn(DateTimeOffset at) => LastSignInAt = at;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
