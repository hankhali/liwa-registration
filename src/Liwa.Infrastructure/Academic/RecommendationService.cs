using Liwa.Domain.Academic;
using Liwa.Domain.Planning;

namespace Liwa.Infrastructure.Academic;

/// <summary>Turns a transcript into the facts the pure planner uses.</summary>
internal static class PlannerFacts
{
    /// <summary>Reduces a transcript to the facts the planner uses, applying the repeat-grade rule.</summary>
    internal static PlannerStudent StudentFacts(StudentRecord record, RepeatGradeRule rule, IReadOnlyDictionary<Guid, string> codeById, int totalCredits)
    {
        var counting = record.Transcript
            .Where(t => t.Status == TranscriptStatus.Completed && t.Grade is not null)
            .GroupBy(t => t.CourseId)
            .Select(g => rule == RepeatGradeRule.LatestCounts
                ? g.OrderBy(t => TermCodes.SortKey(t.TermCode)).Last()
                : g.OrderBy(t => Grades.PointsFor(t.Grade!) ?? -1m).ThenBy(t => TermCodes.SortKey(t.TermCode)).Last())
            .ToList();

        var passed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in counting.Where(t => Grades.IsPassing(t.Grade!))) passed[codeById[t.CourseId]] = t.Grade!;
        foreach (var t in record.TransferCredits) passed.TryAdd(codeById[t.CourseId], "TR");

        var failed = counting.Where(t => !Grades.IsPassing(t.Grade!)).Select(t => codeById[t.CourseId]).Where(c => !passed.ContainsKey(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var inProgress = record.Transcript.Where(t => t.Status == TranscriptStatus.InProgress).Select(t => codeById[t.CourseId])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var summary = AcademicSummary.From(record, rule);

        return new PlannerStudent(passed, failed, inProgress, summary.CreditsEarned, summary.Gpa, record.Status == AcademicStatus.Probation,
            totalCredits, new HashSet<string>(StringComparer.OrdinalIgnoreCase), record.Hold);
    }
}
