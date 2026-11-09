using Liwa.Application.Abstractions;
using Liwa.Application.Imports;
using Liwa.Domain.Users;
using Liwa.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Liwa.Infrastructure.Seeding;

public sealed class DemoSeedOptions
{
    public const string Section = "DemoSeed";

    /// <summary>On in Development only. Never enable for a real deployment.</summary>
    public bool Enabled { get; set; }

    /// <summary>Shared password for every demo account.</summary>
    public string Password { get; set; } = "Liwa#2027";
}

/// <summary>
/// Creates demo accounts, then loads the illustrative academic data (terms, catalog, three programs,
/// Spring 2027 timetable, student records) from the CSV files in Seeding/Demo through the real import
/// pipeline, so the demo also proves the import contract. Idempotent: existing usernames are left alone
/// and academic data is only loaded into an empty database (never over real or edited data).
/// </summary>
public sealed class DemoSeeder(
    LiwaDbContext db,
    IPasswordHasher hasher,
    TimeProvider clock,
    IImportService imports,
    IOptions<DemoSeedOptions> options,
    ILogger<DemoSeeder> logger)
{
    /// <summary>Stable IDs so later seed data (M2) can reference these accounts.</summary>
    public static IReadOnlyList<DemoAccount> Accounts { get; } =
    [
        new(new Guid("0b9e6a1e-0001-4000-8000-000000000001"), "student.new", "Mariam Al Mansoori", UserRole.Student, "202700101", "New first-year"),
        new(new Guid("0b9e6a1e-0001-4000-8000-000000000002"), "student.probation", "Khalid Al Hammadi", UserRole.Student, "202500102", "On academic probation"),
        new(new Guid("0b9e6a1e-0001-4000-8000-000000000003"), "student.honors", "Aisha Al Nuaimi", UserRole.Student, "202500103", "High GPA"),
        new(new Guid("0b9e6a1e-0001-4000-8000-000000000004"), "student.senior", "Omar Al Shamsi", UserRole.Student, "202300104", "Graduating senior, 6 credits left"),
        new(new Guid("0b9e6a1e-0001-4000-8000-000000000005"), "student.repeat", "Noura Al Ketbi", UserRole.Student, "202500105", "Failed a prerequisite"),
        new(new Guid("0b9e6a1e-0002-4000-8000-000000000001"), "advisor.it", "Dr. Ahmed Al Zaabi", UserRole.Advisor, "E1001", "Advisor, Information Technology"),
        new(new Guid("0b9e6a1e-0002-4000-8000-000000000002"), "advisor.business", "Dr. Fatima Al Suwaidi", UserRole.Advisor, "E1002", "Advisor, Business"),
        new(new Guid("0b9e6a1e-0002-4000-8000-000000000003"), "advisor.media", "Dr. Saeed Al Mazrouei", UserRole.Advisor, "E1003", "Advisor, Mass Communication"),
        new(new Guid("0b9e6a1e-0003-4000-8000-000000000001"), "admin", "Registrar Admin", UserRole.Admin, null, "Administrator"),
    ];

    public async Task SeedAsync(CancellationToken ct)
    {
        var o = options.Value;
        if (!o.Enabled) return;

        var existing = await db.Users.Select(u => u.Username).ToListAsync(ct);
        var created = 0;
        foreach (var a in Accounts.Where(a => !existing.Contains(a.Username)))
        {
            var user = new User(a.Id, a.Username, a.DisplayName, email: null, a.Role, a.UniversityId, clock.GetUtcNow());
            user.SetPasswordHash(hasher.Hash(o.Password));
            db.Users.Add(user);
            created++;
        }

        if (created > 0) await db.SaveChangesAsync(ct);
        logger.LogWarning("Demo seed is ENABLED: {Created} demo account(s) created. Disable DemoSeed:Enabled for real deployments.", created);

        if (await db.Terms.AnyAsync(ct)) return;
        foreach (var (kind, file) in DemoFiles)
        {
            await using var stream = typeof(DemoSeeder).Assembly.GetManifestResourceStream($"Liwa.Infrastructure.Seeding.Demo.{file}")
                ?? throw new InvalidOperationException($"Demo data file {file} is missing from the build.");
            var report = await imports.ImportAsync(kind, stream, dryRun: false, actorUserId: null, ct);
            if (report.HasErrors)
                throw new InvalidOperationException($"Demo data {file} failed to import: " +
                    string.Join(" | ", report.Issues.Where(i => i.Severity == IssueSeverity.Error).Take(5).Select(i => $"line {i.Line}: {i.Message}")));
            logger.LogInformation("Demo data {File}: {Created} created, {Warnings} warning(s).", file, report.Created, report.WarningCount);
        }
    }

    /// <summary>Import order matters: later files refer to earlier ones.</summary>
    public static IReadOnlyList<(ImportKind Kind, string File)> DemoFiles { get; } =
    [
        (ImportKind.Terms, "terms.csv"),
        (ImportKind.Courses, "courses.csv"),
        (ImportKind.Programs, "programs.csv"),
        (ImportKind.DegreePlans, "degree-plans.csv"),
        (ImportKind.Sections, "sections.csv"),
        (ImportKind.Students, "students.csv"),
        (ImportKind.Transcripts, "transcripts.csv"),
    ];
}

public sealed record DemoAccount(Guid Id, string Username, string DisplayName, UserRole Role, string? UniversityId, string Description);
