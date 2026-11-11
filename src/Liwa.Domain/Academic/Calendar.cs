namespace Liwa.Domain.Academic;

/// <summary>Days of the week as flags, so a term's teaching days fit in one value.</summary>
[Flags]
public enum WeekDays
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,

    /// <summary>The UAE work week.</summary>
    MondayToFriday = Monday | Tuesday | Wednesday | Thursday | Friday,
}

public static class WeekDaysExtensions
{
    public static WeekDays ToFlag(this DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => WeekDays.Monday,
        DayOfWeek.Tuesday => WeekDays.Tuesday,
        DayOfWeek.Wednesday => WeekDays.Wednesday,
        DayOfWeek.Thursday => WeekDays.Thursday,
        DayOfWeek.Friday => WeekDays.Friday,
        DayOfWeek.Saturday => WeekDays.Saturday,
        DayOfWeek.Sunday => WeekDays.Sunday,
        _ => WeekDays.None,
    };

    public static bool Includes(this WeekDays days, DayOfWeek day) => (days & day.ToFlag()) != 0;
}

public enum TermSeason
{
    Fall = 1,
    Spring = 2,
    Summer = 3,
}

/// <summary>
/// Term codes: four-digit year + SP (Spring), SU (Summer) or FA (Fall), e.g. "2027SP".
/// Within a calendar year the order is Spring, Summer, Fall.
/// </summary>
public static class TermCodes
{
    public static bool TryParse(string? code, out int year, out TermSeason season)
    {
        year = 0;
        season = default;
        var c = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (c.Length != 6 || !int.TryParse(c.AsSpan(0, 4), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out year)) return false;
        switch (c[4..])
        {
            case "SP": season = TermSeason.Spring; return true;
            case "SU": season = TermSeason.Summer; return true;
            case "FA": season = TermSeason.Fall; return true;
            default: return false;
        }
    }

    public static bool IsValid(string? code) => TryParse(code, out _, out _);

    public static string Format(int year, TermSeason season) => season switch
    {
        TermSeason.Spring => $"{year}SP",
        TermSeason.Summer => $"{year}SU",
        _ => $"{year}FA",
    };

    /// <summary>A number that sorts terms chronologically; -1 for an invalid code.</summary>
    public static int SortKey(string? code) =>
        TryParse(code, out var year, out var season)
            ? year * 10 + season switch { TermSeason.Spring => 1, TermSeason.Summer => 2, _ => 3 }
            : -1;
}

/// <summary>Which seasons a course is normally offered in. Used for multi-term projections.</summary>
[Flags]
public enum OfferedIn
{
    None = 0,
    Fall = 1,
    Spring = 2,
    Summer = 4,
    FallAndSpring = Fall | Spring,
    All = Fall | Spring | Summer,
}

public static class OfferedInExtensions
{
    public static OfferedIn ToOffering(this TermSeason season) => season switch
    {
        TermSeason.Fall => OfferedIn.Fall,
        TermSeason.Spring => OfferedIn.Spring,
        TermSeason.Summer => OfferedIn.Summer,
        _ => OfferedIn.None,
    };

    public static bool Includes(this OfferedIn offering, TermSeason season) => (offering & season.ToOffering()) != 0;
}

/// <summary>
/// An academic term (e.g. Spring 2027). Teaching days are configurable per term; Saturday is optional.
/// Friday after 12:00 is not a teaching slot (Jumu'ah) unless <see cref="AllowFridayAfternoon"/> is set.
/// </summary>
public sealed class Term
{
    private Term() { } // EF Core

    public Term(Guid id, string code, string name, TermSeason season, int year, DateOnly startDate, DateOnly endDate)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Term code is required.", nameof(code));
        if (endDate <= startDate) throw new ArgumentException("A term must end after it starts.", nameof(endDate));
        Id = id;
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        Season = season;
        Year = year;
        StartDate = startDate;
        EndDate = endDate;
        TeachingDays = WeekDays.MondayToFriday;
    }

    public Guid Id { get; private set; }

    /// <summary>Stable key, e.g. "2027SP". Matches the CSV contract and (later) PeopleSoft STRM mapping.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public TermSeason Season { get; private set; }

    public int Year { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public WeekDays TeachingDays { get; private set; }

    public bool AllowFridayAfternoon { get; private set; }

    /// <summary>The term students are currently planning and registering for.</summary>
    public bool IsCurrent { get; private set; }

    /// <summary>
    /// This term's override of the credit policy's "in-progress prerequisites count" switch (brief §4); null = use the policy.
    /// </summary>
    public bool? AllowInProgressPrerequisites { get; private set; }

    public void SetInProgressPrerequisites(bool? allow) => AllowInProgressPrerequisites = allow;

    /// <summary>This term's normal load (credits the planner aims for); null = the credit policy's.</summary>
    public int? NormalLoad { get; private set; }

    public void SetNormalLoad(int? credits)
    {
        if (credits is < 0 or > 30) throw new ArgumentOutOfRangeException(nameof(credits), "A normal load must be between 0 and 30 credits.");
        NormalLoad = credits;
    }

    public void Update(string name, TermSeason season, int year, DateOnly startDate, DateOnly endDate)
    {
        if (endDate <= startDate) throw new ArgumentException("A term must end after it starts.", nameof(endDate));
        Name = name.Trim();
        Season = season;
        Year = year;
        StartDate = startDate;
        EndDate = endDate;
    }

    public void SetTeachingDays(WeekDays days, bool allowFridayAfternoon)
    {
        if ((days & WeekDays.Sunday) != 0) throw new ArgumentException("Sunday is a weekend day in the UAE and cannot be a teaching day.", nameof(days));
        if (days == WeekDays.None) throw new ArgumentException("A term needs at least one teaching day.", nameof(days));
        TeachingDays = days;
        AllowFridayAfternoon = allowFridayAfternoon;
    }

    public void SetCurrent(bool isCurrent) => IsCurrent = isCurrent;
}
