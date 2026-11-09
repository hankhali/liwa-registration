namespace Liwa.Domain.Academic;

/// <summary>A degree program, e.g. Bachelor of Information Technology (120 credits).</summary>
public sealed class AcademicProgram
{
    private AcademicProgram() { } // EF Core

    public AcademicProgram(Guid id, string code, string name, int totalCredits)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Program code is required.", nameof(code));
        Id = id;
        Code = code.Trim().ToUpperInvariant();
        Update(name, null, null, totalCredits);
    }

    public Guid Id { get; private set; }

    /// <summary>e.g. "BIT", "BBA-MGT", "BMC".</summary>
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? NameAr { get; private set; }

    public string? College { get; private set; }

    public int TotalCredits { get; private set; }

    public void Update(string name, string? nameAr, string? college, int totalCredits)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Program name is required.", nameof(name));
        if (totalCredits is <= 0 or > 300) throw new ArgumentOutOfRangeException(nameof(totalCredits), "Total credits must be between 1 and 300.");
        Name = name.Trim();
        NameAr = string.IsNullOrWhiteSpace(nameAr) ? null : nameAr.Trim();
        College = string.IsNullOrWhiteSpace(college) ? null : college.Trim();
        TotalCredits = totalCredits;
    }
}

public enum RequirementKind
{
    /// <summary>Every listed course must be taken.</summary>
    AllOf = 1,

    /// <summary>Take courses from the list until <see cref="RequirementGroup.CreditsRequired"/> is reached.</summary>
    ChooseCredits = 2,

    /// <summary>Any course from the catalog counts (free electives); the list may be empty.</summary>
    AnyCourse = 3,
}

/// <summary>A program's requirements for one catalog year (students follow the plan of the year they entered).</summary>
public sealed class DegreePlan
{
    private readonly List<RequirementGroup> _groups = [];

    private DegreePlan() { } // EF Core

    public DegreePlan(Guid id, Guid programId, int catalogYear)
    {
        if (catalogYear is < 2000 or > 2100) throw new ArgumentOutOfRangeException(nameof(catalogYear));
        Id = id;
        ProgramId = programId;
        CatalogYear = catalogYear;
    }

    public Guid Id { get; private set; }

    public Guid ProgramId { get; private set; }

    public int CatalogYear { get; private set; }

    public IReadOnlyList<RequirementGroup> Groups => _groups;

    public RequirementGroup AddGroup(string key, string name, RequirementKind kind, int creditsRequired, int sortOrder)
    {
        if (_groups.Any(g => string.Equals(g.Key, key.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Requirement group '{key}' already exists in this plan.");
        var group = new RequirementGroup(Guid.NewGuid(), Id, key, name, kind, creditsRequired, sortOrder);
        _groups.Add(group);
        return group;
    }

    public void ClearGroups() => _groups.Clear();

    /// <summary>Credits the plan adds up to; should equal the program's total.</summary>
    public int PlannedCredits(IReadOnlyDictionary<Guid, int> creditsByCourse) =>
        _groups.Sum(g => g.Kind == RequirementKind.AllOf
            ? g.Courses.Sum(c => creditsByCourse.GetValueOrDefault(c.CourseId))
            : g.CreditsRequired);
}

/// <summary>A block of a degree plan, e.g. "University requirements" or "Major electives (choose 15 credits)".</summary>
public sealed class RequirementGroup
{
    private readonly List<RequirementCourse> _courses = [];

    private RequirementGroup() { } // EF Core

    internal RequirementGroup(Guid id, Guid degreePlanId, string key, string name, RequirementKind kind, int creditsRequired, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Group key is required.", nameof(key));
        ArgumentOutOfRangeException.ThrowIfNegative(creditsRequired);
        Id = id;
        DegreePlanId = degreePlanId;
        Key = key.Trim().ToUpperInvariant();
        Name = string.IsNullOrWhiteSpace(name) ? Key : name.Trim();
        Kind = kind;
        CreditsRequired = creditsRequired;
        SortOrder = sortOrder;
    }

    public Guid Id { get; private set; }

    public Guid DegreePlanId { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public RequirementKind Kind { get; private set; }

    /// <summary>For <see cref="RequirementKind.ChooseCredits"/>/<see cref="RequirementKind.AnyCourse"/>; informational for AllOf.</summary>
    public int CreditsRequired { get; private set; }

    public int SortOrder { get; private set; }

    public IReadOnlyList<RequirementCourse> Courses => _courses;

    public void AddCourse(Guid courseId, int? suggestedSemester)
    {
        if (_courses.Any(c => c.CourseId == courseId)) return;
        if (suggestedSemester is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(suggestedSemester));
        _courses.Add(new RequirementCourse(Id, courseId, suggestedSemester));
    }
}

public sealed class RequirementCourse
{
    private RequirementCourse() { } // EF Core

    internal RequirementCourse(Guid groupId, Guid courseId, int? suggestedSemester)
    {
        GroupId = groupId;
        CourseId = courseId;
        SuggestedSemester = suggestedSemester;
    }

    public Guid GroupId { get; private set; }

    public Guid CourseId { get; private set; }

    /// <summary>Semester the plan suggests (1–8 for a four-year degree). Guidance only.</summary>
    public int? SuggestedSemester { get; private set; }
}
