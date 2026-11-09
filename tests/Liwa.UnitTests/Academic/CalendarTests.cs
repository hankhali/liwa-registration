using Liwa.Domain.Academic;

namespace Liwa.UnitTests.Academic;

/// <summary>UAE calendar rules (brief §4).</summary>
public sealed class CalendarTests
{
    private static Term Spring() => new(Guid.NewGuid(), "2027SP", "Spring 2027", TermSeason.Spring, 2027, new(2027, 1, 11), new(2027, 5, 7));

    private static MeetingTime M(DayOfWeek d, int h1, int m1, int h2, int m2) => new(d, new TimeOnly(h1, m1), new TimeOnly(h2, m2));

    [Fact]
    public void A_normal_monday_wednesday_section_is_fine() =>
        Assert.Empty(MeetingRules.Validate(Spring(), DeliveryMode.OnCampus, [M(DayOfWeek.Monday, 17, 0, 18, 15), M(DayOfWeek.Wednesday, 17, 0, 18, 15)]));

    [Fact]
    public void Sunday_is_never_a_teaching_day()
    {
        var problems = MeetingRules.Validate(Spring(), DeliveryMode.OnCampus, [M(DayOfWeek.Sunday, 9, 0, 10, 15), M(DayOfWeek.Tuesday, 9, 0, 10, 15), M(DayOfWeek.Thursday, 9, 0, 10, 15)]);
        Assert.Contains(problems, p => p.Contains("Sunday is a weekend day", StringComparison.Ordinal));
        Assert.Contains(problems, p => p.Contains("Sunday/Tuesday/Thursday", StringComparison.Ordinal));
    }

    [Fact]
    public void Friday_after_noon_is_blocked_unless_the_term_allows_it()
    {
        var term = Spring();
        MeetingTime[] lateFriday = [M(DayOfWeek.Friday, 11, 0, 12, 15)];
        Assert.Contains(MeetingRules.Validate(term, DeliveryMode.OnCampus, lateFriday), p => p.Contains("Jumu'ah", StringComparison.Ordinal));
        Assert.Empty(MeetingRules.Validate(term, DeliveryMode.OnCampus, [M(DayOfWeek.Friday, 11, 0, 11, 50)]));

        term.SetTeachingDays(WeekDays.MondayToFriday, allowFridayAfternoon: true);
        Assert.Empty(MeetingRules.Validate(term, DeliveryMode.OnCampus, lateFriday));
    }

    [Fact]
    public void Saturday_only_counts_when_the_term_turns_it_on()
    {
        var term = Spring();
        MeetingTime[] saturday = [M(DayOfWeek.Saturday, 9, 0, 11, 50)];
        Assert.Contains(MeetingRules.Validate(term, DeliveryMode.OnCampus, saturday), p => p.Contains("not a teaching day", StringComparison.Ordinal));

        term.SetTeachingDays(WeekDays.MondayToFriday | WeekDays.Saturday, false);
        Assert.Empty(MeetingRules.Validate(term, DeliveryMode.OnCampus, saturday));
    }

    [Fact]
    public void A_term_cannot_teach_on_sunday() =>
        Assert.Throws<ArgumentException>(() => Spring().SetTeachingDays(WeekDays.MondayToFriday | WeekDays.Sunday, false));

    [Fact]
    public void Online_sections_may_have_no_meetings_but_campus_ones_need_some()
    {
        Assert.Empty(MeetingRules.Validate(Spring(), DeliveryMode.Online, []));
        Assert.NotEmpty(MeetingRules.Validate(Spring(), DeliveryMode.OnCampus, []));
    }

    [Fact]
    public void Sections_that_share_a_time_clash()
    {
        var a = new Section(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "01");
        var b = new Section(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "01");
        var c = new Section(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "01");
        a.SetMeetings([M(DayOfWeek.Monday, 9, 0, 10, 15)]);
        b.SetMeetings([M(DayOfWeek.Monday, 10, 0, 11, 15)]);
        c.SetMeetings([M(DayOfWeek.Monday, 10, 15, 11, 30)]);

        Assert.True(a.ClashesWith(b));
        Assert.False(a.ClashesWith(c)); // back-to-back is fine
    }

    [Theory]
    [InlineData("2027SP", true)]
    [InlineData("2026fa", true)]
    [InlineData("2027WI", false)]
    [InlineData("27SP", false)]
    public void Term_codes_are_year_plus_season(string code, bool valid) => Assert.Equal(valid, TermCodes.IsValid(code));

    [Fact]
    public void Term_codes_sort_spring_summer_fall_within_a_year()
    {
        string[] codes = ["2027FA", "2027SP", "2026FA", "2027SU"];
        Assert.Equal(["2026FA", "2027SP", "2027SU", "2027FA"], codes.OrderBy(TermCodes.SortKey));
    }
}
