using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Liwa.IntegrationTests;

/// <summary>The planner on the real demo data, through the API.</summary>
[Collection(ApiCollection.Name)]
public sealed class RecommendationTests(LiwaApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private async Task<ResultBody> RecommendAsync(string username, object? body = null)
    {
        var client = await factory.SignedInClientAsync(username);
        var response = body is null
            ? await client.PostAsync("/api/students/me/recommendation", null)
            : await client.PostAsJsonAsync("/api/students/me/recommendation", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ResultBody>(Json))!;
    }

    private static readonly object EveningsOnly = new
    {
        availability = new[] { "Monday", "Tuesday", "Wednesday", "Thursday" }.Select(d => new { day = d, start = "17:00", end = "20:00" }),
    };

    [Fact]
    public async Task The_graduating_senior_gets_their_last_6_credits_and_graduates_this_term()
    {
        var r = await RecommendAsync("student.senior");

        Assert.Equal(6, r.TotalCredits);
        Assert.Equal(6, r.Limits!.MinCredits);
        Assert.Contains(r.Plan, p => p.CourseCode == "CIT485");
        Assert.Contains(r.Plan, p => p.CourseCode == "CIT470");
        Assert.DoesNotContain(r.Warnings, w => w.Code == "BelowMinimumCredits");
        Assert.Equal("2027SP", r.Projection.WithoutSummer.GraduationTerm);
    }

    [Fact]
    public async Task The_high_gpa_student_can_take_21_credits_and_courses_waiting_for_grades_are_flagged()
    {
        var r = await RecommendAsync("student.honors");

        Assert.Equal(21, r.MaxCredits);
        Assert.Contains(r.Warnings, w => w.Code == "InProgressPrerequisite");
        Assert.All(r.Plan, p => Assert.True(p.SectionId != Guid.Empty));
    }

    [Fact]
    public async Task The_probation_student_is_capped_at_12_credits()
    {
        var r = await RecommendAsync("student.probation");

        Assert.Equal(12, r.MaxCredits);
        Assert.InRange(r.TotalCredits, 1, 12);
    }

    [Fact]
    public async Task The_repeat_student_is_told_why_they_cannot_take_mcm202()
    {
        var r = await RecommendAsync("student.repeat");

        // Failed MCM123 Media Theories, which MCM202 Media Research Methods needs; MCM123 can be retaken now.
        var mcm202 = r.Courses.Single(c => c.CourseCode == "MCM202");
        Assert.Equal("NotEligible", mcm202.Status);
        Assert.Contains(mcm202.Reasons, x => x.Code is "PrerequisiteFailed" or "PrerequisiteNotMet" && x.Courses.Contains("MCM123"));
        Assert.NotEqual("NotEligible", r.Courses.Single(c => c.CourseCode == "MCM123").Status);
    }

    [Fact]
    public async Task The_new_student_starts_with_the_first_semester_of_the_bit_plan()
    {
        var r = await RecommendAsync("student.new");

        Assert.Contains(r.Plan, p => p.CourseCode == "CIT112"); // Introduction to Computer Programming (Year 1, Semester 1)
        Assert.Contains(r.Plan, p => p.CourseCode == "CIT121"); // Introduction to Computer Architecture
        Assert.DoesNotContain(r.Plan, p => p.CourseCode == "ENG100"); // already has AP credit
        Assert.InRange(r.TotalCredits, 12, 18);
    }

    [Fact]
    public async Task An_evenings_only_student_gets_a_plan_and_daytime_only_courses_say_why()
    {
        // A fifth-semester BIT student studying Mon–Thu 5–8 pm: some later courses only have daytime sections.
        var r = await RecommendAsync("student.mid2", EveningsOnly);

        Assert.NotEmpty(r.Plan);
        Assert.All(r.Plan, p => Assert.DoesNotContain(p.Alternatives, a => a.SectionId == p.SectionId));
        Assert.Contains(r.Courses, c => c.Reasons.Any(x => x.Code == "NoSectionFits"));
    }

    [Fact]
    public async Task A_course_offered_this_season_with_no_sections_is_a_soft_warning()
    {
        // MCM329 Documentary Cinema is offered every term (catalog default) but the demo timetable schedules no section.
        var r = await RecommendAsync("student.media");
        var mcm329 = r.Courses.SingleOrDefault(c => c.CourseCode == "MCM329");
        if (mcm329 is not null) Assert.Contains(mcm329.Reasons, x => x.Code == "NoSectionsThisTerm");
    }

    [Fact]
    public async Task Projections_with_and_without_summer_are_both_returned()
    {
        var r = await RecommendAsync("student.new");
        Assert.NotNull(r.Projection.WithSummer.GraduationTerm);
        Assert.NotNull(r.Projection.WithoutSummer.GraduationTerm);
        // Summers add (smaller) terms, so compare when the student graduates, not how many terms it takes.
        Assert.True(Liwa.Domain.Academic.TermCodes.SortKey(r.Projection.WithSummer.GraduationTerm)
                    <= Liwa.Domain.Academic.TermCodes.SortKey(r.Projection.WithoutSummer.GraduationTerm));
    }

    [Fact]
    public async Task Bad_preferences_are_explained()
    {
        var client = await factory.SignedInClientAsync("student.new");
        var response = await client.PostAsJsonAsync("/api/students/me/recommendation", new { maxCredits = 99 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Only_students_get_recommendations_here()
    {
        var client = await factory.SignedInClientAsync("advisor.it");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/students/me/recommendation", null)).StatusCode);
    }

    public sealed record ReasonBody(string Code, List<string> Courses, string? Value);

    public sealed record LimitsBody(int MinCredits, int MaxCredits, string RuleName);

    public sealed record AlternativeBody(Guid SectionId, string SectionCode, List<ReasonBody> Reasons);

    public sealed record PlannedBody(string CourseCode, int Credits, Guid SectionId, string SectionCode, int PriorityRank, List<ReasonBody> Reasons, List<AlternativeBody> Alternatives);

    public sealed record CourseBody(string CourseCode, int Credits, string GroupKey, string Status, int PriorityRank, List<ReasonBody> Reasons);

    public sealed record TermProjectionBody(string? GraduationTerm, int TermsRemaining);

    public sealed record ProjectionBody(TermProjectionBody WithSummer, TermProjectionBody WithoutSummer);

    public sealed record ResultBody(string TermCode, LimitsBody? Limits, int MaxCredits, List<PlannedBody> Plan, int TotalCredits, List<CourseBody> Courses, List<ReasonBody> Warnings, ProjectionBody Projection);
}
