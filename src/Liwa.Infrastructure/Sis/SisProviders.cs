using Liwa.Application.Sis;
using Liwa.Domain.Academic;
using Liwa.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liwa.Infrastructure.Sis;

/// <summary>
/// The providers backed by this application's own database, which is filled by the CSV imports (the same format a
/// PeopleSoft query export would use; docs/import-contract.md). This is the provider in use today.
/// </summary>
internal sealed class DatabaseSisProvider(LiwaDbContext db) : IStudentRecordProvider, ICourseCatalogProvider, ITermScheduleProvider
{
    public async Task<StudentRecord?> GetByUserAsync(Guid userId, CancellationToken ct) =>
        await db.StudentRecords.AsNoTracking().Include(s => s.Transcript).Include(s => s.TransferCredits).SingleOrDefaultAsync(s => s.UserId == userId, ct);

    public async Task<IReadOnlyList<Course>> CoursesAsync(CancellationToken ct) => await db.Courses.AsNoTracking().ToListAsync(ct);

    public async Task<AcademicProgram?> ProgramAsync(Guid programId, CancellationToken ct) =>
        await db.Programs.AsNoTracking().SingleOrDefaultAsync(p => p.Id == programId, ct);

    public async Task<DegreePlan?> DegreePlanAsync(Guid programId, int catalogYear, CancellationToken ct) =>
        await db.DegreePlans.AsNoTracking().Include(d => d.Groups).ThenInclude(g => g.Courses)
            .SingleOrDefaultAsync(d => d.ProgramId == programId && d.CatalogYear == catalogYear, ct);

    public async Task<Term?> CurrentTermAsync(CancellationToken ct) => await db.Terms.AsNoTracking().FirstOrDefaultAsync(t => t.IsCurrent, ct);

    public async Task<IReadOnlyList<Section>> SectionsAsync(Guid termId, CancellationToken ct) =>
        await db.Sections.AsNoTracking().Where(s => s.TermId == termId).ToListAsync(ct);
}

/// <summary>
/// Placeholder for reading directly from Oracle PeopleSoft Campus Solutions (Integration Broker REST, a Component
/// Interface through middleware, or scheduled PS Query exports). Not implemented: there is no API access yet.
/// docs/peoplesoft-integration.md lists, for each method, the PeopleSoft records and fields it must read and how they
/// map to the import columns. Until then the CSV export route (Imports page) is the supported path.
/// </summary>
internal sealed class PeopleSoftSisProvider : IStudentRecordProvider, ICourseCatalogProvider, ITermScheduleProvider
{
    private static NotSupportedException NotYet(string what) =>
        new($"The PeopleSoft provider is not implemented yet ({what}). Set Sis:Provider to Database and load PeopleSoft exports through the Imports page; see docs/peoplesoft-integration.md.");

    /// <summary>EMPLID → ACAD_PROG / ACAD_PLAN (program, catalog year = REQ_TERM), STDNT_ENRL + grades, TRNS_CRSE_DTL, ADVISOR, SRVC_IND_DATA (holds).</summary>
    public Task<StudentRecord?> GetByUserAsync(Guid userId, CancellationToken ct) => throw NotYet("student records");

    /// <summary>CRSE_CATALOG / CRSE_OFFER (courses, units, typically offered), RQ_GRP / requisites (prerequisite rules).</summary>
    public Task<IReadOnlyList<Course>> CoursesAsync(CancellationToken ct) => throw NotYet("courses");

    /// <summary>ACAD_PROG_TBL (programs, total units).</summary>
    public Task<AcademicProgram?> ProgramAsync(Guid programId, CancellationToken ct) => throw NotYet("programs");

    /// <summary>Academic Advisement requirement groups (RQ_GRP_TBL, RQ_LINE_TBL) per catalog year.</summary>
    public Task<DegreePlan?> DegreePlanAsync(Guid programId, int catalogYear, CancellationToken ct) => throw NotYet("degree plans");

    /// <summary>TERM_TBL (STRM, dates).</summary>
    public Task<Term?> CurrentTermAsync(CancellationToken ct) => throw NotYet("terms");

    /// <summary>CLASS_TBL + CLASS_MTG_PAT (sections, meeting patterns, ENRL_CAP / ENRL_TOT).</summary>
    public Task<IReadOnlyList<Section>> SectionsAsync(Guid termId, CancellationToken ct) => throw NotYet("class schedule");
}
