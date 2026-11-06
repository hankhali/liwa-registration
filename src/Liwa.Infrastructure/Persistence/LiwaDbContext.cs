using Liwa.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Liwa.Infrastructure.Persistence;

public sealed class LiwaDbContext(DbContextOptions<LiwaDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LiwaDbContext).Assembly);
    }
}
