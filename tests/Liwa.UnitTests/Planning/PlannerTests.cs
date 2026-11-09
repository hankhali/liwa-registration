using System.Text.Json;
using Liwa.Domain.Academic;
using Liwa.Domain.Academic.Prerequisites;
using Liwa.Domain.Planning;

namespace Liwa.UnitTests.Planning;

/// <summary>A small world to plan in: catalog, degree plan, sections, student facts.</summary>
internal sealed class Scenario
{
    public List<PlannerCourse> Catalog { get; } = [];

    public List<PlannerRequirementCourse> Core { get; } = [];

    public List<PlannerRequirementCourse> Electives { get; } = [];

    public int ElectiveCredits { get; set; }

    public int FreeCredits { get; set; }

    public List<PlannerSection> Sections { get; } = [];

    public Dictionary<string, string> Passed { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> Failed { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> InProgress { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> Permissions { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int Earned { get; set; } = 30;

    public decimal? Gpa { get; set; } = 3.0m;

    public bool Probation { get; set; }

    public PlannerTerm Term { get; set; } = new("2027SP", TermSeason.Spring, 2027);

    public Availability? Availability { get; set; }

    public PlannerPreferences Preferences { get; set; } = PlannerPreferences.None;

    public CreditPolicy Policy { get; set; } = DefaultPolicy();

    public static CreditPolicy DefaultPolicy()
    {
        var p = new CreditPolicy(Guid.NewGuid(), "Test");
        p.ReplaceRules([
            new CreditLimitRule(Guid.NewGuid(), 1, "Summer, on probation", 0, 6).When(season: TermSeason.Summer, onProbation: true),
            new CreditLimitRule(Guid.NewGuid(), 2, "Summer", 0, 9).When(season: TermSeason.Summer),
            new CreditLimitRule(Guid.NewGuid(), 3, "On academic probation", 9, 12).When(onProbation: true),
            new CreditLimitRule(Guid.NewGuid(), 4, "High GPA (3.50 and above)", 12, 21).When(minGpa: 3.50m),
            new CreditLimitRule(Guid.NewGuid(), 5, "Standard full-time load", 12, 18),
        ]);
        return p;
    }

    /// <summary>Adds a course to the catalog and (unless elective/notInPlan) to the required group.</summary>
    public Scenario Course(string code, int credits = 3, string? prereq = null, OfferedIn offered = OfferedIn.FallAndSpring,
        bool elective = false, bool notInPlan = false, int? semester = null)
    {
        Catalog.Add(new PlannerCourse(code, credits, PrerequisiteParser.Parse(prereq), offered));
        if (notInPlan) return this;
        (elective ? Electives : Core).Add(new PlannerRequirementCourse(code, semester));
        return this;
    }

    private int _slot;

    /// <summary>Adds a section; without explicit times each section gets its own non-clashing daytime slot.</summary>
    public Scenario Section(string course, string section = "01", string? days = "Mon;Wed", string? start = null, string? end = null,
        int seats = 10, DeliveryMode delivery = DeliveryMode.OnCampus)
    {
        if (start is null && days is not null)
        {
            // Mon/Wed or Tue/Thu, 8:00–20:00 in 75-minute blocks: plenty of non-clashing slots.
            var i = _slot++;
            days = i % 2 == 0 ? "Mon;Wed" : "Tue;Thu";
            var startMin = 8 * 60 + (i / 2) * 75;
            start = $"{startMin / 60:00}:{startMin % 60:00}";
            var endMin = startMin + 60;
            end = $"{endMin / 60:00}:{endMin % 60:00}";
        }

        var meetings = days is null
            ? new List<MeetingTime>()
            : days.Split(';').Select(d => new MeetingTime(Enum.Parse<DayOfWeek>(Expand(d)), TimeOnly.Parse(start!, System.Globalization.CultureInfo.InvariantCulture), TimeOnly.Parse(end!, System.Globalization.CultureInfo.InvariantCulture))).ToList();
        Sections.Add(new PlannerSection(Guid.NewGuid(), course, section, delivery, meetings, seats));
        return this;
    }

    private static string Expand(string d) => d switch
    {
        "Mon" => "Monday", "Tue" => "Tuesday", "Wed" => "Wednesday", "Thu" => "Thursday", "Fri" => "Friday", "Sat" => "Saturday", _ => d,
    };

    public PlannerInput Input()
    {
        List<PlannerRequirementGroup> groups = [new("CORE", "Major requirements", RequirementKind.AllOf, 0, Core)];
        if (Electives.Count > 0) groups.Add(new("ELEC", "Major electives", RequirementKind.ChooseCredits, ElectiveCredits, Electives));
        if (FreeCredits > 0) groups.Add(new("FREE", "Free electives", RequirementKind.AnyCourse, FreeCredits, []));
        return new PlannerInput(
            new DateOnly(2026, 11, 9),
            Term,
            new PlannerStudent(Passed, Failed, InProgress, Earned, Gpa, Probation, 120, Permissions),
            Catalog, groups, Sections, Policy, Availability, Preferences);
    }

    public PlannerResult Run() => Planner.Plan(Input());

    public static Availability EveningsMonToThu() => new([
        new(DayOfWeek.Monday, new(17, 0), new(20, 0)), new(DayOfWeek.Tuesday, new(17, 0), new(20, 0)),
        new(DayOfWeek.Wednesday, new(17, 0), new(20, 0)), new(DayOfWeek.Thursday, new(17, 0), new(20, 0))]);
}

internal static class ResultExtensions
{
    public static CourseAssessment For(this PlannerResult r, string code) => r.Courses.Single(c => c.CourseCode == code);

    public static bool Has(this IEnumerable<PlannerReason> reasons, ReasonCode code) => reasons.Any(x => x.Code == code);

    public static PlannerReason Get(this IEnumerable<PlannerReason> reasons, ReasonCode code) => reasons.First(x => x.Code == code);

    public static string[] PlannedCodes(this PlannerResult r) => r.Plan.Select(p => p.CourseCode).Order(StringComparer.Ordinal).ToArray();
}

/// <summary>The five tests the brief requires (§8).</summary>
public sealed class PlannerRequiredTests
{
    [Fact]
    public void A_fall_only_course_correctly_adds_a_year_when_missed()
    {
        Scenario Build(PlannerTerm term) => new Scenario { Term = term }
            .Course("ITC301")                                            // Fall and Spring
            .Course("ITC401", prereq: "ITC301", offered: OfferedIn.Fall) // Fall only, needs ITC301 first
            .Section("ITC301");

        // Planning in Spring: ITC301 now, ITC401 in the coming Fall.
        var spring = Build(new PlannerTerm("2027SP", TermSeason.Spring, 2027)).Run();
        // Planning in Fall: ITC301 now, but ITC401 can't be taken until the NEXT Fall.
        var fall = Build(new PlannerTerm("2027FA", TermSeason.Fall, 2027)).Run();

        Assert.Equal(["ITC301"], spring.PlannedCodes());
        Assert.Equal("2027FA", spring.Projection.WithoutSummer.GraduationTerm);
        Assert.Equal("2028FA", fall.Projection.WithoutSummer.GraduationTerm);
        Assert.Equal("2028FA", fall.Projection.WithSummer.GraduationTerm); // summer doesn't help: not offered then
        Assert.Contains(ReasonCode.NotOfferedThisTerm, spring.For("ITC401").Reasons.Select(r => r.Code));
    }

    [Fact]
    public void The_probation_credit_cap_is_applied()
    {
        var s = new Scenario { Probation = true, Gpa = 1.80m };
        foreach (var code in new[] { "BUS101", "BUS110", "BUS120", "BUS205", "BUS210", "BUS230" }) s.Course(code).Section(code);

        var r = s.Run();

        Assert.Equal("On academic probation", r.Limits!.RuleName);
        Assert.Equal(12, r.MaxCredits);
        Assert.Equal(12, r.TotalCredits);
        Assert.Equal(4, r.Plan.Count);
        Assert.Equal(2, r.Courses.Count(c => c.Reasons.Has(ReasonCode.CreditLimitReached)));
    }

    [Fact]
    public void A_student_exactly_at_the_gpa_3_50_boundary_gets_the_21_credit_cap()
    {
        Scenario Build(decimal gpa)
        {
            var s = new Scenario { Gpa = gpa };
            for (var i = 1; i <= 8; i++) s.Course($"ITC{300 + i}").Section($"ITC{300 + i}");
            return s;
        }

        var atBoundary = Build(3.50m).Run();
        var justBelow = Build(3.49m).Run();

        Assert.Equal(21, atBoundary.MaxCredits);
        Assert.Equal(21, atBoundary.TotalCredits);
        Assert.Equal(7, atBoundary.Plan.Count);
        Assert.Equal(18, justBelow.MaxCredits);
        Assert.Equal(18, justBelow.TotalCredits);
    }

    [Fact]
    public void A_corequisite_pair_where_only_one_section_fits_availability_is_rejected_as_a_pair_with_a_clear_issue()
    {
        var r = new Scenario { Availability = Scenario.EveningsMonToThu() }
            .Course("ITC202", prereq: "COREQ(ITC202L)")
            .Course("ITC202L", credits: 1, prereq: "COREQ(ITC202)")
            .Section("ITC202", "01", "Mon;Wed", "09:00", "10:15")
            .Section("ITC202", "E1", "Mon;Wed", "17:00", "18:15")  // the lecture fits evenings…
            .Section("ITC202L", "L1", "Wed", "14:00", "16:50")     // …but the only lab is in the afternoon
            .Run();

        Assert.Empty(r.Plan);
        var lecture = r.For("ITC202");
        var lab = r.For("ITC202L");
        Assert.Equal(["ITC202L"], lecture.Reasons.Get(ReasonCode.CorequisiteDoesNotFit).Courses);
        Assert.Equal(["ITC202"], lab.Reasons.Get(ReasonCode.CorequisiteDoesNotFit).Courses);
        Assert.True(lab.Reasons.Has(ReasonCode.OutsideAvailability));
    }

    [Fact]
    public void A_graduating_senior_with_6_credits_left_is_not_forced_to_the_12_credit_minimum()
    {
        var s = new Scenario { Earned = 114, Gpa = 3.35m };
        s.Course("ITC304", offered: OfferedIn.Spring).Section("ITC304");
        s.Course("ITC350").Section("ITC350");

        var r = s.Run();

        Assert.Equal(6, r.Limits!.MinCredits);
        Assert.Equal(6, r.TotalCredits);
        Assert.False(r.Warnings.Has(ReasonCode.BelowMinimumCredits));
        Assert.Equal("2027SP", r.Projection.WithoutSummer.GraduationTerm);
        Assert.Equal(1, r.Projection.WithoutSummer.TermsRemaining);
    }
}

public sealed class PlannerEligibilityTests
{
    [Fact]
    public void A_prerequisite_in_progress_makes_a_course_eligible_with_a_condition()
    {
        var s = new Scenario().Course("CSC201", prereq: "CSC102").Section("CSC201");
        s.InProgress.Add("CSC102");

        var r = s.Run();

        Assert.Equal(["CSC201"], r.PlannedCodes());
        Assert.Equal(["CSC102"], r.Plan[0].Reasons.Get(ReasonCode.WaitingForGrade).Courses);
        Assert.Equal(["CSC201"], r.Warnings.Get(ReasonCode.InProgressPrerequisite).Courses);
    }

    [Fact]
    public void In_progress_prerequisites_can_be_switched_off()
    {
        var s = new Scenario().Course("CSC201", prereq: "CSC102").Section("CSC201");
        s.InProgress.Add("CSC102");
        s.Policy.SetSwitches(RepeatGradeRule.LatestCounts, allowInProgressPrerequisites: false, graduatingExemptFromMinimum: true);

        Assert.Equal(CourseStatus.NotEligible, s.Run().For("CSC201").Status);
    }

    [Fact]
    public void A_failed_prerequisite_is_named_as_failed()
    {
        var s = new Scenario().Course("MCM301", prereq: "MCM201:C").Section("MCM301");
        s.Failed.Add("MCM201");

        var reason = s.Run().For("MCM301").Reasons.Get(ReasonCode.PrerequisiteFailed);
        Assert.Equal(["MCM201"], reason.Courses);
        Assert.Equal("C", reason.Value);
    }

    [Fact]
    public void A_grade_below_the_minimum_does_not_count()
    {
        var s = new Scenario().Course("MCM301", prereq: "MCM201:C").Section("MCM301");
        s.Passed["MCM201"] = "D";

        Assert.True(s.Run().For("MCM301").Reasons.Has(ReasonCode.MinimumGradeNotMet));
    }

    [Fact]
    public void Transfer_credit_meets_a_minimum_grade()
    {
        var s = new Scenario().Course("ENG102", prereq: "ENG101:C").Section("ENG102");
        s.Passed["ENG101"] = "TR";
        Assert.Equal(["ENG102"], s.Run().PlannedCodes());
    }

    [Fact]
    public void Missing_or_options_are_listed_together()
    {
        var reason = new Scenario().Course("ITC380", prereq: "ITC304 OR ITC301").Section("ITC380").Run()
            .For("ITC380").Reasons.Get(ReasonCode.PrerequisiteNotMet);
        Assert.Equal(["ITC304", "ITC301"], reason.Courses);
    }

    [Fact]
    public void One_satisfied_option_is_enough()
    {
        var s = new Scenario().Course("ITC380", prereq: "ITC304 OR ITC301").Section("ITC380");
        s.Passed["ITC301"] = "B";
        Assert.Equal(["ITC380"], s.Run().PlannedCodes());
    }

    [Theory]
    [InlineData("STANDING(Junior)", 59, ReasonCode.StandingRequired)]
    [InlineData("CREDITS(90)", 89, ReasonCode.CreditsRequired)]
    [InlineData("PERMISSION", 100, ReasonCode.PermissionRequired)]
    public void Standing_credits_and_permission_are_checked(string rule, int earned, ReasonCode expected)
    {
        var r = new Scenario { Earned = earned }.Course("ITC410", prereq: rule).Section("ITC410").Run();
        Assert.Equal(CourseStatus.NotEligible, r.For("ITC410").Status);
        Assert.True(r.For("ITC410").Reasons.Has(expected));
    }

    [Fact]
    public void Granted_permission_counts()
    {
        var s = new Scenario { Earned = 95 }.Course("ITC410", prereq: "CREDITS(90) AND PERMISSION").Section("ITC410");
        s.Permissions.Add("ITC410");
        Assert.Equal(["ITC410"], s.Run().PlannedCodes());
    }

    [Fact]
    public void A_course_offered_this_season_but_with_no_sections_gets_a_soft_warning()
    {
        var r = new Scenario().Course("ITC390").Course("ITC201").Section("ITC201").Run();

        Assert.True(r.For("ITC390").Reasons.Has(ReasonCode.NoSectionsThisTerm));
        Assert.Equal(["ITC390"], r.Warnings.Get(ReasonCode.OfferedButNoSections).Courses);
        Assert.Equal(["ITC201"], r.PlannedCodes());
    }

    [Fact]
    public void Courses_already_passed_or_in_progress_are_not_planned_again()
    {
        var s = new Scenario().Course("ITC201").Course("ITC210").Section("ITC201").Section("ITC210");
        s.Passed["ITC201"] = "A";
        s.InProgress.Add("ITC210");

        var r = s.Run();
        Assert.Empty(r.Plan);
        Assert.Empty(r.Courses);
    }
}

public sealed class PlannerPackingTests
{
    [Fact]
    public void Clashing_sections_are_never_combined()
    {
        var r = new Scenario()
            .Course("ITC201").Course("ITC210")
            .Section("ITC201", "01", "Mon;Wed", "09:00", "10:15")
            .Section("ITC210", "01", "Mon;Wed", "09:30", "10:45")
            .Run();

        Assert.Single(r.Plan);
        var loser = r.Courses.Single(c => c.Status != CourseStatus.Planned);
        Assert.True(loser.Reasons.Has(ReasonCode.NoSectionFits));
        Assert.True(loser.Reasons.Has(ReasonCode.ClashesWith));
    }

    [Fact]
    public void A_non_clashing_section_is_chosen_and_the_clashing_one_is_recorded_as_a_rejected_alternative()
    {
        var r = new Scenario()
            .Course("ITC201", semester: 1).Course("ITC210", semester: 2)
            .Section("ITC201", "01", "Mon;Wed", "09:00", "10:15")
            .Section("ITC210", "01", "Mon;Wed", "09:30", "10:45")
            .Section("ITC210", "02", "Tue;Thu", "09:00", "10:15")
            .Run();

        Assert.Equal(2, r.Plan.Count);
        var itc210 = r.Plan.Single(p => p.CourseCode == "ITC210");
        Assert.Equal("02", itc210.SectionCode);
        var rejected = Assert.Single(itc210.Alternatives);
        Assert.Equal("01", rejected.SectionCode);
        Assert.Equal(["ITC201"], rejected.Reasons.Get(ReasonCode.ClashesWith).Courses);
    }

    [Fact]
    public void Only_sections_inside_the_students_free_time_are_used()
    {
        var r = new Scenario { Availability = Scenario.EveningsMonToThu() }
            .Course("ITC201")
            .Section("ITC201", "01", "Mon;Wed", "09:00", "10:15")
            .Section("ITC201", "E1", "Tue;Thu", "17:00", "18:15")
            .Run();

        var planned = Assert.Single(r.Plan);
        Assert.Equal("E1", planned.SectionCode);
        Assert.True(planned.Alternatives.Single().Reasons.Has(ReasonCode.OutsideAvailability));
    }

    [Fact]
    public void Online_sections_without_meetings_always_fit_availability()
    {
        var r = new Scenario { Availability = Scenario.EveningsMonToThu() }
            .Course("ITC201").Section("ITC201", "W1", days: null, delivery: DeliveryMode.Online).Run();
        Assert.Equal("W1", Assert.Single(r.Plan).SectionCode);
    }

    [Fact]
    public void The_section_with_more_seats_wins_and_full_sections_are_skipped()
    {
        var r = new Scenario()
            .Course("ITC201")
            .Section("ITC201", "01", "Mon;Wed", "09:00", "10:15", seats: 0)
            .Section("ITC201", "02", "Tue;Thu", "09:00", "10:15", seats: 3)
            .Section("ITC201", "03", "Tue;Thu", "11:00", "12:15", seats: 12)
            .Run();

        var planned = Assert.Single(r.Plan);
        Assert.Equal("03", planned.SectionCode);
        Assert.True(planned.Alternatives.Single(a => a.SectionCode == "01").Reasons.Has(ReasonCode.SectionFull));
        Assert.Equal("03", planned.Alternatives.Single(a => a.SectionCode == "02").Reasons.Get(ReasonCode.FewerSeatsLeft).Value);
    }

    [Fact]
    public void Each_course_gets_at_most_one_section()
    {
        var r = new Scenario().Course("ITC201").Section("ITC201", "01").Section("ITC201", "02").Section("ITC201", "03").Run();
        Assert.Single(r.Plan);
    }

    [Fact]
    public void A_corequisite_pair_is_planned_together_when_it_fits()
    {
        var r = new Scenario()
            .Course("ITC202", prereq: "COREQ(ITC202L)")
            .Course("ITC202L", credits: 1, prereq: "COREQ(ITC202)")
            .Section("ITC202", "01", "Mon;Wed", "09:00", "10:15")
            .Section("ITC202L", "L1", "Wed", "09:30", "12:20") // clashes with the lecture
            .Section("ITC202L", "L2", "Thu", "14:00", "16:50")
            .Run();

        Assert.Equal(["ITC202", "ITC202L"], r.PlannedCodes());
        Assert.Equal("L2", r.Plan.Single(p => p.CourseCode == "ITC202L").SectionCode);
        Assert.True(r.Plan.Single(p => p.CourseCode == "ITC202").Reasons.Has(ReasonCode.TakeWithCorequisite));
        Assert.Equal(4, r.TotalCredits);
    }

    [Fact]
    public void Elective_groups_stop_once_their_credits_are_covered()
    {
        var s = new Scenario { ElectiveCredits = 3 }
            .Course("ITC310", elective: true).Course("ITC320", elective: true)
            .Section("ITC310").Section("ITC320");

        var r = s.Run();
        Assert.Single(r.Plan);
        Assert.True(r.Courses.Single(c => c.Status != CourseStatus.Planned).Reasons.Has(ReasonCode.ElectiveQuotaMet));
    }

    [Fact]
    public void The_students_own_cap_can_only_lower_the_maximum()
    {
        var s = new Scenario { Preferences = new PlannerPreferences(9, DeliveryPreference.Any) };
        for (var i = 1; i <= 6; i++) s.Course($"ITC{300 + i}").Section($"ITC{300 + i}");
        Assert.Equal(9, s.Run().TotalCredits);

        s.Preferences = new PlannerPreferences(30, DeliveryPreference.Any);
        Assert.Equal(18, s.Run().TotalCredits);
    }

    [Fact]
    public void Delivery_preference_filters_sections()
    {
        var r = new Scenario { Preferences = new PlannerPreferences(null, DeliveryPreference.OnlineOnly) }
            .Course("ITC201")
            .Section("ITC201", "01", "Mon;Wed", "09:00", "10:15")
            .Section("ITC201", "W1", days: null, delivery: DeliveryMode.Online)
            .Run();
        Assert.Equal("W1", Assert.Single(r.Plan).SectionCode);
    }

    [Fact]
    public void Being_below_the_minimum_is_a_warning_not_a_reason_to_add_invalid_courses()
    {
        // Plenty of degree left (60 free-elective credits), so the graduating exemption doesn't apply.
        var r = new Scenario { FreeCredits = 60 }.Course("ITC201").Course("ITC301", prereq: "ITC201").Section("ITC201").Section("ITC301").Run();
        Assert.Equal(["ITC201"], r.PlannedCodes());
        Assert.Equal("12", r.Warnings.Get(ReasonCode.BelowMinimumCredits).Value);
    }
}

public sealed class PlannerPriorityAndProjectionTests
{
    [Fact]
    public void Courses_that_unlock_more_go_first()
    {
        var s = new Scenario { Preferences = new PlannerPreferences(3, DeliveryPreference.Any) }
            .Course("ITC101", semester: 1)                // leaf
            .Course("CSC101", semester: 2)                // starts a long chain
            .Course("CSC102", prereq: "CSC101")
            .Course("CSC201", prereq: "CSC102")
            .Section("ITC101").Section("CSC101");

        var r = s.Run();

        Assert.Equal(["CSC101"], r.PlannedCodes());
        Assert.Equal(1, r.For("CSC101").PriorityRank);
        Assert.Equal(3, r.For("CSC101").ChainLength);
        var unlocks = r.Plan[0].Reasons.Get(ReasonCode.UnlocksOtherCourses);
        Assert.Equal("2", unlocks.Value);
        Assert.True(r.For("ITC101").Reasons.Has(ReasonCode.CreditLimitReached));
    }

    [Fact]
    public void Summer_terms_can_bring_graduation_forward()
    {
        var s = new Scenario { Term = new PlannerTerm("2027SP", TermSeason.Spring, 2027), Preferences = new PlannerPreferences(3, DeliveryPreference.Any) }
            .Course("ENG101", offered: OfferedIn.All).Course("ENG102", offered: OfferedIn.All).Course("ENG201", offered: OfferedIn.All)
            .Section("ENG101").Section("ENG102").Section("ENG201");

        var r = s.Run();

        Assert.Equal("2027FA", r.Projection.WithSummer.GraduationTerm);   // Spring, Summer, Fall
        Assert.Equal("2028SP", r.Projection.WithoutSummer.GraduationTerm); // Spring, Fall, Spring
        Assert.Equal(3, r.Projection.WithSummer.TermsRemaining);
    }

    [Fact]
    public void Without_summer_still_uses_summer_for_summer_only_courses_like_internships()
    {
        var r = new Scenario { Earned = 100, Term = new PlannerTerm("2027SP", TermSeason.Spring, 2027) }
            .Course("ITC402").Section("ITC402")
            .Course("ITC410", credits: 4, offered: OfferedIn.Summer)  // internship: summer only
            .Course("ITC360")                                          // a normal course, not taken in summer without summer
            .Run();

        var noSummer = r.Projection.WithoutSummer;
        Assert.NotNull(noSummer.GraduationTerm);
        var summer = Assert.Single(noSummer.Terms, t => t.TermCode == "2027SU");
        Assert.Equal(["ITC410"], summer.Courses);
        Assert.Equal(0, summer.ElectiveCredits);
    }

    [Fact]
    public void Free_elective_credits_count_toward_graduation()
    {
        var r = new Scenario { FreeCredits = 9, Preferences = new PlannerPreferences(9, DeliveryPreference.Any) }
            .Course("ITC201").Section("ITC201").Run();

        Assert.Equal(["ITC201"], r.PlannedCodes());
        var next = r.Projection.WithoutSummer.Terms[1];
        Assert.Equal(9, next.ElectiveCredits);
        Assert.Equal("2027FA", r.Projection.WithoutSummer.GraduationTerm);
    }

    [Fact]
    public void An_unreachable_requirement_is_reported()
    {
        var r = new Scenario().Course("ITC999", offered: OfferedIn.None).Run();
        Assert.Null(r.Projection.WithSummer.GraduationTerm);
        Assert.True(r.Warnings.Has(ReasonCode.GraduationNotReachable));
    }

    [Fact]
    public void The_same_input_always_gives_the_same_plan()
    {
        var s = new Scenario { Availability = Scenario.EveningsMonToThu() };
        foreach (var c in new[] { "ITC201", "ITC210", "ITC220", "ITC250", "ITC301" })
            s.Course(c).Section(c, "E1", "Mon;Wed", "17:00", "18:15").Section(c, "E2", "Tue;Thu", "17:00", "18:15");
        var input = s.Input();

        var a = JsonSerializer.Serialize(Planner.Plan(input));
        var b = JsonSerializer.Serialize(Planner.Plan(input));

        Assert.Equal(a, b);
    }

    [Fact]
    public void The_policy_limits_are_reported_with_the_rule_name()
    {
        var r = new Scenario { FreeCredits = 60 }.Course("ITC201").Section("ITC201").Run();
        Assert.Equal(new CreditLimits(12, 18, "Standard full-time load"), r.Limits);
    }

    [Fact]
    public void A_gap_in_the_credit_rules_plans_nothing_and_says_why()
    {
        var policy = new CreditPolicy(Guid.NewGuid(), "Summer only");
        policy.ReplaceRules([new CreditLimitRule(Guid.NewGuid(), 1, "Summer", 0, 9).When(season: TermSeason.Summer)]);
        var r = new Scenario { Policy = policy }.Course("ITC201").Section("ITC201").Run();

        Assert.Empty(r.Plan);
        Assert.True(r.Warnings.Has(ReasonCode.NoCreditRule));
    }
}
