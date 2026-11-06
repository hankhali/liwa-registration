namespace Liwa.Domain.Users;

public enum UserRole
{
    Student = 1,
    Advisor = 2,
    Admin = 3,
}

/// <summary>String names used in tokens and authorization policies.</summary>
public static class RoleNames
{
    public const string Student = nameof(UserRole.Student);
    public const string Advisor = nameof(UserRole.Advisor);
    public const string Admin = nameof(UserRole.Admin);

    public static string Of(UserRole role) => role.ToString();
}
