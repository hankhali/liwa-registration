using Liwa.Domain.Academic;
using Liwa.Domain.Activity;
using Liwa.Domain.Planning;
using Liwa.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liwa.Infrastructure.Persistence.Configurations;

internal sealed class TermConfiguration : IEntityTypeConfiguration<Term>
{
    public void Configure(EntityTypeBuilder<Term> b)
    {
        b.ToTable("terms");
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).ValueGeneratedNever();
        b.Property(t => t.Code).HasMaxLength(10).IsRequired();
        b.HasIndex(t => t.Code).IsUnique();
        b.Property(t => t.Name).HasMaxLength(100).IsRequired();
        b.Property(t => t.Season).HasConversion<string>().HasMaxLength(10);
        // "Only one current term" is enforced by the import/admin code (a unique index would make swapping the current term order-dependent).
    }
}

internal sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> b)
    {
        b.ToTable("courses");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).ValueGeneratedNever();
        b.Property(c => c.Code).HasMaxLength(10).IsRequired();
        b.HasIndex(c => c.Code).IsUnique();
        b.Ignore(c => c.Subject);
        b.Property(c => c.Title).HasMaxLength(200).IsRequired();
        b.Property(c => c.TitleAr).HasMaxLength(200);
        b.Property(c => c.Department).HasMaxLength(100);
        b.Property(c => c.Description).HasMaxLength(2000);
        b.Property(c => c.Prerequisites).HasMaxLength(500);
    }
}

