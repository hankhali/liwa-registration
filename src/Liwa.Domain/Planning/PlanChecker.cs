using System.Globalization;
using Liwa.Domain.Academic;

namespace Liwa.Domain.Planning;

public static partial class Planner
{
    /// <summary>
    /// Checks a plan the student put together by hand, using the same rules as <see cref="Plan"/>.
    /// Errors (clash, same course twice, not eligible, missing corequisite, full section, over the maximum)
    /// block saving. Warnings (below the minimum, waiting for a grade, outside the student's free time)
    /// are allowed but must be confirmed with "I understand" when the plan is sent.
    /// </summary>
    public static PlanCheckResult Check(PlannerInput input, IReadOnlyCollection<Guid> sectionIds)
    {
        var errors = new List<PlannerReason>();
        var warnings = new List<PlannerReason>();
        var student = input.Student;
        var catalog = input.Catalog.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
        var sectionById = input.Sections.ToDictionary(s => s.Id);
        var inPlan = input.DegreePlan.SelectMany(g => g.Courses).Select(c => c.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var picked = new List<PlannerSection>();
        foreach (var id in sectionIds.Distinct())
        {
            if (sectionById.TryGetValue(id, out var s)) picked.Add(s);
            else errors.Add(PlannerReason.Of(ReasonCode.UnknownSection, id.ToString()));
        }

        foreach (var dup in picked.GroupBy(s => s.CourseCode, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            errors.Add(PlannerReason.Of(ReasonCode.DuplicateCourse, [dup.Key]));

        for (var i = 0; i < picked.Count; i++)
            for (var j = i + 1; j < picked.Count; j++)
                if (picked[i].Meetings.Any(m => picked[j].Meetings.Any(m.Overlaps)))
                    errors.Add(PlannerReason.Of(ReasonCode.ClashesWith, [picked[i].CourseCode, picked[j].CourseCode]));

        var state = new State(student.Passed, student.InProgress, student.Failed, student.CreditsEarned, student.PermissionsGranted,
            input.Policy.AllowInProgressPrerequisites, AssumePermission: false);
        var pickedCodes = picked.Select(s => s.CourseCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var waiting = new List<string>();

        foreach (var s in picked.DistinctBy(s => s.CourseCode, StringComparer.OrdinalIgnoreCase))
        {
            var code = s.CourseCode;
            if (!catalog.TryGetValue(code, out var course)) { errors.Add(PlannerReason.Of(ReasonCode.UnknownSection, s.Id.ToString())); continue; }
            if (student.Passed.ContainsKey(code) || student.InProgress.Contains(code)) { errors.Add(PlannerReason.Of(ReasonCode.AlreadyTaken, [code])); continue; }
            if (!inPlan.Contains(code)) errors.Add(PlannerReason.Of(ReasonCode.NotInDegreePlan, [code]));

            var e = Evaluate(course.Prerequisite, code, state);
            if (e.Result == Sat.No)
            {
                errors.Add(PlannerReason.Of(ReasonCode.NotEligibleNow, [code]));
                errors.AddRange(e.Failures);
            }
            else if (e.Result == Sat.Conditional)
            {
                warnings.Add(PlannerReason.Of(ReasonCode.WaitingForGrade, e.WaitingFor.Prepend(code)));
                waiting.Add(code);
            }

            foreach (var co in e.PendingCoreqs.Where(c => !pickedCodes.Contains(c)))
                errors.Add(PlannerReason.Of(ReasonCode.CorequisiteMissing, [code, co]));

            if (s.SeatsLeft <= 0) errors.Add(PlannerReason.Of(ReasonCode.SectionFull, [code]));
            if (input.Availability is { Windows.Count: > 0 } a && !a.Fits(s.Meetings))
                warnings.Add(PlannerReason.Of(ReasonCode.OutsideAvailability, [code]));
        }

        var total = picked.DistinctBy(s => s.CourseCode, StringComparer.OrdinalIgnoreCase)
            .Sum(s => catalog.TryGetValue(s.CourseCode, out var c) ? c.Credits : 0);

        // Credit limits: the same rule the recommendation used (including the graduating exemption).
        var recommendation = Plan(input);
        var limits = recommendation.Limits;
        if (limits is null) errors.Add(PlannerReason.Of(ReasonCode.NoCreditRule));
        else if (total > recommendation.MaxCredits)
            errors.Add(PlannerReason.Of(ReasonCode.OverCreditLimit, recommendation.MaxCredits.ToString(CultureInfo.InvariantCulture)));
        else if (total < limits.MinCredits && picked.Count > 0)
            warnings.Add(PlannerReason.Of(ReasonCode.BelowMinimumCredits, limits.MinCredits.ToString(CultureInfo.InvariantCulture)));

        return new PlanCheckResult(total, limits, recommendation.MaxCredits, errors.Distinct().ToList(), warnings.Distinct().ToList());
    }

    /// <summary>A stable key for a warning, so the student's "I understand" can be matched on the server.</summary>
    public static string WarningKey(PlannerReason r) => $"{r.Code}:{string.Join(',', r.Courses)}:{r.Value}";
}
