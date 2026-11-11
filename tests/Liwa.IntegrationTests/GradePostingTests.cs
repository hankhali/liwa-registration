using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Liwa.IntegrationTests;

/// <summary>Own database: grades are posted and approved plans move to "Needs review".</summary>
public sealed class GradePostingApiFactory : LiwaApiFactory;

public sealed class GradePostingTests(GradePostingApiFactory factory) : IClassFixture<GradePostingApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static string Key(PlanFlowTests.ReasonBody r) => $"{r.Code}:{string.Join(',', r.Courses)}:{r.Value}";

    private static async Task<T> Read<T>(HttpResponseMessage r) => (await r.Content.ReadFromJsonAsync<T>(Json))!;

    [Fact]
    public async Task A_failed_prerequisite_moves_an_approved_plan_to_needs_review_and_tells_both_sides()
    {
        // student.honors (202500103) has Fall 2026 courses in progress, so the plan waits for grades.
        var student = await factory.SignedInClientAsync("student.honors");
        var view = await Read<PlanFlowTests.ViewBody>(await student.PostAsync("/api/students/me/planning", null));
        var ids = view.Recommendation.Plan.Select(p => p.SectionId).ToArray();
        var check = await Read<PlanFlowTests.CheckBody>(await student.PostAsJsonAsync("/api/students/me/plan/check", new { sectionIds = ids }));
        var waitingFor = check.Warnings.Where(w => w.Code == "WaitingForGrade").SelectMany(w => w.Courses.Skip(1)).Distinct().ToList();
        Assert.NotEmpty(waitingFor);
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsJsonAsync("/api/students/me/plan",
            new { sectionIds = ids, submit = true, acknowledgedWarnings = check.Warnings.Select(Key) })).StatusCode);

        // Both sides hear about the status change.
        Assert.Contains((await Read<AdvisorFlowTests.NotificationsBody>(await student.GetAsync("/api/notifications"))).Items,
            n => n.MessageKey == "notifications.you.submitted");

        var advisor = await factory.SignedInClientAsync("advisor.it");
        var planId = (await Read<List<AdvisorFlowTests.QueueBody>>(await advisor.GetAsync("/api/advisor/plans"))).Single(q => q.StudentId == "202500103").PlanId;
        Assert.Equal(HttpStatusCode.OK, (await advisor.PostAsJsonAsync($"/api/advisor/plans/{planId}/decision", new { decision = "Approve" })).StatusCode);

        var userId = (await Read<List<AdvisorFlowTests.AdviseeIdBody>>(await advisor.GetAsync("/api/advisor/students"))).Single(s => s.StudentId == "202500103").UserId;
        var record = await Read<AdvisorFlowTests.RecordBody>(await advisor.GetAsync($"/api/advisor/students/{userId}"));
        var inProgress = record.Transcript.Where(t => t.Status == "InProgress").ToList();
        var admin = await factory.SignedInClientAsync("admin");

        // A passing grade for a course the plan doesn't depend on: the plan stays approved.
        var unrelated = inProgress.FirstOrDefault(t => !waitingFor.Contains(t.CourseCode));
        if (unrelated is not null)
        {
            var ok = await Read<RecheckBody>(await admin.PostAsJsonAsync("/api/admin/grades",
                new { studentId = "202500103", courseCode = unrelated.CourseCode, termCode = unrelated.TermCode, grade = "A" }));
            Assert.Equal(1, ok.Checked);
            Assert.Empty(ok.MovedToNeedsReview);
        }

        // An F in a prerequisite the plan depends on: the plan needs review.
        var prerequisite = inProgress.Single(t => t.CourseCode == waitingFor[0]);
        var failed = await admin.PostAsJsonAsync("/api/admin/grades",
            new { studentId = "202500103", courseCode = prerequisite.CourseCode, termCode = prerequisite.TermCode, grade = "F" });
        Assert.Equal(HttpStatusCode.OK, failed.StatusCode);
        var moved = Assert.Single((await Read<RecheckBody>(failed)).MovedToNeedsReview);
        Assert.Equal(planId, moved.PlanId);
        Assert.Contains(moved.Problems, p => p.Code is "NotEligibleNow" or "PrerequisiteFailed" or "MinimumGradeNotMet");

        Assert.Equal("NeedsReview", (await Read<List<AdvisorFlowTests.QueueBody>>(await advisor.GetAsync("/api/advisor/plans"))).Single(q => q.PlanId == planId).Status);
        Assert.Contains((await Read<AdvisorFlowTests.NotificationsBody>(await student.GetAsync("/api/notifications"))).Items, n => n.MessageKey == "notifications.planNeedsReviewStudent");
        Assert.Contains((await Read<AdvisorFlowTests.NotificationsBody>(await advisor.GetAsync("/api/notifications"))).Items, n => n.MessageKey == "notifications.planNeedsReviewAdvisor");
        Assert.Equal(1, (await Read<OverviewBody>(await advisor.GetAsync("/api/advisor/overview"))).NeedsReview);

        // Approve stays blocked while today's checks show errors; propose needs a note and a valid plan.
        Assert.Equal(HttpStatusCode.BadRequest, (await advisor.PostAsJsonAsync($"/api/advisor/plans/{planId}/decision", new { decision = "Approve" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await advisor.PostAsJsonAsync($"/api/advisor/plans/{planId}/decision", new { decision = "ProposeChanges", sectionIds = ids })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await advisor.PostAsJsonAsync($"/api/advisor/plans/{planId}/decision", new { decision = "ProposeChanges", note = "Try this.", sectionIds = ids })).StatusCode);

        // A whole-term re-check doesn't touch it again (it's no longer approved).
        Assert.Empty((await Read<RecheckBody>(await admin.PostAsync("/api/admin/recheck", null))).MovedToNeedsReview);

        // The record shows the plan history, and the advisee list shows standing and plan status.
        var after = await Read<RecordWithHistoryBody>(await advisor.GetAsync($"/api/advisor/students/{userId}"));
        Assert.Contains(after.PlanHistory, h => h.Action == "needsReview");
        var row = (await Read<List<AdviseeRowBody>>(await advisor.GetAsync("/api/advisor/students"))).Single(s => s.StudentId == "202500103");
        Assert.Equal("NeedsReview", row.PlanStatus);
        Assert.NotNull(row.Standing);
        Assert.NotNull(row.LastActivity);
    }

    [Fact]
    public async Task Grade_posting_rejects_bad_input()
    {
        var admin = await factory.SignedInClientAsync("admin");

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/grades",
            new { studentId = "202500103", courseCode = "CIT121", termCode = "2026FA", grade = "Z" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/api/admin/grades",
            new { studentId = "999999999", courseCode = "CIT121", termCode = "2026FA", grade = "A" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/admin/grades",
            new { studentId = "202700101", courseCode = "CIT121", termCode = "2026FA", grade = "A" })).StatusCode);

        var student = await factory.SignedInClientAsync("student.new");
        Assert.Equal(HttpStatusCode.Forbidden, (await student.PostAsync("/api/admin/recheck", null)).StatusCode);
    }

    public sealed record ProblemBody(string Code, List<string> Courses, string? Value);

    public sealed record MovedBody(Guid PlanId, string StudentId, List<ProblemBody> Problems);

    public sealed record RecheckBody(int Checked, List<MovedBody> MovedToNeedsReview);

    public sealed record OverviewBody(int AwaitingReview, int NeedsReview, int ApprovedThisTerm, int ChangesProposed);

    public sealed record RecordWithHistoryBody(List<AdvisorFlowTests.EventBody> PlanHistory);

    public sealed record AdviseeRowBody(string StudentId, string? Standing, string? PlanStatus, string? ProjectedGraduation, DateTimeOffset? LastActivity);
}
