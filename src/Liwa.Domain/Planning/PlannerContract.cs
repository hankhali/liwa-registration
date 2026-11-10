using Liwa.Domain.Academic;
using Liwa.Domain.Academic.Prerequisites;

namespace Liwa.Domain.Planning;

// ============================================================================================
// Planner contract (see docs/planner.md). The planner is deterministic and pure: everything it
// needs arrives in PlannerInput (including "today" and the term), nothing is read from a database
// or a clock, and the same input always gives the same result.
// ============================================================================================

/// <summary>Everything the planner needs to recommend sections for one term.</summary>
public sealed record PlannerInput(
    DateOnly Today,
    PlannerTerm Term,
    PlannerStudent Student,
    IReadOnlyList<PlannerCourse> Catalog,
    IReadOnlyList<PlannerRequirementGroup> DegreePlan,
    IReadOnlyList<PlannerSection> Sections,
    CreditPolicy Policy,
    Availability? Availability,
    PlannerPreferences Preferences);

/// <summary>The term being planned (normally the current term).</summary>
/// <param name="AllowInProgressPrerequisites">This term's override of the policy switch (null = use the policy).</param>
public sealed record PlannerTerm(string Code, TermSeason Season, int Year, bool? AllowInProgressPrerequisites = null);

/// <summary>
/// What the student has done, already reduced to facts (the repeat-grade rule has been applied).
/// </summary>
/// <param name="Passed">Course code → counting grade for every passed course. Transfer credit uses "TR".</param>
/// <param name="Failed">Courses whose counting attempt is an F (and not passed later).</param>
/// <param name="InProgress">Courses currently being taken (no final grade yet).</param>
/// <param name="CreditsEarned">Credits earned toward the degree (completed + transfer).</param>
/// <param name="Gpa">Cumulative GPA (2 decimals), or null with no grades yet.</param>
/// <param name="OnProbation">Academic probation, as reported by the registrar.</param>
/// <param name="TotalCreditsRequired">Credits the program needs (e.g. 120).</param>
/// <param name="PermissionsGranted">Courses for which department permission has been given.</param>
/// <param name="Hold">A registrar hold on the account (e.g. "FINANCE"), or null.</param>
public sealed record PlannerStudent(
    IReadOnlyDictionary<string, string> Passed,
    IReadOnlySet<string> Failed,
    IReadOnlySet<string> InProgress,
    int CreditsEarned,
    decimal? Gpa,
    bool OnProbation,
    int TotalCreditsRequired,
    IReadOnlySet<string> PermissionsGranted,
    string? Hold = null);

public sealed record PlannerCourse(string Code, int Credits, Prerequisite? Prerequisite, OfferedIn Offered);

public sealed record PlannerRequirementGroup(string Key, string Name, RequirementKind Kind, int CreditsRequired, IReadOnlyList<PlannerRequirementCourse> Courses);

public sealed record PlannerRequirementCourse(string Code, int? SuggestedSemester);

/// <param name="Capacity">Total seats (0 = unknown), used for the NearlyFull warning at 90%.</param>
public sealed record PlannerSection(Guid Id, string CourseCode, string SectionCode, DeliveryMode Delivery, IReadOnlyList<MeetingTime> Meetings, int SeatsLeft,
    int Capacity = 0);

/// <summary>When the student is free. A meeting fits if it lies entirely inside one window on its day.</summary>
public sealed record Availability(IReadOnlyList<TimeWindow> Windows)
{
    public bool Fits(IReadOnlyList<MeetingTime> meetings) =>
        meetings.All(m => Windows.Any(w => w.Day == m.Day && w.Start <= m.Start && m.End <= w.End));
}

public sealed record TimeWindow(DayOfWeek Day, TimeOnly Start, TimeOnly End);

public enum DeliveryPreference
{
    Any = 1,

    /// <summary>On-campus and hybrid sections only.</summary>
    OnCampusOnly = 2,

    OnlineOnly = 3,
}

/// <param name="MaxCredits">The student's own cap; can only lower the policy maximum, never raise it.</param>
public sealed record PlannerPreferences(int? MaxCredits, DeliveryPreference Delivery)
{
    public static PlannerPreferences None { get; } = new(null, DeliveryPreference.Any);
}

// ----------------------------------------------------------------------------------- output

public sealed record PlannerResult(
    string TermCode,
    CreditLimits? Limits,
    int MaxCredits,
    IReadOnlyList<PlannedCourse> Plan,
    int TotalCredits,
    IReadOnlyList<CourseAssessment> Courses,
    IReadOnlyList<PlannerReason> Warnings,
    GraduationProjection Projection);

/// <summary>A recommended section, why it was chosen, and the sections that lost (and why).</summary>
public sealed record PlannedCourse(
    string CourseCode,
    int Credits,
    Guid SectionId,
    string SectionCode,
    int PriorityRank,
    IReadOnlyList<PlannerReason> Reasons,
    IReadOnlyList<RejectedSection> Alternatives);

public sealed record RejectedSection(Guid SectionId, string SectionCode, IReadOnlyList<PlannerReason> Reasons);

public enum CourseStatus
{
    /// <summary>In the recommended plan.</summary>
    Planned = 1,

    /// <summary>Could be taken this term but didn't make the plan (credit limit, clashes, availability…).</summary>
    Eligible = 2,

    /// <summary>Could be taken if a condition holds (e.g. passing a course in progress).</summary>
    EligibleWithCondition = 3,

