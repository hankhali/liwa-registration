namespace Liwa.Domain.Activity;

/// <summary>
/// A setting an administrator changes in the app (sign-in, AI provider). Secrets (client secret, API key) are stored
/// encrypted (<see cref="IsSecret"/>); the app only ever shows their last four characters (brief §4).
/// </summary>
public sealed class AppSetting
{
    private AppSetting() { } // EF Core

    public AppSetting(string key, string? value, bool isSecret, DateTimeOffset at, Guid? byUserId)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key is required.", nameof(key));
        Key = key;
        Set(value, isSecret, at, byUserId);
    }

    /// <summary>e.g. "sso.tenantId", "ai.apiKey".</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>Plain value, or the encrypted value (base64) when <see cref="IsSecret"/>.</summary>
    public string? Value { get; private set; }

    public bool IsSecret { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    public void Set(string? value, bool isSecret, DateTimeOffset at, Guid? byUserId)
    {
        Value = value;
        IsSecret = isSecret;
        UpdatedAt = at;
        UpdatedByUserId = byUserId;
    }
}
