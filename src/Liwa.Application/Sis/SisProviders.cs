using Liwa.Domain.Academic;

namespace Liwa.Application.Sis;

// ============================================================================================
// Data provider abstraction (brief §6). Everything the planner and the screens read about students,
// the catalog and the timetable goes through these interfaces. Nothing outside the provider layer knows
// which backend is in use; the setting "Sis:Provider" chooses it ("Database" today, "PeopleSoft" later).
// See docs/peoplesoft-integration.md for what a PeopleSoft provider must return.
// ============================================================================================

/// <summary>A student's record: program, catalog year, transcript, transfer credit, standing inputs, advisor, hold.</summary>
public interface IStudentRecordProvider
{
    /// <summary>The record for the person signed in (their app account), with transcript and transfer credit.</summary>
    Task<StudentRecord?> GetByUserAsync(Guid userId, CancellationToken ct);
}

/// <summary>Courses (with prerequisite rules and usual seasons), programs and degree plans per catalog year.</summary>
public interface ICourseCatalogProvider
{
    Task<IReadOnlyList<Course>> CoursesAsync(CancellationToken ct);

    Task<AcademicProgram?> ProgramAsync(Guid programId, CancellationToken ct);

    /// <summary>The degree plan with its requirement groups and courses.</summary>
    Task<DegreePlan?> DegreePlanAsync(Guid programId, int catalogYear, CancellationToken ct);
}

/// <summary>Terms (teaching days, Friday rule, overrides) and the class schedule (sections with meeting times and seats).</summary>
public interface ITermScheduleProvider
{
    Task<Term?> CurrentTermAsync(CancellationToken ct);

    Task<IReadOnlyList<Section>> SectionsAsync(Guid termId, CancellationToken ct);
}

/// <summary>Which backend the providers read from (configuration "Sis:Provider").</summary>
public enum SisProviderKind
{
    Database = 1,
    PeopleSoft = 2,
}
