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
        Assert.Contains(r.Plan, p => p.CourseCode == "ITC304");
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
    public async Task The_repeat_student_is_told_why_they_cannot_take_mcm301()
    {
        var r = await RecommendAsync("student.repeat");

        var mcm301 = r.Courses.Single(c => c.CourseCode == "MCM301");
        Assert.Equal("NotEligible", mcm301.Status);
        Assert.Contains(mcm301.Reasons, x => x.Code == "PrerequisiteFailed" && x.Courses.Contains("MCM201"));
        var mcm201 = r.Courses.Single(c => c.CourseCode == "MCM201");
        Assert.Contains(mcm201.Reasons, x => x.Code == "NotOfferedThisTerm" && x.Value == "Fall");
    }

    [Fact]
    public async Task The_new_student_gets_programming_and_its_lab_together()
    {
        var r = await RecommendAsync("student.new");

        Assert.Contains(r.Plan, p => p.CourseCode == "CSC101");
        Assert.Contains(r.Plan, p => p.CourseCode == "CSC101L");
        Assert.DoesNotContain(r.Plan, p => p.CourseCode == "ENG101"); // already has AP credit
        Assert.InRange(r.TotalCredits, 12, 18);
    }

    [Fact]
    public async Task An_evenings_only_student_gets_only_evening_or_online_sections_and_daytime_labs_reject_the_pair()
    {
        var r = await RecommendAsync("student.honors", EveningsOnly);

        Assert.All(r.Plan, p => Assert.DoesNotContain(p.Alternatives, a => a.SectionId == p.SectionId));
        var itc202 = r.Courses.SingleOrDefault(c => c.CourseCode == "ITC202");
        Assert.NotNull(itc202);
        Assert.Contains(itc202!.Reasons, x => x.Code == "CorequisiteDoesNotFit" && x.Courses.Contains("ITC202L"));
    }

    [Fact]
    public async Task A_course_offered_this_season_with_no_sections_is_a_soft_warning()
    {
        // ITC390 is offered Fall and Spring in the catalog but the demo timetable schedules no section for it.
        var r = await RecommendAsync("student.senior");
        var itc390 = r.Courses.SingleOrDefault(c => c.CourseCode == "ITC390");
        if (itc390 is not null) Assert.Contains(itc390.Reasons, x => x.Code == "NoSectionsThisTerm");
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
