using Liwa.Domain.Academic;
using Liwa.Domain.Planning;

namespace Liwa.Application.Planning;

public sealed record CourseInfo(string Code, string Title, string? TitleAr, int Credits);

public sealed record SectionInfo(Guid Id, string CourseCode, string SectionCode, DeliveryMode Delivery, string? Instructor, string? Room,
    IReadOnlyList<MeetingTime> Meetings, int SeatsLeft);

public sealed record SavedPlan(Guid Id, PlanStatus Status, int Version, IReadOnlyList<Guid> SectionIds, DateTimeOffset UpdatedAt,
    IReadOnlyList<TimeWindow>? Availability);

/// <summary>Everything the student planning screen needs in one response.</summary>
public sealed record PlanningView(
    string TermCode,
    string TermName,
    string? AdvisorName,
    PlannerResult Recommendation,
    IReadOnlyList<CourseInfo> Courses,
    IReadOnlyList<SectionInfo> Sections,
    SavedPlan? Saved);

public sealed record SavePlanRequest(
    IReadOnlyList<Guid> SectionIds,
    IReadOnlyList<TimeWindow>? Availability,
    bool Submit,
    IReadOnlyList<string>? AcknowledgedWarnings,
    string? Note);

public enum SaveOutcome
{
    Saved = 1,
    Invalid = 2,
    WarningsNotConfirmed = 3,
    AlreadyWithAdvisor = 4,
    NotFound = 5,
}

public sealed record SavePlanResult(SaveOutcome Outcome, PlanCheckResult? Check, SavedPlan? Plan);

public interface IStudentPlanService
{
    Task<PlanningView?> GetViewAsync(Guid studentUserId, Availability? availability, PlannerPreferences preferences, CancellationToken ct);

    Task<PlanCheckResult?> CheckAsync(Guid studentUserId, IReadOnlyCollection<Guid> sectionIds, Availability? availability, CancellationToken ct);

    Task<SavePlanResult> SaveAsync(Guid studentUserId, SavePlanRequest request, CancellationToken ct);
}
