using Liwa.Application.Abstractions;
using Liwa.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Liwa.Infrastructure.Persistence;

internal sealed class UserRepository(LiwaDbContext db) : IUserRepository
{
    public Task<User?> FindByUsernameAsync(string username, CancellationToken ct) =>
        db.Users.SingleOrDefaultAsync(u => u.Username == username, ct);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Users.SingleOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> FindByExternalSubjectAsync(string subject, CancellationToken ct) =>
        db.Users.SingleOrDefaultAsync(u => u.ExternalSubject == subject, ct);

    public Task<User?> FindByEmailAsync(string email, CancellationToken ct) =>
        db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
