using Liwa.Application.Academic;
using Liwa.Domain.Planning;

namespace Liwa.Application.Planning;

/// <summary>One step in a plan's history, visible to both the student and the advisor.</summary>
/// <param name="Action">submitted, saved, reviewing, approved, rejected, proposed, accepted.</param>
/// <param name="Added">Classes added in this step, e.g. "ITC201 (E1)".</param>
/// <param name="Removed">Classes removed in this step.</param>
public sealed record PlanEvent(DateTimeOffset At, string ActorName, string ActorRole, string Action, PlanStatus Status, int? Version,
    string? Note, IReadOnlyList<string> Added, IReadOnlyList<string> Removed);

/// <summary>A row in the advisor's review queue.</summary>
public sealed record QueueItem(
    Guid PlanId,
    Guid StudentUserId,
    string StudentName,
    string StudentId,
    string ProgramCode,
    string ProgramName,
    string? ProgramNameAr,
    PlanStatus Status,
    int Version,
    int Credits,
    int WarningCount,
    DateTimeOffset UpdatedAt,
    bool OnProbation,
    bool Urgent,
    string? Hold = null,
    bool NearGraduation = false,
    int? TermsRemaining = null);

/// <summary>The counts on the advisor's home page (brief §10).</summary>
public sealed record AdvisorOverview(int AwaitingReview, int NeedsReview, int ApprovedThisTerm, int ChangesProposed);

public sealed record ConfirmedWarning(string Key, string Code, IReadOnlyList<string> Courses, string? Value);

/// <summary>Everything the advisor's one-page review needs.</summary>
public sealed record PlanReview(
    Guid PlanId,
    Guid StudentUserId,
    PlanStatus Status,
    int Version,
    StudentSummary Student,
    string StudentName,
    PlanningView Planning,
    IReadOnlyList<Guid> SectionIds,
    PlanCheckResult LiveCheck,
    IReadOnlyList<ConfirmedWarning> ConfirmedWarnings,
    GraduationProjection ThisPlan,
    IReadOnlyList<PlanEvent> History);

public enum Decision
{
    Approve = 1,
    Reject = 2,
    ProposeChanges = 3,
}

public sealed record DecisionRequest(Decision Decision, string? Note, IReadOnlyList<Guid>? SectionIds);

public enum DecisionOutcome
{
    Done = 1,
    NotFound = 2,
    NotYourStudent = 3,
    NotDecidable = 4,
    NoteRequired = 5,
    PlanHasErrors = 6,
}

public sealed record DecisionResult(DecisionOutcome Outcome, PlanCheckResult? Check);

/// <summary>A student's transcript line for the advisor's read-only view.</summary>
public sealed record TranscriptLine(string TermCode, string CourseCode, string Title, string? TitleAr, int Credits, string? Grade, string Status);

public sealed record StudentRecordView(Guid UserId, string StudentName, StudentSummary Summary, IReadOnlyList<TranscriptLine> Transcript, PlanStatus? CurrentPlanStatus, Guid? CurrentPlanId,
    IReadOnlyList<PlanEvent>? PlanHistory = null, string? Hold = null);

public interface IAdvisorService
{
    Task<IReadOnlyList<QueueItem>> GetQueueAsync(Guid advisorUserId, CancellationToken ct);

    /// <summary>The advisor's students with standing, plan status, projected graduation and last activity.</summary>
    Task<IReadOnlyList<Liwa.Application.Academic.AdviseeSummary>> GetAdviseesAsync(Guid advisorUserId, CancellationToken ct);

    Task<AdvisorOverview> GetOverviewAsync(Guid advisorUserId, CancellationToken ct);

    /// <summary>Opening a newly sent plan marks it "Advisor reviewing" (and tells the student).</summary>
    Task<(DecisionOutcome Outcome, PlanReview? Review)> OpenReviewAsync(Guid advisorUserId, Guid planId, CancellationToken ct);

    Task<PlanCheckResult?> CheckProposalAsync(Guid advisorUserId, Guid planId, IReadOnlyCollection<Guid> sectionIds, CancellationToken ct);

    Task<DecisionResult> DecideAsync(Guid advisorUserId, Guid planId, DecisionRequest request, CancellationToken ct);

    Task<StudentRecordView?> GetStudentRecordAsync(Guid advisorUserId, Guid studentUserId, CancellationToken ct);
}

public sealed record NotificationItem(Guid Id, DateTimeOffset CreatedAt, string MessageKey, string? Parameters, string? Link, bool Read);

public sealed record NotificationList(int Unread, IReadOnlyList<NotificationItem> Items);

public interface INotificationService
{
    Task<NotificationList> GetAsync(Guid userId, CancellationToken ct);

    Task MarkAllReadAsync(Guid userId, CancellationToken ct);
}
