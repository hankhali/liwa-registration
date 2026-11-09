using System.Text.RegularExpressions;

namespace Liwa.Domain.Academic;

/// <summary>A catalog course, e.g. ITC201 Database Systems (3 credits).</summary>
public sealed partial class Course
{
    public const int MinCredits = 0;
    public const int MaxCredits = 6;

    private Course() { } // EF Core

    public Course(Guid id, string code, string title, int credits)
    {
        Id = id;
        Code = NormalizeCode(code);
        SetDetails(title, null, credits, null, null);
        Offered = OfferedIn.FallAndSpring;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    /// <summary>Subject prefix + number, upper-case, e.g. "ITC201" or "ITC110L". Unique.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Subject prefix, e.g. "ITC".</summary>
    public string Subject => SubjectOf(Code);

    public string Title { get; private set; } = string.Empty;

    public string? TitleAr { get; private set; }

    public int Credits { get; private set; }

    public string? Department { get; private set; }

    public string? Description { get; private set; }

    /// <summary>
    /// Prerequisite expression in the documented text format, e.g. <c>CSC101 AND (MTH102 OR MTH110:C)</c>.
    /// Parsed and validated on import (see <see cref="Prerequisites.PrerequisiteParser"/>).
    /// </summary>
    public string? Prerequisites { get; private set; }

    public OfferedIn Offered { get; private set; }

    public bool IsActive { get; private set; }

    public void SetDetails(string title, string? titleAr, int credits, string? department, string? description)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Course title is required.", nameof(title));
        if (credits is < MinCredits or > MaxCredits) throw new ArgumentOutOfRangeException(nameof(credits), $"Credits must be between {MinCredits} and {MaxCredits}.");
        Title = title.Trim();
        TitleAr = Clean(titleAr);
        Credits = credits;
        Department = Clean(department);
        Description = Clean(description);
    }

    public void SetPrerequisites(string? expression) => Prerequisites = Clean(expression);

    public void SetOffering(OfferedIn offered) => Offered = offered;

    public void SetActive(bool active) => IsActive = active;

    public static bool IsValidCode(string? code) => code is not null && CodePattern().IsMatch(code.Trim().ToUpperInvariant());

    public static string NormalizeCode(string code)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (!CodePattern().IsMatch(normalized)) throw new ArgumentException($"'{code}' is not a valid course code (e.g. ITC201 or ITC110L).", nameof(code));
        return normalized;
    }

    public static string SubjectOf(string code) => new(code.TakeWhile(char.IsLetter).ToArray());

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>2–4 letters, 3 digits, optional letter suffix (L = lab).</summary>
    [GeneratedRegex("^[A-Z]{2,4}[0-9]{3}[A-Z]?$")]
    private static partial Regex CodePattern();
}