internal sealed class SectionConfiguration : IEntityTypeConfiguration<Section>
{
    public void Configure(EntityTypeBuilder<Section> b)
    {
        b.ToTable("sections");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedNever();
        b.Property(s => s.SectionCode).HasMaxLength(10).IsRequired();
        b.HasIndex(s => new { s.TermId, s.CourseId, s.SectionCode }).IsUnique();
        b.Property(s => s.Delivery).HasConversion<string>().HasMaxLength(10);
        b.Property(s => s.Instructor).HasMaxLength(200);
        b.Property(s => s.Room).HasMaxLength(50);
        b.Ignore(s => s.SeatsLeft);
        b.HasOne<Term>().WithMany().HasForeignKey(s => s.TermId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Course>().WithMany().HasForeignKey(s => s.CourseId).OnDelete(DeleteBehavior.Restrict);
        // Weekly meetings live with the section as JSON; the planner always loads them together.
        b.OwnsMany(s => s.Meetings, m =>
        {
            m.ToJson("meetings");
            m.Property(x => x.Day).HasConversion<string>();
        });
        b.Navigation(s => s.Meetings).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ProgramConfiguration : IEntityTypeConfiguration<AcademicProgram>
{
    public void Configure(EntityTypeBuilder<AcademicProgram> b)
    {
        b.ToTable("programs");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).ValueGeneratedNever();
        b.Property(p => p.Code).HasMaxLength(20).IsRequired();
        b.HasIndex(p => p.Code).IsUnique();
        b.Property(p => p.Name).HasMaxLength(200).IsRequired();
        b.Property(p => p.NameAr).HasMaxLength(200);
        b.Property(p => p.College).HasMaxLength(200);
    }
}

internal sealed class DegreePlanConfiguration : IEntityTypeConfiguration<DegreePlan>
{
    public void Configure(EntityTypeBuilder<DegreePlan> b)
    {
        b.ToTable("degree_plans");
        b.HasKey(d => d.Id);
        b.Property(d => d.Id).ValueGeneratedNever();
        b.HasIndex(d => new { d.ProgramId, d.CatalogYear }).IsUnique();
        b.HasOne<AcademicProgram>().WithMany().HasForeignKey(d => d.ProgramId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(d => d.Groups).WithOne().HasForeignKey(g => g.DegreePlanId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(d => d.Groups).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class RequirementGroupConfiguration : IEntityTypeConfiguration<RequirementGroup>
{
    public void Configure(EntityTypeBuilder<RequirementGroup> b)
    {
        b.ToTable("requirement_groups");
        b.HasKey(g => g.Id);
        b.Property(g => g.Id).ValueGeneratedNever();
        b.Property(g => g.Key).HasMaxLength(30).IsRequired();
        b.HasIndex(g => new { g.DegreePlanId, g.Key }).IsUnique();
        b.Property(g => g.Name).HasMaxLength(200).IsRequired();
        b.Property(g => g.Kind).HasConversion<string>().HasMaxLength(20);
        b.HasMany(g => g.Courses).WithOne().HasForeignKey(c => c.GroupId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(g => g.Courses).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class RequirementCourseConfiguration : IEntityTypeConfiguration<RequirementCourse>
{
    public void Configure(EntityTypeBuilder<RequirementCourse> b)
    {
        b.ToTable("requirement_courses");
        b.HasKey(c => new { c.GroupId, c.CourseId });
        b.HasOne<Course>().WithMany().HasForeignKey(c => c.CourseId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StudentRecordConfiguration : IEntityTypeConfiguration<StudentRecord>
{
    public void Configure(EntityTypeBuilder<StudentRecord> b)
    {
        b.ToTable("student_records");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedNever();
        b.Property(s => s.StudentId).HasMaxLength(20).IsRequired();
        b.HasIndex(s => s.StudentId).IsUnique();
        b.HasIndex(s => s.UserId).IsUnique();
        b.HasIndex(s => s.AdvisorUserId);
        b.Property(s => s.AdmitTermCode).HasMaxLength(10);
        b.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        b.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(s => s.AdvisorUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<AcademicProgram>().WithMany().HasForeignKey(s => s.ProgramId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(s => s.Transcript).WithOne().HasForeignKey(t => t.StudentRecordId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(s => s.TransferCredits).WithOne().HasForeignKey(t => t.StudentRecordId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(s => s.Transcript).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(s => s.TransferCredits).HasField("_transfers").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class TranscriptEntryConfiguration : IEntityTypeConfiguration<TranscriptEntry>
{
    public void Configure(EntityTypeBuilder<TranscriptEntry> b)
    {
        b.ToTable("transcript_entries");
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).ValueGeneratedNever();
        b.Property(t => t.TermCode).HasMaxLength(10).IsRequired();
        b.Property(t => t.Grade).HasMaxLength(3);
        b.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        b.HasOne<Course>().WithMany().HasForeignKey(t => t.CourseId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TransferCreditConfiguration : IEntityTypeConfiguration<TransferCredit>
{
    public void Configure(EntityTypeBuilder<TransferCredit> b)
    {
        b.ToTable("transfer_credits");
        b.HasKey(t => t.Id);
        b.Property(t => t.Id).ValueGeneratedNever();
        b.Property(t => t.Source).HasMaxLength(200);
        b.HasOne<Course>().WithMany().HasForeignKey(t => t.CourseId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CreditPolicyConfiguration : IEntityTypeConfiguration<CreditPolicy>
{
    public void Configure(EntityTypeBuilder<CreditPolicy> b)
    {
        b.ToTable("credit_policies");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).ValueGeneratedNever();
        b.Property(p => p.Name).HasMaxLength(100).IsRequired();
        b.Property(p => p.RepeatGradeRule).HasConversion<string>().HasMaxLength(20);
        b.HasMany(p => p.Rules).WithOne().HasForeignKey(r => r.CreditPolicyId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(p => p.Rules).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class CreditLimitRuleConfiguration : IEntityTypeConfiguration<CreditLimitRule>
{
    public void Configure(EntityTypeBuilder<CreditLimitRule> b)
    {
        b.ToTable("credit_rules");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).ValueGeneratedNever();
        b.HasIndex(r => new { r.CreditPolicyId, r.Priority }).IsUnique();
        b.Property(r => r.Name).HasMaxLength(100).IsRequired();
        b.Property(r => r.Season).HasConversion<string>().HasMaxLength(10);
        b.Property(r => r.Standing).HasConversion<string>().HasMaxLength(10);
        b.Property(r => r.MinGpa).HasPrecision(3, 2);
        b.Property(r => r.MaxGpaExclusive).HasPrecision(3, 2);
    }
}

internal sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> b)
    {
        b.ToTable("plans");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).ValueGeneratedNever();
        b.HasIndex(p => new { p.StudentRecordId, p.TermId }).IsUnique();
        b.HasIndex(p => p.Status);
        b.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        b.Ignore(p => p.Current);
        b.HasOne<StudentRecord>().WithMany().HasForeignKey(p => p.StudentRecordId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Term>().WithMany().HasForeignKey(p => p.TermId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(p => p.Versions).WithOne().HasForeignKey(v => v.PlanId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(p => p.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PlanVersionConfiguration : IEntityTypeConfiguration<PlanVersion>
{
    public void Configure(EntityTypeBuilder<PlanVersion> b)
    {
        b.ToTable("plan_versions");
        b.HasKey(v => v.Id);
        b.Property(v => v.Id).ValueGeneratedNever();
        b.HasIndex(v => new { v.PlanId, v.Number }).IsUnique();
        b.Property(v => v.Note).HasMaxLength(2000);
        b.HasOne<User>().WithMany().HasForeignKey(v => v.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(v => v.Sections).WithOne().HasForeignKey(s => s.PlanVersionId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(v => v.Sections).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PlanVersionSectionConfiguration : IEntityTypeConfiguration<PlanVersionSection>
{
    public void Configure(EntityTypeBuilder<PlanVersionSection> b)
    {
        b.ToTable("plan_version_sections");
        b.HasKey(s => new { s.PlanVersionId, s.SectionId });
        b.HasOne<Section>().WithMany().HasForeignKey(s => s.SectionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> b)
    {
        b.ToTable("audit_log");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).ValueGeneratedNever();
        b.Property(a => a.Action).HasMaxLength(100).IsRequired();
        b.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
        b.Property(a => a.EntityId).HasMaxLength(100);
        b.Property(a => a.Before).HasColumnType("jsonb");
        b.Property(a => a.After).HasColumnType("jsonb");
        b.HasIndex(a => a.At);
        b.HasIndex(a => new { a.EntityType, a.EntityId });
    }
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications");
        b.HasKey(n => n.Id);
        b.Property(n => n.Id).ValueGeneratedNever();
        b.Property(n => n.MessageKey).HasMaxLength(100).IsRequired();
        b.Property(n => n.Parameters).HasColumnType("jsonb");
        b.Property(n => n.Link).HasMaxLength(300);
        b.HasIndex(n => new { n.UserId, n.ReadAt });
        b.HasOne<User>().WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
