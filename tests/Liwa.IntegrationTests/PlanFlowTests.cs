using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Liwa.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Liwa.IntegrationTests;

/// <summary>Own database: these tests save and send plans.</summary>
public sealed class PlanFlowApiFactory : LiwaApiFactory;

public sealed class PlanFlowTests(PlanFlowApiFactory factory) : IClassFixture<PlanFlowApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private async Task<(HttpClient Client, ViewBody View)> ViewAsync(string username)
    {
        var client = await factory.SignedInClientAsync(username);
        var response = await client.PostAsync("/api/students/me/planning", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (client, (await response.Content.ReadFromJsonAsync<ViewBody>(Json))!);
    }

    private static string Key(ReasonBody r) => $"{r.Code}:{string.Join(',', r.Courses)}:{r.Value}";

    [Fact]
    public async Task The_planning_view_has_titles_class_times_and_the_advisor()
    {
        var (_, view) = await ViewAsync("student.new");

        Assert.Equal("Spring 2027", view.TermName);
        Assert.Equal("Dr. Ahmed Al Zaabi", view.AdvisorName);
        Assert.NotEmpty(view.Recommendation.Plan);
        foreach (var p in view.Recommendation.Plan)
        {
            Assert.Contains(view.Courses, c => c.Code == p.CourseCode && !string.IsNullOrEmpty(c.Title) && !string.IsNullOrEmpty(c.TitleAr));
            Assert.Contains(view.Sections, s => s.Id == p.SectionId);
        }

        Assert.Null(view.Saved);
    }

    [Fact]
    public async Task A_programming_class_without_its_lab_is_refused()
    {
        var (client, view) = await ViewAsync("student.new");
        var csc101 = view.Sections.First(s => s.CourseCode == "CSC101");

        var response = await client.PostAsJsonAsync("/api/students/me/plan", new { sectionIds = new[] { csc101.Id }, submit = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SaveBody>(Json);
        Assert.Contains(body!.Check!.Errors, e => e.Code == "CorequisiteMissing" && e.Courses.SequenceEqual(new[] { "CSC101", "CSC101L" }));
    }

    [Fact]
    public async Task Clashing_classes_are_caught_by_the_live_check()
    {
        var (client, view) = await ViewAsync("student.new");
        var a = view.Sections.First(s => s.Meetings.Count > 0);
        var b = view.Sections.First(s => s.CourseCode != a.CourseCode && s.Meetings.Any(m => a.Meetings.Any(x => x.Day == m.Day && x.Start < m.End && m.Start < x.End)));

        var response = await client.PostAsJsonAsync("/api/students/me/plan/check", new { sectionIds = new[] { a.Id, b.Id } });

        var check = await response.Content.ReadFromJsonAsync<CheckBody>(Json);
        Assert.Contains(check!.Errors, e => e.Code == "ClashesWith");
    }

    [Fact]
    public async Task Sending_needs_every_warning_confirmed_then_goes_to_the_advisor_once()
    {
        var (client, view) = await ViewAsync("student.honors");
        var ids = view.Recommendation.Plan.Select(p => p.SectionId).ToArray();
        var check = (await (await client.PostAsJsonAsync("/api/students/me/plan/check", new { sectionIds = ids })).Content.ReadFromJsonAsync<CheckBody>(Json))!;
        Assert.Empty(check.Errors);
        Assert.Contains(check.Warnings, w => w.Code == "WaitingForGrade"); // Fall 2026 grades not posted yet

        var unconfirmed = await client.PostAsJsonAsync("/api/students/me/plan", new { sectionIds = ids, submit = true });
        Assert.Equal(HttpStatusCode.Conflict, unconfirmed.StatusCode);

        var sent = await client.PostAsJsonAsync("/api/students/me/plan", new { sectionIds = ids, submit = true, acknowledgedWarnings = check.Warnings.Select(Key) });
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        var saved = (await sent.Content.ReadFromJsonAsync<SaveBody>(Json))!.Plan!;
        Assert.Equal("Submitted", saved.Status);
        Assert.Equal(1, saved.Version);

        var again = await client.PostAsJsonAsync("/api/students/me/plan", new { sectionIds = ids, submit = true, acknowledgedWarnings = check.Warnings.Select(Key) });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var (_, after) = await ViewAsync("student.honors");
        Assert.Equal("Submitted", after.Saved!.Status);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiwaDbContext>();
        Assert.True(await db.AuditLog.AnyAsync(a => a.Action == "plan.submitted"));
        var advisor = await db.Users.SingleAsync(u => u.Username == "advisor.it");
        Assert.True(await db.Notifications.AnyAsync(n => n.UserId == advisor.Id && n.MessageKey == "notifications.planSubmitted"));
        var version = await db.Plans.Where(p => p.Status == Liwa.Domain.Planning.PlanStatus.Submitted).SelectMany(p => p.Versions).FirstAsync();
        Assert.Contains("WaitingForGrade", version.AcknowledgedWarnings!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_draft_can_be_saved_and_changed_and_each_save_is_a_new_version()
    {
        var (client, view) = await ViewAsync("student.repeat");
        var ids = view.Recommendation.Plan.Select(p => p.SectionId).ToArray();

        var first = await client.PostAsJsonAsync("/api/students/me/plan", new { sectionIds = ids, submit = false });
        var second = await client.PostAsJsonAsync("/api/students/me/plan", new { sectionIds = ids.Take(ids.Length - 1).ToArray(), submit = false });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var saved = (await second.Content.ReadFromJsonAsync<SaveBody>(Json))!.Plan!;
        Assert.Equal("Draft", saved.Status);
        Assert.Equal(2, saved.Version);
    }

    public sealed record ReasonBody(string Code, List<string> Courses, string? Value);

    public sealed record MeetingBody(string Day, TimeOnly Start, TimeOnly End);

    public sealed record SectionBody(Guid Id, string CourseCode, string SectionCode, List<MeetingBody> Meetings);

    public sealed record CourseBody(string Code, string Title, string? TitleAr, int Credits);

    public sealed record PlannedBody(string CourseCode, Guid SectionId);

    public sealed record ResultBody(List<PlannedBody> Plan, int TotalCredits);

    public sealed record SavedBody(Guid Id, string Status, int Version, List<Guid> SectionIds);

    public sealed record ViewBody(string TermCode, string TermName, string? AdvisorName, ResultBody Recommendation, List<CourseBody> Courses, List<SectionBody> Sections, SavedBody? Saved);

    public sealed record CheckBody(int TotalCredits, int MaxCredits, List<ReasonBody> Errors, List<ReasonBody> Warnings);

    public sealed record SaveBody(string Outcome, CheckBody? Check, SavedBody? Plan);
}
