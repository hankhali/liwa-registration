using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Liwa.Application.Admin;
using Liwa.Application.Explainer;
using Liwa.Infrastructure.Explainer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Liwa.IntegrationTests;

/// <summary>A scripted model: each test says what the "provider" answers, and every prompt is recorded.</summary>
public sealed class FakeLlm : ILlmClientFactory, ILlmClient
{
    public ConcurrentQueue<Func<string, string, LlmReply>> Script { get; } = new();

    public ConcurrentBag<(string System, string User)> Calls { get; } = [];

    public Func<string, string, LlmReply> Default { get; set; } = (_, _) => new LlmReply("Your plan is ready.", 100, 20);

    public ILlmClient Create(AiProviderConfig config) => this;

    public Task<LlmReply> CompleteAsync(string system, string user, int maxTokens, CancellationToken ct)
    {
        Calls.Add((system, user));
        var step = Script.TryDequeue(out var s) ? s : Default;
        return Task.FromResult(step(system, user));
    }
}

/// <summary>Own database with the fake model in place of a real provider.</summary>
public sealed class ExplainerApiFactory : LiwaApiFactory
{
    public FakeLlm Llm { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(s => s.AddSingleton<ILlmClientFactory>(Llm));
    }
}

public sealed class ExplainerTests(ExplainerApiFactory factory) : IClassFixture<ExplainerApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static async Task<T> Read<T>(HttpResponseMessage r) => (await r.Content.ReadFromJsonAsync<T>(Json))!;

    private async Task ConfigureAsync(bool enabled = true, int hourlyLimit = 100)
    {
        var admin = await factory.SignedInClientAsync("admin");
        var r = await admin.PutAsJsonAsync("/api/admin/settings/ai",
            new { enabled, provider = "anthropic", model = "claude-sonnet-5-5", apiKey = "sk-test-0000", timeoutSeconds = 5, hourlyLimitPerUser = hourlyLimit });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    private static async Task<ExplainBody> AskAsync(HttpClient client, object body) =>
        await Read<ExplainBody>(await client.PostAsJsonAsync("/api/students/me/explain", body, Json));

    [Fact]
    public async Task Without_a_provider_the_explainer_is_simply_not_available()
    {
        await ConfigureAsync(enabled: false);
        var student = await factory.SignedInClientAsync("student.first");
        Assert.False((await Read<AvailableBody>(await student.GetAsync("/api/me/explainer"))).Available);
        var r = await AskAsync(student, new { kind = "Plan", language = "en" });
        Assert.False(r.Available);
        Assert.Null(r.Text);
    }

    [Fact]
    public async Task Explaining_a_plan_sends_planner_facts_only_and_never_personal_data()
    {
        await ConfigureAsync();
        var student = await factory.SignedInClientAsync("student.honors");
        factory.Llm.Script.Enqueue((_, user) =>
        {
            Assert.DoesNotContain("Aisha", user, StringComparison.Ordinal);
            Assert.DoesNotContain("Nuaimi", user, StringComparison.Ordinal);
            Assert.DoesNotContain("202500103", user, StringComparison.Ordinal);
            Assert.DoesNotContain("@lu.ac.ae", user, StringComparison.Ordinal);
            Assert.DoesNotContain("Zaabi", user, StringComparison.Ordinal); // the advisor
            Assert.Contains("\"planned\"", user, StringComparison.Ordinal);
            return new LlmReply("Your plan has the courses you need first, and none of the classes clash.", 500, 40);
        });

        var r = await AskAsync(student, new { kind = "Plan", language = "en" });

        Assert.True(r.Available);
        Assert.False(r.Fallback);
        Assert.Equal("Your plan has the courses you need first, and none of the classes clash.", r.Text);
    }

    [Fact]
    public async Task An_ungrounded_reply_is_discarded_and_the_fallback_shown()
    {
        await ConfigureAsync();
        var student = await factory.SignedInClientAsync("student.mid2");
        factory.Llm.Script.Enqueue((_, _) => new LlmReply("You should take ZZZ999 as well.", 100, 10));

        var r = await AskAsync(student, new { kind = "Plan", language = "en", question = "discard-check" });

        Assert.True(r.Fallback);
        Assert.Equal(ExplainerGuard.FallbackEn, r.Text);
        var admin = await factory.SignedInClientAsync("admin");
        var usage = await Read<UsageBody>(await admin.GetAsync("/api/admin/ai-usage"));
        Assert.Contains(usage.Recent, e => e.Kind == "Discard" && e.Reason == "UnknownCourse");
    }

    [Fact]
    public async Task A_timeout_is_retried_once_then_falls_back()
    {
        await ConfigureAsync();
        var student = await factory.SignedInClientAsync("student.mid1");
        factory.Llm.Script.Enqueue((_, _) => throw new TaskCanceledException());
        factory.Llm.Script.Enqueue((_, _) => new LlmReply("Your plan keeps you on track to graduate.", 100, 10));
        var retried = await AskAsync(student, new { kind = "Plan", language = "en", question = "retry-check" });
        Assert.False(retried.Fallback); // the retry worked

        factory.Llm.Script.Enqueue((_, _) => throw new TaskCanceledException());
        factory.Llm.Script.Enqueue((_, _) => throw new HttpRequestException("down"));
        var fell = await AskAsync(student, new { kind = "Plan", language = "ar", question = "fallback-check" });
        Assert.True(fell.Fallback);
        Assert.Equal(ExplainerGuard.FallbackAr, fell.Text);
    }

    [Fact]
    public async Task Identical_questions_are_answered_from_the_cache()
    {
        await ConfigureAsync();
        var student = await factory.SignedInClientAsync("student.media");
        var before = factory.Llm.Calls.Count;
        var first = await AskAsync(student, new { kind = "Plan", language = "en", question = "cache-check" });
        var second = await AskAsync(student, new { kind = "Plan", language = "en", question = "cache-check" });

        Assert.False(first.Cached);
        Assert.True(second.Cached);
        Assert.Equal(first.Text, second.Text);
        Assert.Equal(before + 1, factory.Llm.Calls.Count);
    }

    [Fact]
    public async Task What_if_questions_rerun_the_planner_and_the_model_explains_only_the_difference()
    {
        await ConfigureAsync();
        var student = await factory.SignedInClientAsync("student.new");
        var plan = await Read<PlanFlowTests.ViewBody>(await student.PostAsync("/api/students/me/planning", null));
        var drop = plan.Recommendation.Plan.First(p => !p.CourseCode.EndsWith('L')).CourseCode;
        factory.Llm.Script.Enqueue((_, user) =>
        {
            Assert.Contains("\"whatIf\"", user, StringComparison.Ordinal);
            return new LlmReply($"If you drop {drop}, the planner's figures below show what changes.", 300, 20);
        });

        var r = await AskAsync(student, new { kind = "Question", language = "en", question = $"What if I drop {drop}?" });

        Assert.Equal("drop", r.Intent);
        Assert.NotNull(r.Facts);
        Assert.Equal(drop, r.Facts.Course);
        Assert.NotNull(r.Facts.After.GraduationWithoutSummer);
        Assert.True(r.Facts.SemesterDelta >= 0);
    }

    [Fact]
    public async Task Questions_the_planner_cannot_compute_are_declined()
    {
        await ConfigureAsync();
        var student = await factory.SignedInClientAsync("student.parttime");
        factory.Llm.Script.Enqueue((_, _) => new LlmReply("{\"intent\":\"none\",\"course\":null}", 30, 5)); // the classification call

        var r = await AskAsync(student, new { kind = "Question", language = "en", question = "Write my essay about camels" });

        Assert.True(r.Fallback);
        Assert.Contains("only answer questions about your plan", r.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Each_person_has_an_hourly_limit()
    {
        await ConfigureAsync(hourlyLimit: 2);
        var student = await factory.SignedInClientAsync("student.evening");
        await AskAsync(student, new { kind = "Plan", language = "en", question = "limit-1" });
        await AskAsync(student, new { kind = "Plan", language = "en", question = "limit-2" });
        var third = await AskAsync(student, new { kind = "Plan", language = "en", question = "limit-3" });
        Assert.True(third.RateLimited);
        await ConfigureAsync(); // restore for the other tests
    }

    [Fact]
    public async Task Advisors_get_the_explainer_and_a_draft_note_that_is_never_sent()
    {
        await ConfigureAsync();
        var student = await factory.SignedInClientAsync("student.transfer");
        var view = await Read<PlanFlowTests.ViewBody>(await student.PostAsync("/api/students/me/planning", null));
        var ids = view.Recommendation.Plan.Select(p => p.SectionId).ToArray();
        var check = await Read<PlanFlowTests.CheckBody>(await student.PostAsJsonAsync("/api/students/me/plan/check", new { sectionIds = ids }));
        Assert.Equal(HttpStatusCode.OK, (await student.PostAsJsonAsync("/api/students/me/plan",
            new { sectionIds = ids, submit = true, acknowledgedWarnings = check.Warnings.Select(w => $"{w.Code}:{string.Join(',', w.Courses)}:{w.Value}") })).StatusCode);
        var advisor = await factory.SignedInClientAsync("advisor.business");
        var planId = (await Read<List<AdvisorFlowTests.QueueBody>>(await advisor.GetAsync("/api/advisor/plans"))).Single(q => q.StudentId == "202500112").PlanId;
        factory.Llm.Script.Enqueue((system, user) =>
        {
            Assert.Contains("Draft a short, friendly note", system, StringComparison.Ordinal);
            Assert.DoesNotContain("Maryam", user, StringComparison.Ordinal);
            return new LlmReply("Your plan looks good and keeps you on track.", 200, 20);
        });

        var note = await Read<ExplainBody>(await advisor.PostAsJsonAsync($"/api/advisor/plans/{planId}/explain", new { kind = "DraftNote", language = "en" }, Json));

        Assert.Equal("Your plan looks good and keeps you on track.", note.Text);
        Assert.DoesNotContain((await Read<AdvisorFlowTests.NotificationsBody>(await student.GetAsync("/api/notifications"))).Items,
            n => n.MessageKey.Contains("Rejected", StringComparison.Ordinal) || n.MessageKey.Contains("Approved", StringComparison.Ordinal));
        var other = await factory.SignedInClientAsync("advisor.it");
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/advisor/plans/{planId}/explain", new { kind = "Plan" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await student.PostAsJsonAsync("/api/students/me/explain", new { kind = "DraftNote" }, Json)).StatusCode);
    }

    public sealed record AvailableBody(bool Available);

    public sealed record ProjBody(string? GraduationWithoutSummer, int SemestersWithoutSummer, string? GraduationWithSummer, int SemestersWithSummer);

    public sealed record FactsBody(string Intent, string? Course, ProjBody Before, ProjBody After, int SemesterDelta);

    public sealed record ExplainBody(bool Available, string? Text, bool Fallback, string? Intent, FactsBody? Facts, bool Cached, bool RateLimited);

    public sealed record EventBody(string Kind, string Reason);

    public sealed record UsageBody(bool Enabled, List<EventBody> Recent);
}
