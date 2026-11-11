using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Liwa.IntegrationTests;

/// <summary>Own database: these tests move plans through the whole review workflow.</summary>
public sealed class AdvisorFlowApiFactory : LiwaApiFactory;

public sealed class AdvisorFlowTests(AdvisorFlowApiFactory factory) : IClassFixture<AdvisorFlowApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static string Key(PlanFlowTests.ReasonBody r) => $"{r.Code}:{string.Join(',', r.Courses)}:{r.Value}";

    private static async Task<T> Read<T>(HttpResponseMessage r) => (await r.Content.ReadFromJsonAsync<T>(Json))!;

    /// <summary>Sends the student's recommended plan to their advisor and returns the section ids.</summary>
    private async Task<(HttpClient Student, Guid[] Ids)> SendRecommendedPlanAsync(string username)
    {
        var student = await factory.SignedInClientAsync(username);
        var view = await Read<PlanFlowTests.ViewBody>(await student.PostAsync("/api/students/me/planning", null));
        var ids = view.Recommendation.Plan.Select(p => p.SectionId).ToArray();
        var check = await Read<PlanFlowTests.CheckBody>(await student.PostAsJsonAsync("/api/students/me/plan/check", new { sectionIds = ids }));
        var sent = await student.PostAsJsonAsync("/api/students/me/plan", new { sectionIds = ids, submit = true, acknowledgedWarnings = check.Warnings.Select(Key) });
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        return (student, ids);
    }

    [Fact]
    public async Task The_whole_review_conversation_works_and_is_recorded()
    {
        var (student, ids) = await SendRecommendedPlanAsync("student.honors");
        var advisor = await factory.SignedInClientAsync("advisor.it");

        // The queue shows the sent plan.
        var queue = await Read<List<QueueBody>>(await advisor.GetAsync("/api/advisor/plans"));
        var item = Assert.Single(queue, q => q.StudentId == "202500103");
        Assert.Equal("Submitted", item.Status);
        Assert.True(item.WarningCount > 0);

        // Opening it marks it "Advisor reviewing" and tells the student.
        var review = await Read<ReviewBody>(await advisor.GetAsync($"/api/advisor/plans/{item.PlanId}"));
        Assert.Equal("AdvisorReviewing", review.Status);
        Assert.Equal("Aisha Al Nuaimi", review.StudentName);
        Assert.Equal(ids.Order(), review.SectionIds.Order());
        Assert.Empty(review.LiveCheck.Errors);
        Assert.NotEmpty(review.ConfirmedWarnings);
        Assert.NotNull(review.ThisPlan.WithSummer.GraduationTerm);
        Assert.Equal(["submitted", "reviewing"], review.History.Select(h => h.Action));
        Assert.Contains((await Read<NotificationsBody>(await student.GetAsync("/api/notifications"))).Items, n => n.MessageKey == "notifications.planReviewing");

        // Rejecting needs a comment.
        var noComment = await advisor.PostAsJsonAsync($"/api/advisor/plans/{item.PlanId}/decision", new { decision = "Reject" });
        Assert.Equal(HttpStatusCode.BadRequest, noComment.StatusCode);
        var rejected = await advisor.PostAsJsonAsync($"/api/advisor/plans/{item.PlanId}/decision", new { decision = "Reject", note = "Please add an evening option." });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);

        var studentView = await Read<StudentViewBody>(await student.PostAsync("/api/students/me/planning", null));
        Assert.Equal("Rejected", studentView.Saved!.Status);
        Assert.Contains(studentView.History, h => h.Action == "rejected" && h.Note == "Please add an evening option." && h.ActorRole == "Advisor");

        // The student sends it again; the advisor proposes a change (drops one class).
        var check = await Read<PlanFlowTests.CheckBody>(await student.PostAsJsonAsync("/api/students/me/plan/check", new { sectionIds = ids }));
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsJsonAsync("/api/students/me/plan", new { sectionIds = ids, submit = true, acknowledgedWarnings = check.Warnings.Select(Key) })).StatusCode);
        await advisor.GetAsync($"/api/advisor/plans/{item.PlanId}");
        // Drop one class that can go on its own (not half of a lecture + lab pair): the check finds one.
        Guid[] proposal = [];
        foreach (var drop in ids)
        {
            var candidate = ids.Where(x => x != drop).ToArray();
            var c = await Read<PlanFlowTests.CheckBody>(await advisor.PostAsJsonAsync($"/api/advisor/plans/{item.PlanId}/check", new { sectionIds = candidate }));
            if (c.Errors.Count == 0) { proposal = candidate; break; }
        }

        Assert.NotEmpty(proposal);
        var proposed = await advisor.PostAsJsonAsync($"/api/advisor/plans/{item.PlanId}/decision", new { decision = "ProposeChanges", note = "One fewer class this term.", sectionIds = proposal });
        Assert.Equal(HttpStatusCode.OK, proposed.StatusCode);

        studentView = await Read<StudentViewBody>(await student.PostAsync("/api/students/me/planning", null));
        Assert.Equal("ChangesProposed", studentView.Saved!.Status);
        var proposedEvent = studentView.History.Single(h => h.Action == "proposed");
        Assert.Single(proposedEvent.Removed);
        Assert.Empty(proposedEvent.Added);
        // The previous version's classes come back too, so My Plan can show before / after side by side.
        Assert.Equal(proposal.Order(), studentView.Saved.SectionIds.Order());
        Assert.NotNull(studentView.Saved.PreviousSectionIds);
        Assert.Equal(proposal.Length + 1, studentView.Saved.PreviousSectionIds!.Count);
        Assert.Empty(proposal.Except(studentView.Saved.PreviousSectionIds));

        // The student accepts: approved, and the advisor is told.
        Assert.Equal(HttpStatusCode.NoContent, (await student.PostAsync("/api/students/me/plan/accept", null)).StatusCode);
        studentView = await Read<StudentViewBody>(await student.PostAsync("/api/students/me/planning", null));
        Assert.Equal("Approved", studentView.Saved!.Status);
        Assert.Equal(["submitted", "reviewing", "rejected", "submitted", "reviewing", "proposed", "accepted"], studentView.History.Select(h => h.Action));
        Assert.Contains((await Read<NotificationsBody>(await advisor.GetAsync("/api/notifications"))).Items, n => n.MessageKey == "notifications.planAccepted");
    }

    [Fact]
    public async Task An_advisor_can_approve_with_an_optional_note()
    {
        await SendRecommendedPlanAsync("student.senior");
        var advisor = await factory.SignedInClientAsync("advisor.it");
        var planId = (await Read<List<QueueBody>>(await advisor.GetAsync("/api/advisor/plans"))).Single(q => q.StudentId == "202300104").PlanId;

        var approved = await advisor.PostAsJsonAsync($"/api/advisor/plans/{planId}/decision", new { decision = "Approve" });

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var again = await advisor.PostAsJsonAsync($"/api/advisor/plans/{planId}/decision", new { decision = "Approve" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode); // already decided
    }

    [Fact]
    public async Task Advisors_only_see_their_own_students()
    {
        await SendRecommendedPlanAsync("student.repeat");
        var media = await factory.SignedInClientAsync("advisor.media");
        var it = await factory.SignedInClientAsync("advisor.it");
        var planId = (await Read<List<QueueBody>>(await media.GetAsync("/api/advisor/plans"))).Single(q => q.StudentId == "202500105").PlanId;

        Assert.Equal(HttpStatusCode.Forbidden, (await it.GetAsync($"/api/advisor/plans/{planId}")).StatusCode);
        Assert.DoesNotContain(await Read<List<QueueBody>>(await it.GetAsync("/api/advisor/plans")), q => q.StudentId == "202500105");
    }

    [Fact]
    public async Task The_read_only_student_record_shows_the_transcript()
    {
        var media = await factory.SignedInClientAsync("advisor.media");
        var students = await Read<List<DemoDataTests.AdviseeBody>>(await media.GetAsync("/api/advisor/students"));
        var userId = (await Read<List<AdviseeIdBody>>(await media.GetAsync("/api/advisor/students"))).Single(s => s.StudentId == "202500105").UserId;

        var record = await Read<RecordBody>(await media.GetAsync($"/api/advisor/students/{userId}"));

        Assert.Contains(record.Transcript, t => t.CourseCode == "MCM201" && t.Grade == "F");
        Assert.NotEmpty(students);
        var it = await factory.SignedInClientAsync("advisor.it");
        Assert.Equal(HttpStatusCode.NotFound, (await it.GetAsync($"/api/advisor/students/{userId}")).StatusCode);
    }

    [Fact]
    public async Task Notifications_can_be_marked_read()
    {
        await SendRecommendedPlanAsync("student.new");
        var advisor = await factory.SignedInClientAsync("advisor.it");
        Assert.True((await Read<NotificationsBody>(await advisor.GetAsync("/api/notifications"))).Unread > 0);

        Assert.Equal(HttpStatusCode.NoContent, (await advisor.PostAsync("/api/notifications/read", null)).StatusCode);

        Assert.Equal(0, (await Read<NotificationsBody>(await advisor.GetAsync("/api/notifications"))).Unread);
    }

    public sealed record QueueBody(Guid PlanId, Guid StudentUserId, string StudentName, string StudentId, string ProgramCode, string Status, int Version, int Credits, int WarningCount, bool Urgent);

    public sealed record EventBody(string ActorName, string ActorRole, string Action, string Status, int? Version, string? Note, List<string> Added, List<string> Removed);

    public sealed record TermProjBody(string? GraduationTerm);

    public sealed record ProjBody(TermProjBody WithSummer, TermProjBody WithoutSummer);

    public sealed record ReviewBody(Guid PlanId, string Status, int Version, string StudentName, List<Guid> SectionIds, PlanFlowTests.CheckBody LiveCheck,
        List<PlanFlowTests.ReasonBody> ConfirmedWarnings, ProjBody ThisPlan, List<EventBody> History);

    public sealed record StudentViewBody(PlanFlowTests.SavedBody? Saved, List<EventBody> History);

    public sealed record NotificationBody(Guid Id, string MessageKey, string? Parameters, bool Read);

    public sealed record NotificationsBody(int Unread, List<NotificationBody> Items);

    public sealed record AdviseeIdBody(Guid UserId, string StudentId);

    public sealed record LineBody(string TermCode, string CourseCode, string? Grade, string Status);

    public sealed record RecordBody(string StudentName, List<LineBody> Transcript);
}
