namespace Liwa.Domain.Academic;

/// <summary>
/// Letter grades on a 4.0 scale (assumption, to confirm with the registrar). P (pass) earns credit without
/// affecting the GPA; W never counts; F counts in the GPA and earns no credit.
/// </summary>
public static class Grades
{
    private static readonly Dictionary<string, decimal?> Points = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A"] = 4.0m, ["A-"] = 3.7m,
        ["B+"] = 3.3m, ["B"] = 3.0m, ["B-"] = 2.7m,
        ["C+"] = 2.3m, ["C"] = 2.0m, ["C-"] = 1.7m,
        ["D+"] = 1.3m, ["D"] = 1.0m,
        ["F"] = 0.0m,
        ["P"] = null,
    };

    /// <summary>Order used to compare grades for MinGrade prerequisites (higher is better).</summary>
    private static readonly string[] Order = ["F", "D", "D+", "C-", "C", "C+", "B-", "B", "B+", "A-", "A"];

    public static bool IsKnown(string? grade) => grade is not null && Points.ContainsKey(grade.Trim());

    public static string Normalize(string grade) => grade.Trim().ToUpperInvariant();

    /// <summary>Grade points, or null for grades that don't affect the GPA (P).</summary>
    public static decimal? PointsFor(string grade) => Points[grade.Trim()];

    /// <summary>Earns credit toward the degree.</summary>
    public static bool IsPassing(string grade) => !string.Equals(grade.Trim(), "F", StringComparison.OrdinalIgnoreCase) && IsKnown(grade);

    /// <summary>True when <paramref name="grade"/> is at least <paramref name="minimum"/> (P satisfies any minimum except where the registrar says otherwise).</summary>
    public static bool MeetsMinimum(string grade, string minimum)
    {
        if (string.Equals(grade.Trim(), "P", StringComparison.OrdinalIgnoreCase)) return true;
        var g = Array.IndexOf(Order, Normalize(grade));
        var m = Array.IndexOf(Order, Normalize(minimum));
        return g >= 0 && m >= 0 && g >= m;
    }

    public static bool IsLetterGrade(string grade) => Array.IndexOf(Order, Normalize(grade)) >= 0;
}

/// <summary>Which attempt counts when a course is repeated (configurable in the credit policy).</summary>
public enum RepeatGradeRule
{
    LatestCounts = 1,
    HighestCounts = 2,
}

/// <summary>GPA, credits and standing derived from a transcript. Pure: no database, no clock.</summary>
public sealed record AcademicSummary(decimal? Gpa, int CreditsEarned, int GpaCredits, int InProgressCredits, Standing Standing)
{
    public static AcademicSummary From(StudentRecord record, RepeatGradeRule rule)
    {
        // Pick the counting attempt per course among completed ones.
        var counting = record.Transcript
            .Where(t => t.Status == TranscriptStatus.Completed && t.Grade is not null)
            .GroupBy(t => t.CourseId)
            .Select(g => rule == RepeatGradeRule.LatestCounts
                ? g.OrderBy(t => TermCodes.SortKey(t.TermCode)).Last()
                : g.OrderBy(t => Grades.PointsFor(t.Grade!) ?? -1m).ThenBy(t => TermCodes.SortKey(t.TermCode)).Last())
            .ToList();

        var gpaAttempts = counting.Where(t => Grades.PointsFor(t.Grade!) is not null).ToList();
        var gpaCredits = gpaAttempts.Sum(t => t.Credits);
        decimal? gpa = gpaCredits == 0
            ? null
            : Math.Round(gpaAttempts.Sum(t => Grades.PointsFor(t.Grade!)!.Value * t.Credits) / gpaCredits, 2, MidpointRounding.AwayFromZero);

        var earned = counting.Where(t => Grades.IsPassing(t.Grade!)).Sum(t => t.Credits)
                     + record.TransferCredits.Where(tc => counting.All(c => c.CourseId != tc.CourseId)).Sum(tc => tc.Credits);
        var inProgress = record.Transcript.Where(t => t.Status == TranscriptStatus.InProgress).Sum(t => t.Credits);

        return new AcademicSummary(gpa, earned, gpaCredits, inProgress, StandingFor(earned));
    }

    /// <summary>Freshman &lt;30, Sophomore 30–59, Junior 60–89, Senior 90+ credits earned (assumption).</summary>
    public static Standing StandingFor(int creditsEarned) => creditsEarned switch
    {
        >= 90 => Standing.Senior,
        >= 60 => Standing.Junior,
        >= 30 => Standing.Sophomore,
        _ => Standing.Freshman,
    };
}
