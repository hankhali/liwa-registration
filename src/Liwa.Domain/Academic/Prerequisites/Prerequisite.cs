namespace Liwa.Domain.Academic.Prerequisites;

/// <summary>
/// A parsed prerequisite expression. Text format (brief §8), case-insensitive keywords:
/// <list type="bullet">
/// <item><c>CSC101</c> — pass the course (any passing grade)</item>
/// <item><c>MTH110:C</c> — pass with at least the grade (MinGrade)</item>
/// <item><c>COREQ(ITC110L)</c> — take at the same time or before (Corequisite)</item>
/// <item><c>STANDING(Junior)</c> — Freshman, Sophomore, Junior or Senior</item>
/// <item><c>CREDITS(60)</c> — at least this many credits earned</item>
/// <item><c>PERMISSION</c> — department permission</item>
/// <item><c>A AND B</c>, <c>A OR B</c>, parentheses. AND binds tighter than OR.</item>
/// </list>
/// Example: <c>CSC101 AND (MTH102 OR MTH110:C)</c>. Deciding whether a student meets it is the planner's job (M3).
/// </summary>
public abstract record Prerequisite
{
    /// <summary>Every course code mentioned anywhere in the expression.</summary>
    public IEnumerable<string> CourseCodes() => this switch
    {
        CoursePassed c => [c.CourseCode],
        Corequisite c => [c.CourseCode],
        AllOf a => a.Items.SelectMany(i => i.CourseCodes()),
        AnyOf a => a.Items.SelectMany(i => i.CourseCodes()),
        _ => [],
    };

    /// <summary>Canonical text (round-trips through the parser).</summary>
    public abstract string ToText();
}

public sealed record CoursePassed(string CourseCode, string? MinGrade) : Prerequisite
{
    public override string ToText() => MinGrade is null ? CourseCode : $"{CourseCode}:{MinGrade}";
}

public sealed record Corequisite(string CourseCode) : Prerequisite
{
    public override string ToText() => $"COREQ({CourseCode})";
}

public sealed record StandingAtLeast(Standing Standing) : Prerequisite
{
    public override string ToText() => $"STANDING({Standing})";
}

public sealed record CreditsEarnedAtLeast(int Credits) : Prerequisite
{
    public override string ToText() => $"CREDITS({Credits})";
}

public sealed record DepartmentPermission : Prerequisite
{
    public override string ToText() => "PERMISSION";
}

public sealed record AllOf(IReadOnlyList<Prerequisite> Items) : Prerequisite
{
    public override string ToText() => string.Join(" AND ", Items.Select(i => i is AnyOf ? $"({i.ToText()})" : i.ToText()));

    public bool Equals(AllOf? other) => other is not null && Items.SequenceEqual(other.Items);

    public override int GetHashCode() => Items.Aggregate(17, (h, i) => HashCode.Combine(h, i));
}

public sealed record AnyOf(IReadOnlyList<Prerequisite> Items) : Prerequisite
{
    public override string ToText() => string.Join(" OR ", Items.Select(i => i.ToText()));

    public bool Equals(AnyOf? other) => other is not null && Items.SequenceEqual(other.Items);

    public override int GetHashCode() => Items.Aggregate(19, (h, i) => HashCode.Combine(h, i));
}

public sealed class PrerequisiteSyntaxException(string message, int position) : FormatException(message)
{
    /// <summary>Zero-based character position of the problem.</summary>
    public int Position { get; } = position;
}
