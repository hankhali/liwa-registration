using Liwa.Domain.Academic;
using Liwa.Domain.Activity;
using Liwa.Domain.Planning;
using Liwa.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Liwa.Infrastructure.Persistence;

public sealed class LiwaDbContext(DbContextOptions<LiwaDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Term> Terms => Set<Term>();

    public DbSet<Course> Courses => Set<Course>();

    public DbSet<Section> Sections => Set<Section>();

    public DbSet<AcademicProgram> Programs => Set<AcademicProgram>();

    public DbSet<DegreePlan> DegreePlans => Set<DegreePlan>();

    public DbSet<StudentRecord> StudentRecords => Set<StudentRecord>();

    public DbSet<CreditPolicy> CreditPolicies => Set<CreditPolicy>();

    public DbSet<Plan> Plans => Set<Plan>();

    public DbSet<AuditEntry> AuditLog => Set<AuditEntry>();

    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LiwaDbContext).Assembly);
    }
}
