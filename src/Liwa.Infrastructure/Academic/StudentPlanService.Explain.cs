using Liwa.Domain.Academic;
using Liwa.Domain.Planning;
using Microsoft.EntityFrameworkCore;

namespace Liwa.Infrastructure.Academic;

/// <summary>What the explainer needs from the planner: the input, the recommendation, the plan being explained, and the
/// personal strings that must never reach the model (used only to check the payload).</summary>
internal sealed record ExplainContext(
    PlannerInput Input,
    PlannerResult Recommendation,
    IReadOnlyList<Guid> Selection,
    IReadOnlyList<Course> Courses,
    string TermName,
    IReadOnlyList<string> PersonalStrings);

internal sealed partial class StudentPlanService
{
    /// <summary>For the student: their sections (given, else the saved plan, else the recommendation).</summary>
    internal async Task<ExplainContext?> ExplainContextForStudentAsync(Guid studentUserId, IReadOnlyList<Guid>? sectionIds, CancellationToken ct)
    {
        var ctx = await BuildAsync(studentUserId, null, PlannerPreferences.None, ct);
        if (ctx is null) return null;
        var recommendation = Planner.Plan(ctx.Input);
        IReadOnlyList<Guid> selection = sectionIds is { Count: > 0 } ? sectionIds
            : (await LoadPlanAsync(ctx.Record.Id, ctx.Term.Id, ct))?.SectionIds ?? recommendation.Plan.Select(p => p.SectionId).ToList();
        return new ExplainContext(ctx.Input, recommendation, selection, ctx.Courses, ctx.Term.Name, await PersonalStringsAsync(ctx.Record, ct));
    }

    /// <summary>For the advisor: the submitted plan of one of their own students (null = not theirs or not found).</summary>
    internal async Task<ExplainContext?> ExplainContextForAdvisorAsync(Guid advisorUserId, Guid planId, CancellationToken ct)
    {
        var (outcome, plan, record) = await LoadForAdvisorAsync(advisorUserId, planId, tracking: false, ct);
        if (outcome != Application.Planning.DecisionOutcome.Done) return null;
        var ctx = await BuildAsync(record!.UserId, AvailabilityOf(plan!.Current!), PlannerPreferences.None, ct);
        if (ctx is null) return null;
        var strings = (await PersonalStringsAsync(ctx.Record, ct)).ToList();
        strings.AddRange(await db.Users.AsNoTracking().Where(u => u.Id == advisorUserId).Select(u => u.DisplayName).ToListAsync(ct));
        return new ExplainContext(ctx.Input, Planner.Plan(ctx.Input), plan.Current!.Sections.Select(s => s.SectionId).ToList(), ctx.Courses, ctx.Term.Name, strings);
    }

    /// <summary>The student's and advisor's names, the student ID and emails: checked against, never sent.</summary>
    private async Task<IReadOnlyList<string>> PersonalStringsAsync(StudentRecord record, CancellationToken ct)
    {
        var ids = new List<Guid> { record.UserId };
        if (record.AdvisorUserId is { } a) ids.Add(a);
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToListAsync(ct);
        return users.SelectMany(u => new[] { u.DisplayName, u.Username, u.Email ?? "" }).Append(record.StudentId)
            .Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    }
}
