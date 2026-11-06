using Liwa.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liwa.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(u => u.Id);
        b.Property(u => u.Id).ValueGeneratedNever();
        b.Property(u => u.Username).HasMaxLength(100).IsRequired();
        b.HasIndex(u => u.Username).IsUnique();
        b.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
        b.Property(u => u.Email).HasMaxLength(256);
        b.HasIndex(u => u.Email).IsUnique().HasFilter("email IS NOT NULL");
        b.Property(u => u.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(u => u.UniversityId).HasMaxLength(20);
        b.HasIndex(u => u.UniversityId).IsUnique().HasFilter("university_id IS NOT NULL");
        b.Property(u => u.PasswordHash).HasMaxLength(256);
        b.Property(u => u.ExternalSubject).HasMaxLength(256);
        b.HasIndex(u => u.ExternalSubject).IsUnique().HasFilter("external_subject IS NOT NULL");
    }
}
