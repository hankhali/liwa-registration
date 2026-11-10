using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Liwa.Application.Admin;
using Liwa.Application.Explainer;
using Liwa.Domain.Activity;
using Liwa.Domain.Planning;
using Liwa.Infrastructure.Academic;
using Liwa.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Liwa.Infrastructure.Explainer;

/// <summary>Creates the LLM client for the configured provider (replaced in tests).</summary>
public interface ILlmClientFactory
{
    ILlmClient Create(AiProviderConfig config);
}

internal sealed class LlmClientFactory(IHttpClientFactory http) : ILlmClientFactory
{
    public ILlmClient Create(AiProviderConfig config) => config.Provider switch
    {
        "mock" => new MockLlmClient(),
        "openai-compatible" => new OpenAiCompatibleClient(http.CreateClient("llm"), config),
        _ => new AnthropicClient(http.CreateClient("llm"), config),
    };
}

/// <summary>
/// The AI explainer (brief §12). Grounding is enforced here, in code: the payload comes only from the planner and the
/// catalog and is refused if it carries personal data; questions are cleaned; what-if questions re-run the pure
/// planner and the model only explains the difference; replies that name unknown courses, contradict the planner or
/// are in the wrong language are thrown away and the fixed fallback is shown. Timeout + one retry, then fallback;
/// identical plan + question pairs are cached; each person has an hourly limit; every request is logged (no content).
/// </summary>
internal sealed class ExplainerService(
    LiwaDbContext db,
    StudentPlanService plans,
    ISettingsService settings,
    ILlmClientFactory clients,
    IMemoryCache cache,
    IHostEnvironment env,
    IConfiguration config,
    TimeProvider clock,
    ILogger<ExplainerService> logger) : IExplainerService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<bool> AvailableAsync(CancellationToken ct) => IsUsable(await settings.GetAiProviderAsync(ct));

    private bool IsUsable(AiProviderConfig c) => c.Enabled && (c.Provider == "mock"
        ? env.IsDevelopment()
        : !string.IsNullOrWhiteSpace(c.ApiKey) && (c.Provider != "openai-compatible" || !string.IsNullOrWhiteSpace(c.BaseUrl)));

    public async Task<ExplainResponse> ExplainForStudentAsync(Guid studentUserId, ExplainRequest request, CancellationToken ct)
    {
        var ctx = await plans.ExplainContextForStudentAsync(studentUserId, request.SectionIds, ct);
        return ctx is null ? new ExplainResponse(await AvailableAsync(ct), null, true, null, null) : await RunAsync(studentUserId, ctx, request, ct);
    }

    public async Task<ExplainResponse?> ExplainForAdvisorAsync(Guid advisorUserId, Guid planId, ExplainRequest request, CancellationToken ct)
    {
        var ctx = await plans.ExplainContextForAdvisorAsync(advisorUserId, planId, ct);
        return ctx is null ? null : await RunAsync(advisorUserId, ctx, request with { SectionIds = null }, ct);
    }

    public async Task<(bool Ok, string Message)> TestAsync(CancellationToken ct)
    {
        var c = await settings.GetAiProviderAsync(ct);
        if (!IsUsable(c)) return (false, c.Provider == "mock" && !env.IsDevelopment() ? "The mock provider only runs on a development machine." : "Switch the assistant on and enter an API key first.");
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(c.TimeoutSeconds));
            var reply = await clients.Create(c).CompleteAsync("Reply with the single word OK.", "Connection test. PAYLOAD:{}", 5, cts.Token);
            return reply.Text.Length > 0 ? (true, $"The provider answered ({c.Provider}, {c.Model}).") : (false, "The provider answered with nothing.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException)
        {
            return (false, ex is HttpRequestException { StatusCode: { } code } ? $"The provider refused the request (HTTP {(int)code}). Check the key and model." : "Couldn't reach the provider.");
        }
    }

    // ------------------------------------------------------------------ the pipeline

    private async Task<ExplainResponse> RunAsync(Guid userId, ExplainContext ctx, ExplainRequest request, CancellationToken ct)
    {
        var language = request.Language == "ar" ? "ar" : "en";
        var provider = await settings.GetAiProviderAsync(ct);
        if (!IsUsable(provider)) return new ExplainResponse(false, null, false, null, null);

        var kind = request.Kind.ToString();
        if (await OverLimitAsync(userId, provider.HourlyLimitPerUser, ct))
        {
            await LogAsync(userId, kind, AiOutcome.RateLimited, 0, 0, "hourly limit", ct);
            return new ExplainResponse(true, language == "ar" ? "لقد طرحت أسئلة كثيرة خلال هذه الساعة. حاول مرة أخرى لاحقًا." : "You've asked a lot of questions this hour. Please try again later.",
                true, null, null, RateLimited: true);
        }

        // 1. The payload: planner output + catalog entries for the courses in it. Nothing else.
        var payload = BuildPayload(ctx, language);
        var question = request.Question is { Length: > 0 } q ? ExplainerGuard.StripQuestion(q, ctx.PersonalStrings, language) : null;
        string? intent = null;
        var tokensIn = 0;
        var tokensOut = 0;

        switch (request.Kind)
        {
            case ExplainKind.WhyNot:
                if (FocusOn(ctx, payload, request.CourseCode, language) is not { } withFocus) return Declined(language, null);
                payload = withFocus;
                intent = "why";
                break;
            case ExplainKind.Question:
                var parsed = ExplainerGuard.ParseIntent(question ?? "");
                if (parsed is null)
                {
                    // One model call classifies the question into a fixed set; the code checks the answer.
                    var (classified, i, o) = await ClassifyAsync(provider, question ?? "", ctx, ct);
                    parsed = classified;
                    tokensIn += i;
                    tokensOut += o;
                }

                if (parsed is null || parsed.Kind == "none")
                {
                    await LogAsync(userId, kind, AiOutcome.Declined, tokensIn, tokensOut, "not a plan question", ct);
                    return Declined(language, null);
                }

                intent = parsed.Kind;
                if (parsed.Kind == "why")
                {
                    if (FocusOn(ctx, payload, parsed.Course, language) is not { } f) return Declined(language, intent);
                    payload = f;
                }
                else if (parsed.Kind is "drop" or "add" or "summer")
                {
                    if (WhatIf(ctx, payload, parsed) is not { } facts) return Declined(language, intent);
                    payload = payload with { WhatIf = facts };
                }

                break;
        }

        if (!ExplainerGuard.PayloadIsSafe(payload, ctx.PersonalStrings, out var unsafeReason))
        {
            // Never send personal data: refuse at the service boundary.
            logger.LogError("Explainer payload refused: it contained personal data ({Reason}).", unsafeReason);
            await LogAsync(userId, kind, AiOutcome.Refused, 0, 0, unsafeReason, ct);
            return new ExplainResponse(true, ExplainerGuard.Fallback(language), true, intent, payload.WhatIf);
        }

        // 2. Cache identical plan + question pairs.
        var system = SystemPrompt(request.Kind, language);
        var user = $"QUESTION: {question ?? "(explain the plan)"}\nPAYLOAD:{JsonSerializer.Serialize(payload, Json)}";
        var key = "explain:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{provider.Provider}|{provider.Model}|{kind}|{language}\n{user}")));
        if (cache.TryGetValue<string>(key, out var cached) && cached is not null)
        {
            await LogAsync(userId, kind, AiOutcome.Cached, 0, 0, null, ct);
            return new ExplainResponse(true, cached, false, intent, payload.WhatIf, Cached: true);
        }

        // 3. Ask the model: timeout, one retry, then the fallback.
        LlmReply? reply = null;
        string? failure = null;
        for (var attempt = 1; attempt <= 2 && reply is null; attempt++)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(provider.TimeoutSeconds));
                reply = await clients.Create(provider).CompleteAsync(system, user, request.Kind == ExplainKind.DraftNote ? 400 : 500, cts.Token);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException)
            {
                failure = ex is TaskCanceledException or OperationCanceledException ? "timeout" : "provider error";
                logger.LogWarning("Explainer attempt {Attempt} failed: {Failure}.", attempt, failure);
            }
        }

        if (reply is null)
        {
            await LogAsync(userId, kind, AiOutcome.Fallback, tokensIn, tokensOut, failure, ct);
            return new ExplainResponse(true, ExplainerGuard.Fallback(language), true, intent, payload.WhatIf);
        }

        tokensIn += reply.InputTokens;
        tokensOut += reply.OutputTokens;

        // 4. Check the reply against the payload; discard anything ungrounded.
        var verdict = ExplainerGuard.CheckReply(reply.Text, payload, language);
        if (verdict != ReplyVerdict.Ok)
        {
            logger.LogWarning("Explainer reply discarded: {Verdict}.", verdict);
            await LogAsync(userId, kind, AiOutcome.Discarded, tokensIn, tokensOut, verdict.ToString(), ct);
            return new ExplainResponse(true, ExplainerGuard.Fallback(language), true, intent, payload.WhatIf);
        }

        cache.Set(key, reply.Text, TimeSpan.FromMinutes(30));
        await LogAsync(userId, kind, AiOutcome.Answered, tokensIn, tokensOut, null, ct);
        return new ExplainResponse(true, reply.Text, false, intent, payload.WhatIf);
    }

    private static ExplainResponse Declined(string language, string? intent) => new(true,
        language == "ar"
            ? "أستطيع الإجابة فقط عن أسئلة حول خطتك، مثل «ماذا لو لم أدرس في الصيف؟» أو «لماذا لا يمكنني أخذ ITC301؟»."
            : "I can only answer questions about your plan, like “What if I skip summer?” or “Why can't I take ITC301?”.",
        true, intent, null);

    // ------------------------------------------------------------------ payload

    internal static ExplanationPayload BuildPayload(ExplainContext ctx, string language)
    {
        var courses = ctx.Courses.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
        string Title(string code) => courses.TryGetValue(code, out var c) ? (language == "ar" && c.TitleAr is { } ar ? ar : c.Title) : code;
        var sections = ctx.Input.Sections.ToDictionary(s => s.Id);
        var selectedCodes = ctx.Selection.Where(sections.ContainsKey).Select(id => sections[id].CourseCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var assessments = ctx.Recommendation.Courses.ToDictionary(c => c.CourseCode, StringComparer.OrdinalIgnoreCase);

        var planned = selectedCodes.Select(code =>
        {
            var reasons = ctx.Recommendation.Plan.FirstOrDefault(p => p.CourseCode == code)?.Reasons
                          ?? (assessments.TryGetValue(code, out var a) ? a.Reasons : []);
            return new PayloadCourse(code, Title(code), courses.TryGetValue(code, out var c) ? c.Credits : 0, "Planned", reasons);
        }).ToList();

        var notEligible = ctx.Recommendation.Courses.Where(c => c.Status == CourseStatus.NotEligible).Take(25)
            .Select(c => new PayloadCourse(c.CourseCode, Title(c.CourseCode), c.Credits, "NotEligible", c.Reasons)).ToList();

        var check = Planner.Check(ctx.Input, ctx.Selection.ToList());
        return new ExplanationPayload(language, ctx.TermName, check.TotalCredits, check.Limits?.MinCredits, check.MaxCredits, planned, notEligible,
            check.Errors.Concat(check.Warnings).ToList(), ExplainerGuard.Projection(Planner.ProjectPlan(ctx.Input, ctx.Selection.ToList())));
    }

    private static ExplanationPayload? FocusOn(ExplainContext ctx, ExplanationPayload payload, string? courseCode, string language)
    {
        var code = (courseCode ?? "").Trim().ToUpperInvariant();
        var a = ctx.Recommendation.Courses.FirstOrDefault(c => string.Equals(c.CourseCode, code, StringComparison.OrdinalIgnoreCase));
        if (a is null) return null;
        var course = ctx.Courses.FirstOrDefault(c => c.Code == a.CourseCode);
        var title = course is null ? code : language == "ar" && course.TitleAr is { } ar ? ar : course.Title;
        return payload with { Focus = new PayloadCourse(a.CourseCode, title, a.Credits, a.Status.ToString(), a.Reasons) };
    }

    /// <summary>What-if: re-run the pure planner with the modified input; the model explains only the difference.</summary>
    internal static WhatIfFacts? WhatIf(ExplainContext ctx, ExplanationPayload payload, QuestionIntent intent)
    {
        var selection = ctx.Selection.ToList();
        var before = ExplainerGuard.Projection(Planner.ProjectPlan(ctx.Input, selection));
        switch (intent.Kind)
        {
            case "summer":
                // Brief §4: summer is never assumed; compare the plan with and without it.
                // Delta > 0: skipping summer graduates that many regular semesters later.
                return new WhatIfFacts("summer", null, before, before,
                    ExplainerGuard.SemesterDelta(before.GraduationWithSummer, before.GraduationWithoutSummer, countSummer: false), []);

            case "drop":
            {
                var drop = ctx.Input.Sections.Where(s => selection.Contains(s.Id) && string.Equals(s.CourseCode, intent.Course, StringComparison.OrdinalIgnoreCase)).Select(s => s.Id).ToHashSet();
                if (drop.Count == 0) return null;
                var after = selection.Where(id => !drop.Contains(id)).ToList();
                var afterProj = ExplainerGuard.Projection(Planner.ProjectPlan(ctx.Input, after));
                return new WhatIfFacts("drop", intent.Course, before, afterProj,
                    ExplainerGuard.SemesterDelta(before.GraduationWithoutSummer, afterProj.GraduationWithoutSummer, countSummer: false),
                    Planner.Check(ctx.Input, after).Errors);
            }

            case "add":
            {
                var chosen = ctx.Input.Sections.Where(s => selection.Contains(s.Id)).ToList();
                var option = ctx.Input.Sections
                    .Where(s => string.Equals(s.CourseCode, intent.Course, StringComparison.OrdinalIgnoreCase) && s.SeatsLeft > 0)
                    .Where(s => !chosen.Any(c => c.Meetings.Any(m => s.Meetings.Any(m.Overlaps))))
                    .OrderByDescending(s => s.SeatsLeft).ThenBy(s => s.SectionCode, StringComparer.Ordinal).FirstOrDefault();
                if (option is null || chosen.Any(c => c.CourseCode == option.CourseCode)) return null;
                var after = selection.Append(option.Id).ToList();
                var afterProj = ExplainerGuard.Projection(Planner.ProjectPlan(ctx.Input, after));
                return new WhatIfFacts("add", intent.Course, before, afterProj,
                    ExplainerGuard.SemesterDelta(before.GraduationWithoutSummer, afterProj.GraduationWithoutSummer, countSummer: false),
                    Planner.Check(ctx.Input, after).Errors);
            }

            default:
                return null;
        }
    }

    private async Task<(QuestionIntent? Intent, int In, int Out)> ClassifyAsync(AiProviderConfig provider, string question, ExplainContext ctx, CancellationToken ct)
    {
        var known = ctx.Input.Catalog.Select(c => c.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(provider.TimeoutSeconds));
            var reply = await clients.Create(provider).CompleteAsync(
                "Classify a student's question about their semester plan. Reply with JSON only: {\"intent\": \"drop\"|\"add\"|\"summer\"|\"why\"|\"explain\"|\"none\", \"course\": \"<course code or null>\"}. Use none for anything that isn't about this plan.",
                $"QUESTION: {question}\nPAYLOAD:{{}}", 60, cts.Token);
            return (ExplainerGuard.ParseClassification(reply.Text, known), reply.InputTokens, reply.OutputTokens);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException)
        {
            return (null, 0, 0);
        }
    }

    private static string SystemPrompt(ExplainKind kind, string language)
    {
        var lang = language == "ar" ? "Write in Modern Standard Arabic, simple words. Keep course codes in Latin letters." : "Write in plain English.";
        var task = kind switch
        {
            ExplainKind.DraftNote => "Draft a short, friendly note (3–5 sentences) from the advisor to the student about this plan: what looks good and what to fix. Address the student as \"you\". The advisor will edit it before sending.",
            ExplainKind.WhyNot => "Explain in 2–3 sentences why the course in \"focus\" can't be taken this semester, using only its reasons.",
            ExplainKind.Question => "Answer the question in 2–4 sentences using only the payload. If \"whatIf\" is present, explain only what changes, using the planner's figures (semesterDelta > 0 means graduation is later).",
            _ => "Explain this semester plan in 4–6 short sentences a first-year student understands: why these courses, anything to watch out for, and when they could graduate.",
        };
        return $"""
            You explain a university semester plan to a first-year student at Liwa University. {task}
            Rules: use only facts in the PAYLOAD. Never mention a course code that isn't in the payload. Never say a course in "notEligible" can be taken.
            Never invent dates, numbers or courses. Don't use anyone's name: say "you". No markdown, no lists. {lang}
            Reason codes: RequiredForDegree = needed for the degree; UnlocksOtherCourses = other courses depend on it (value = how many);
            OnlyOfferedIn = only offered in that season; WaitingForGrade = allowed if the student passes a course in progress; PrerequisiteNotMet = must pass the listed course first;
            NotOfferedThisTerm = not offered this semester; NearlyFull = few seats left; BelowMinimumCredits = below the usual minimum.
            """;
    }

    // ------------------------------------------------------------------ limits and usage

    private async Task<bool> OverLimitAsync(Guid userId, int limit, CancellationToken ct)
    {
        var since = clock.GetUtcNow().AddHours(-1);
        var used = await db.AiRequests.CountAsync(r => r.UserId == userId && r.At >= since && r.Outcome != AiOutcome.RateLimited && r.Outcome != AiOutcome.Cached, ct);
        return used >= limit;
    }

    private async Task LogAsync(Guid userId, string kind, AiOutcome outcome, int tokensIn, int tokensOut, string? reason, CancellationToken ct)
    {
        db.AiRequests.Add(new AiRequestLog(Guid.NewGuid(), clock.GetUtcNow(), userId, kind, outcome, tokensIn, tokensOut, reason));
        await db.SaveChangesAsync(ct);
    }

    public async Task<AiUsageView> UsageAsync(CancellationToken ct)
    {
        var c = await settings.GetAiProviderAsync(ct);
        var since = clock.GetUtcNow().AddDays(-30);
        var rows = await db.AiRequests.AsNoTracking().Where(r => r.At >= since).ToListAsync(ct);
        var priceIn = config.GetValue("Ai:PricePerMillionInputUsd", 3m);
        var priceOut = config.GetValue("Ai:PricePerMillionOutputUsd", 15m);
        var days = rows.GroupBy(r => r.At.UtcDateTime.Date).OrderByDescending(g => g.Key).Select(g => new AiUsageDay(
            g.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            g.Count(r => r.Outcome is not (AiOutcome.RateLimited)),
            g.Sum(r => r.InputTokens), g.Sum(r => r.OutputTokens),
            Math.Round((g.Sum(r => r.InputTokens) * priceIn + g.Sum(r => r.OutputTokens) * priceOut) / 1_000_000m, 4),
            g.Count(r => r.Outcome == AiOutcome.Fallback), g.Count(r => r.Outcome == AiOutcome.Discarded), g.Count(r => r.Outcome == AiOutcome.Cached))).ToList();
        var recent = rows.Where(r => r.Outcome is AiOutcome.Fallback or AiOutcome.Discarded or AiOutcome.RateLimited).OrderByDescending(r => r.At).Take(20)
            .Select(r => new AiUsageEvent(r.At, r.Outcome switch { AiOutcome.Fallback => "Fallback", AiOutcome.Discarded => "Discard", _ => "RateLimited" }, r.Reason ?? "")).ToList();
        return new AiUsageView(c.Enabled, c.Provider, c.Model, days, recent);
    }

    /// <summary>Brief §4: the mock provider is only allowed in Development; any other environment refuses to start with it.</summary>
    internal static void EnsureProviderAllowed(AiProviderConfig c, IHostEnvironment env)
    {
        if (c.Provider == "mock" && !env.IsDevelopment())
            throw new InvalidOperationException("The mock AI provider is only allowed in Development. Choose a real provider (Ai:Provider or Settings) or switch the assistant off.");
    }
}
