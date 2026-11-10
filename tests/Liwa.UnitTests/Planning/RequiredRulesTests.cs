using Liwa.Domain.Academic;
using Liwa.Domain.Academic.Prerequisites;
using Liwa.Domain.Planning;

namespace Liwa.UnitTests.Planning;

/// <summary>The remaining tests from the brief's §8 list, written exactly as the brief words them.</summary>
public sealed class RequiredRulesTests
{
    private static MeetingTime M(string start, string end) =>
        new(DayOfWeek.Monday, TimeOnly.Parse(start, System.Globalization.CultureInfo.InvariantCulture), TimeOnly.Parse(end, System.Globalization.CultureInfo.InvariantCulture));

    [Fact]
    public void Back_to_back_sections_10_00_10_50_and_10_50_11_40_are_not_a_clash() =>
        Assert.False(M("10:00", "10:50").Overlaps(M("10:50", "11:40")));

    [Fact]
    public void A_one_minute_overlap_is_a_clash() =>
        Assert.True(M("10:00", "10:51").Overlaps(M("10:50", "11:40")));

    [Fact]
    public void On_probation_a_15_credit_selection_fails()
    {
        var s = new Scenario { Probation = true, Gpa = 1.8m };
        foreach (var c in new[] { "BUS201", "BUS202", "BUS203", "BUS204", "BUS205" }) s.Course(c).Section(c);
        var check = Planner.Check(s.Input(), s.Sections.Select(x => x.Id).ToList());
        Assert.Equal(15, check.TotalCredits);
        Assert.Contains(check.Errors, e => e.Code == ReasonCode.OverCreditLimit && e.Value == "12");
    }

    [Theory]
    [InlineData(3.50, 21)]
    [InlineData(3.35, 18)]
    public void Gpa_3_5_gets_21_credits_and_3_35_gets_18(double gpa, int max)
    {
        var r = new Scenario { Gpa = (decimal)gpa }.Course("ITC201").Section("ITC201").Run();
        Assert.Equal(max, r.MaxCredits);
    }

    [Fact]
    public void Selecting_one_half_of_a_corequisite_pair_alone_is_CorequisiteMissing()
    {
        var s = new Scenario().Course("CSC101", prereq: "COREQ(CSC101L)").Course("CSC101L", credits: 1, prereq: "COREQ(CSC101)")
            .Section("CSC101").Section("CSC101L");
        var check = Planner.Check(s.Input(), [s.Sections[0].Id]);
        Assert.Contains(check.Errors, e => e.Code == ReasonCode.CorequisiteMissing && e.Courses.SequenceEqual(["CSC101", "CSC101L"]));
        Assert.True(Planner.Check(s.Input(), s.Sections.Select(x => x.Id).ToList()).IsValid);
    }

    [Fact]
    public void A_corequisite_is_satisfied_by_the_same_term_selection_in_the_check() =>
        Assert.DoesNotContain(
            Planner.Check(new Scenario().Course("ITC202", prereq: "COREQ(ITC202L)").Course("ITC202L", credits: 1).Section("ITC202").Section("ITC202L") is var s ? s.Input() : null!,
                s.Sections.Select(x => x.Id).ToList()).Errors,
            e => e.Code == ReasonCode.CorequisiteMissing);

    [Fact]
    public void A_section_at_90_percent_gets_a_nearly_full_warning()
    {
        var s = new Scenario().Course("ITC201");
        s.Sections.Add(new PlannerSection(Guid.NewGuid(), "ITC201", "01", DeliveryMode.OnCampus,
            [new MeetingTime(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(10, 15))], SeatsLeft: 3, Capacity: 30));
        Assert.Contains(s.Run().Warnings, w => w.Code == ReasonCode.NearlyFull && w.Courses.SequenceEqual(["ITC201"]));
        Assert.Contains(Planner.Check(s.Input(), [s.Sections[0].Id]).Warnings, w => w.Code == ReasonCode.NearlyFull);
    }

    [Fact]
    public void Skipping_a_spring_only_course_offered_now_warns_it_waits_a_year()
    {
        // Credit cap 3: only one of the two fits; the spring-only one left out is flagged.
        var s = new Scenario { Preferences = new PlannerPreferences(3, DeliveryPreference.Any) }
            .Course("ITC201", prereq: null).Course("ITC390", offered: OfferedIn.Spring).Section("ITC201").Section("ITC390");
        var r = s.Run();
        var missed = r.Warnings.SingleOrDefault(w => w.Code == ReasonCode.FallOnlyMissed);
        if (r.Plan.Any(p => p.CourseCode == "ITC390")) Assert.Null(missed);
        else Assert.Contains("ITC390", missed!.Courses);
    }

    [Fact]
    public void A_term_can_switch_off_in_progress_prerequisites_and_the_reason_says_the_grade_must_be_posted()
    {
        var s = new Scenario { Term = new PlannerTerm("2027SP", TermSeason.Spring, 2027, AllowInProgressPrerequisites: false) }
            .Course("CSC201", prereq: "CSC102").Section("CSC201");
        s.InProgress.Add("CSC102");
        var r = s.Run();
        Assert.Contains(r.For("CSC201").Reasons, x => x.Code == ReasonCode.GradeMustBePosted && x.Courses.SequenceEqual(["CSC102"]));

        var on = new Scenario { Term = new PlannerTerm("2027SP", TermSeason.Spring, 2027, AllowInProgressPrerequisites: true) }
            .Course("CSC201", prereq: "CSC102").Section("CSC201");
        on.InProgress.Add("CSC102");
        Assert.Equal(CourseStatus.Planned, on.Run().For("CSC201").Status);
    }

    [Fact]
    public void Prerequisite_cycles_are_found_but_corequisite_pairs_are_not_cycles()
    {
        var rules = new Dictionary<string, Prerequisite?>
        {
            ["AAA101"] = PrerequisiteParser.Parse("CCC101"),
            ["BBB101"] = PrerequisiteParser.Parse("AAA101"),
            ["CCC101"] = PrerequisiteParser.Parse("BBB101 OR DDD101"),
            ["DDD101"] = null,
        };
        Assert.Equal(["AAA101", "CCC101", "BBB101", "AAA101"], PrerequisiteGraph.FindCycle(rules));

        var pair = new Dictionary<string, Prerequisite?> { ["CSC101"] = PrerequisiteParser.Parse("COREQ(CSC101L)"), ["CSC101L"] = PrerequisiteParser.Parse("COREQ(CSC101)") };
        Assert.Null(PrerequisiteGraph.FindCycle(pair));
    }
}
