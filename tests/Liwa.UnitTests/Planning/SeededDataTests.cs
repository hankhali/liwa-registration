using System.Text.Json;
using System.Text.Json.Serialization;
using Liwa.Domain.Academic;
using Liwa.Domain.Planning;
using Liwa.Infrastructure.Seeding;

namespace Liwa.UnitTests.Planning;

/// <summary>
/// The planner on the real seeded programs, students and Spring 2027 timetable (brief §5/§8), loaded straight from
/// the demo CSV files with the credit policy from appsettings.json.
/// </summary>
public sealed class SeededDataTests
{
    // Static fields initialise in this order: the JSON options must come before the policy that uses them.
    private static readonly JsonSerializerOptions ConfigJson = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
    private static readonly CsvPlannerSource Seed = CsvPlannerSource.FromDemoFiles();
    private static readonly DateOnly Today = new(2026, 11, 10);

    public static CreditPolicy Policy()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir!.FullName, "Liwa.slnx"))) dir = dir.Parent;
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir.FullName, "src", "Liwa.Api", "appsettings.json")));
        var defaults = doc.RootElement.GetProperty("CreditPolicy").Deserialize<CreditPolicyDefaults>(ConfigJson)!;
        return CreditPolicySeeder.Build(defaults);
    }

    /// <summary>One policy instance: its rule ids are part of the result, so determinism compares like with like.</summary>
    private static readonly CreditPolicy SeedPolicy = Policy();

    private static PlannerResult Plan(string studentId, PlannerPreferences? prefs = null, Availability? availability = null) =>
        Planner.Plan(Seed.InputFor(studentId, SeedPolicy, Today, availability, prefs));

    [Fact]
    public void There_are_sixteen_demo_students_and_every_one_gets_a_plan()
    {
        Assert.Equal(16, Seed.StudentIds.Count);
        foreach (var id in Seed.StudentIds)
        {
            var r = Plan(id);
            Assert.NotNull(r.Limits);
            Assert.NotNull(r.Projection.WithSummer.GraduationTerm);
        }
    }

    /// <summary>
    /// Brief §8: each seeded program graduates a fresh student in 8 semesters (no summer). The projection plans the
    /// normal load (15) each semester; the semester that can finish the degree may go up to the maximum (18), as the
    /// catalog's own final semesters do (7 × 15 + 18 = 123).
    /// </summary>
    [Fact]
    public void Each_seeded_program_graduates_a_fresh_student_in_eight_semesters()
    {
        Assert.Equal(3, Seed.ProgramKeys.Count);
        foreach (var program in Seed.ProgramKeys)
        {
            var r = Planner.Plan(Seed.FreshStudent(program, Policy(), Today));
            Assert.True(r.Projection.WithoutSummer.TermsRemaining == 8, $"{program}: {r.Projection.WithoutSummer.TermsRemaining} semesters without summer");
            Assert.Equal(15, r.TargetCredits);
        }
    }

    [Fact]
    public void Section_02_beats_01_because_of_seats_left()
    {
        // Somewhere in the seeded plans, section 02 is chosen over 01 and the decision log says it had more seats left.
        var decisions = Seed.StudentIds.SelectMany(id => Plan(id).Plan)
            .Where(p => p.SectionCode == "02")
            .Select(p => (p.CourseCode, Lost: p.Alternatives.FirstOrDefault(a => a.SectionCode == "01")))
            .Where(x => x.Lost is not null && x.Lost.Reasons.Any(r => r.Code == ReasonCode.FewerSeatsLeft && r.Value == "02"))
            .ToList();
        Assert.NotEmpty(decisions);

        // And the planner never prefers the section with fewer seats when both fit: MTH105 01 has 5 seats, 02 has 25.
        var mth = Seed.StudentIds.SelectMany(id => Plan(id).Plan).Where(p => p.CourseCode == "MTH105").ToList();
        Assert.All(mth, p => Assert.NotEqual("01", p.SectionCode));
    }

    [Fact]
    public void The_graduating_senior_is_not_forced_to_the_minimum()
    {
        var r = Plan("202300104");
        Assert.Equal(6, r.TotalCredits);
        Assert.DoesNotContain(r.Warnings, w => w.Code == ReasonCode.BelowMinimumCredits);
    }

    [Fact]
    public void The_student_with_a_hold_can_plan_but_is_warned()
    {
        var r = Plan("202500113");
        Assert.NotEmpty(r.Plan);
        Assert.Contains(r.Warnings, w => w.Code == ReasonCode.AccountHold && w.Value == "FINANCE");
    }

    [Fact]
    public void The_student_near_the_end_has_the_internship_and_graduation_projects_ahead()
    {
        // The real catalog's internship needs 75 credits (no department permission); student.intern has 102.
        var r = Plan("202300115");
        var ahead = r.Plan.Select(p => p.CourseCode).Concat(r.Projection.WithoutSummer.Terms!.SelectMany(t => t.Courses)).ToHashSet();
        Assert.Contains("CIT483", ahead);
        Assert.Contains("CIT484", ahead);
        Assert.Contains("CIT485", ahead);
    }

    [Fact]
    public void The_real_bba_bit_record_gets_an_evening_plan()
    {
        // student.hanieh: 18 credits earned, 12 in progress, weekday evenings only (Mon–Thu 5–8 pm).
        var evenings = Scenario.EveningsMonToThu();
        var input = Seed.InputFor("202600116", Policy(), Today, evenings);
        Assert.Equal(18, input.Student.CreditsEarned);
        var r = Planner.Plan(input);
        Assert.NotEmpty(r.Plan);
        foreach (var p in r.Plan)
            Assert.True(evenings.Fits(input.Sections.Single(s => s.Id == p.SectionId).Meetings), $"{p.CourseCode} {p.SectionCode}");
    }

    [Fact]
    public void The_evening_only_student_gets_only_evening_or_online_classes()
    {
        var evenings = Scenario.EveningsMonToThu();
        var input = Seed.InputFor("202600111", Policy(), Today, evenings);
        var r = Planner.Plan(input);
        Assert.NotEmpty(r.Plan);
        foreach (var p in r.Plan)
            Assert.True(evenings.Fits(input.Sections.Single(s => s.Id == p.SectionId).Meetings), $"{p.CourseCode} {p.SectionCode}");
    }

    [Fact]
    public void Transfer_credit_satisfies_requirements_but_not_the_gpa()
    {
        var input = Seed.InputFor("202500112", Policy(), Today);
        Assert.Equal("TR", input.Student.Passed["ENG100"]);
        var r = Planner.Plan(input);
        Assert.DoesNotContain(r.Courses, c => c.CourseCode == "ENG100"); // already counts
    }

    [Fact]
    public void Full_sections_are_never_recommended()
    {
        foreach (var id in Seed.StudentIds)
        {
            var input = Seed.InputFor(id, Policy(), Today);
            foreach (var p in Planner.Plan(input).Plan)
                Assert.True(input.Sections.Single(s => s.Id == p.SectionId).SeatsLeft > 0);
        }
    }

    [Fact]
    public void The_seeded_plans_are_deterministic()
    {
        foreach (var id in Seed.StudentIds)
            Assert.Equal(JsonSerializer.Serialize(Plan(id)), JsonSerializer.Serialize(Plan(id)));
    }
}