    NotEligible = 4,
}

/// <summary>Every course still needed for the degree, in priority order, with the planner's reasons.</summary>
public sealed record CourseAssessment(
    string CourseCode,
    int Credits,
    string GroupKey,
    CourseStatus Status,
    int PriorityRank,
    int Unlocks,
    int ChainLength,
    IReadOnlyList<PlannerReason> Reasons);

/// <summary>
/// A machine-readable reason. The UI turns each code into a plain sentence (for students) or a detailed
/// one (for advisors), in English or Arabic. <see cref="Courses"/> and <see cref="Value"/> fill the sentence.
/// </summary>
public sealed record PlannerReason(ReasonCode Code, IReadOnlyList<string> Courses, string? Value = null)
{
    public static PlannerReason Of(ReasonCode code, string? value = null) => new(code, [], value);

    public static PlannerReason Of(ReasonCode code, IEnumerable<string> courses, string? value = null) => new(code, courses.ToList(), value);

    public bool Equals(PlannerReason? other) => other is not null && Code == other.Code && Value == other.Value && Courses.SequenceEqual(other.Courses);

    public override int GetHashCode() => HashCode.Combine(Code, Value, Courses.Count);
}

public enum ReasonCode
{
    // Why a course is recommended / ranked high
    RequiredForDegree = 1,
    CountsTowardElectives = 2,
    UnlocksOtherCourses = 3,      // Courses = what it unlocks; Value = how many
    SuggestedForThisSemester = 4, // Value = suggested semester
    OnlyOfferedIn = 5,            // Value = seasons, e.g. "Fall"
    TakeWithCorequisite = 6,      // Courses = the corequisite planned alongside

    // Conditions (eligible with condition)
    WaitingForGrade = 20,         // Courses = [the course, then the prerequisites in progress]

    // Why a course can't be taken this term
    PrerequisiteNotMet = 40,      // Courses = what's missing (any one of them, for OR); Value = rule text
    PrerequisiteFailed = 41,      // Courses = failed prerequisite; Value = minimum grade if any
    MinimumGradeNotMet = 42,      // Courses = course; Value = minimum grade
    StandingRequired = 43,        // Value = standing
    CreditsRequired = 44,         // Value = credits
    PermissionRequired = 45,
    NotOfferedThisTerm = 46,      // Value = seasons it is offered
    NoSectionsThisTerm = 47,
    CorequisiteDoesNotFit = 48,   // Courses = the corequisite that can't be scheduled with it
    GradeMustBePosted = 49,       // Courses = prerequisite in progress, while in-progress prerequisites are switched off

    // Why it didn't make the plan although eligible
    CreditLimitReached = 60,      // Value = maximum credits
    ElectiveQuotaMet = 61,        // Value = group name
    NoSectionFits = 62,           // every section was ruled out (see section reasons)

    // Why a section lost
    OutsideAvailability = 80,
    ClashesWith = 81,             // Courses = clashing planned course
    SectionFull = 82,
    DeliveryNotPreferred = 83,    // Value = delivery mode
    FewerSeatsLeft = 84,          // Value = chosen section code (advisor detail)

    // Plan-level warnings
    BelowMinimumCredits = 100,    // Value = minimum
    NoCreditRule = 101,
    OfferedButNoSections = 102,   // Courses = courses offered this season with no sections
    InProgressPrerequisite = 103, // Courses = planned courses that depend on a grade in progress
    GraduationNotReachable = 104,
    AccountHold = 105,            // Value = hold type (e.g. FINANCE): plan can be made, registration waits until it's cleared
    FallOnlyMissed = 106,         // Courses = single-season courses offered now but not planned; Value = this season (skipping waits a year)
    NearlyFull = 107,             // Courses = planned courses whose section is at least 90% full
    PermissionAssumed = 108,      // Courses = later courses that need department permission; the projection assumes it is granted

    // Checking a plan the student changed by hand (Planner.Check)
    NotInDegreePlan = 120,        // Courses = course
    AlreadyTaken = 121,           // Courses = course (passed or in progress)
    DuplicateCourse = 122,        // Courses = course chosen twice
    CorequisiteMissing = 123,     // Courses = [course, missing corequisite]
    UnknownSection = 124,
    OverCreditLimit = 125,        // Value = maximum credits
    NotEligibleNow = 126,         // Courses = course; see the course's own reasons
}

/// <summary>Result of checking a hand-picked plan: errors block saving; warnings need an "I understand" at submit.</summary>
public sealed record PlanCheckResult(int TotalCredits, CreditLimits? Limits, int MaxCredits, IReadOnlyList<PlannerReason> Errors, IReadOnlyList<PlannerReason> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>When the student would graduate if they follow the recommendation, with and without summer terms.</summary>
public sealed record GraduationProjection(TermProjection WithSummer, TermProjection WithoutSummer);

/// <param name="GraduationTerm">Term code of the last term needed; null if not reachable within the planning horizon.</param>
/// <param name="TermsRemaining">Terms needed including the one being planned.</param>
public sealed record TermProjection(string? GraduationTerm, int TermsRemaining, IReadOnlyList<ProjectedTerm> Terms);

/// <param name="ElectiveCredits">Free-elective credits assumed in that term (any course counts).</param>
public sealed record ProjectedTerm(string TermCode, IReadOnlyList<string> Courses, int Credits, int ElectiveCredits);
