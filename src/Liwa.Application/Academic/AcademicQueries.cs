namespace Liwa.Application.Academic;

public sealed record TermInfo(string Code, string Name);

/// <summary>What a student sees about their own studies.</summary>
public sealed record StudentSummary(
    string StudentId,
    string ProgramCode,
    string ProgramName,
    string? ProgramNameAr,
    int CatalogYear,
    int CreditsEarned,
    int CreditsRequired,
    int InProgressCredits,
    string Standing,
    decimal? Gpa,
    bool OnProbation,
    string? AdvisorName,
    TermInfo? CurrentTerm);

/// <summary>One row in an advisor's list of students.</summary>
public sealed record AdviseeSummary(
    Guid UserId,
    string StudentId,
    string DisplayName,
    string ProgramCode,
    string ProgramName,
    string? ProgramNameAr,
    int CreditsEarned,
    int CreditsRequired,
    decimal? Gpa,
    bool OnProbation,
    Liwa.Domain.Academic.Standing? Standing = null,
    string? Hold = null,
    Liwa.Domain.Planning.PlanStatus? PlanStatus = null,
    Guid? PlanId = null,
    string? ProjectedGraduation = null,
    int? TermsRemaining = null,
    DateTimeOffset? LastActivity = null);

/// <summary>What the admin home page shows about the data in the system.</summary>
public sealed record DataOverview(
    TermInfo? CurrentTerm,
    int Terms,
    int Courses,
    int Programs,
    int DegreePlans,
    int SectionsThisTerm,
    int Students,
    int Advisors,
    DateTimeOffset? LastImportAt);

public interface IAcademicQueries
{
    Task<StudentSummary?> GetStudentSummaryAsync(Guid userId, CancellationToken ct);

    Task<IReadOnlyList<AdviseeSummary>> GetAdviseesAsync(Guid advisorUserId, CancellationToken ct);

    Task<DataOverview> GetOverviewAsync(CancellationToken ct);
}
