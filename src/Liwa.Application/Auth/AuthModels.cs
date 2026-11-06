using Liwa.Domain.Users;

namespace Liwa.Application.Auth;

public sealed record UserProfile(Guid Id, string Username, string DisplayName, string Role, string? UniversityId)
{
    public static UserProfile From(User user) =>
        new(user.Id, user.Username, user.DisplayName, RoleNames.Of(user.Role), user.UniversityId);
}

public sealed record SignInSuccess(string AccessToken, DateTimeOffset ExpiresAt, UserProfile User);

/// <summary>
/// Result of a sign-in attempt. Failure deliberately carries no reason, so callers cannot reveal
/// whether a username exists.
/// </summary>
public sealed record SignInResult(SignInSuccess? Success)
{
    public bool Succeeded => Success is not null;

    public static SignInResult Failed { get; } = new((SignInSuccess?)null);
}
