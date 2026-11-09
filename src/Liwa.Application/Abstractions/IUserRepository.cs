using Liwa.Domain.Users;

namespace Liwa.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> FindByUsernameAsync(string username, CancellationToken ct);

    Task<User?> FindByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Exact match on the identity-provider subject.</summary>
    Task<User?> FindByExternalSubjectAsync(string subject, CancellationToken ct);

    /// <summary>Match on the stored (lower-case) email address.</summary>
    Task<User?> FindByEmailAsync(string email, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
