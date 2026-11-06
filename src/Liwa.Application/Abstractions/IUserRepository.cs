using Liwa.Domain.Users;

namespace Liwa.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> FindByUsernameAsync(string username, CancellationToken ct);

    Task<User?> FindByIdAsync(Guid id, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
