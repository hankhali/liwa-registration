using Liwa.Domain.Planning;

namespace Liwa.Application.Planning;

/// <summary>A plan that a re-check moved to "Needs review", and why.</summary>
public sealed record RecheckedPlan(Guid PlanId, string StudentId, IReadOnlyList<PlannerReason> Problems);

/// <param name="Checked">Approved plans that were re-checked.</param>
public sealed record RecheckResult(int Checked, IReadOnlyList<RecheckedPlan> MovedToNeedsReview);

public sealed record GradePostingRequest(string StudentId, string CourseCode, string TermCode, string Grade);

public enum GradePostingOutcome
{
    Posted = 1,
    StudentNotFound = 2,
    CourseNotFound = 3,
    NotInProgress = 4,
    InvalidGrade = 5,
}

public sealed record GradePostingResult(GradePostingOutcome Outcome, RecheckResult? Recheck);

/// <summary>
/// Re-validates approved plans when grades change (brief §10). A prerequisite or corequisite that is no longer
/// met moves the plan to "Needs review" and notifies the student and the advisor. A section filling up does not.
/// </summary>
public interface IPlanRevalidationService
{
    /// <summary>Re-checks approved plans for the current term (all students, or only <paramref name="studentRecordIds"/>).</summary>
    Task<RecheckResult> RecheckCurrentTermAsync(Guid? actorUserId, IReadOnlyCollection<Guid>? studentRecordIds, CancellationToken ct);

    /// <summary>Posts one final grade for a course in progress, then re-checks that student's approved plan.</summary>
    Task<GradePostingResult> PostGradeAsync(Guid actorUserId, GradePostingRequest request, CancellationToken ct);
}
