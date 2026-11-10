using Liwa.Domain.Planning;

namespace Liwa.Application.Explainer;

// ============================================================================================
// AI explainer (brief §12). The model never decides anything: it only explains, in plain words,
// what the deterministic planner produced. The payload is built only from planner output plus the
// catalog entries of the courses in it, and contains no name, university ID, email or advisor name.
// ============================================================================================

public enum ExplainKind
{
    /// <summary>"Explain my plan": 4–6 sentences a first-year understands.</summary>
    Plan = 1,

    /// <summary>"Why can't I take X?" from a not-eligible row.</summary>
    WhyNot = 2,

    /// <summary>A question about the plan (what-if questions re-run the planner).</summary>
    Question = 3,

    /// <summary>Advisor only: a draft note to the student, edited before sending (never sent automatically).</summary>
    DraftNote = 4,
}

/// <summary>A course in the payload: code, title in the reader's language, credits and the planner's reasons.</summary>
public sealed record PayloadCourse(string Code, string Title, int Credits, string Status, IReadOnlyList<PlannerReason> Reasons);

public sealed record PayloadProjection(string? GraduationWithoutSummer, int SemestersWithoutSummer, string? GraduationWithSummer, int SemestersWithSummer);

/// <summary>What the planner's re-run says for a what-if question (shown under the answer as the planner's own figures).</summary>
public sealed record WhatIfFacts(string Intent, string? Course, PayloadProjection Before, PayloadProjection After, int SemesterDelta,
    IReadOnlyList<PlannerReason> NewProblems);

/// <summary>
/// Everything the model sees. Built from the planner result and the catalog only (ExplainerGuard refuses anything
/// that carries personal data).
/// </summary>
public sealed record ExplanationPayload(
    string Language,
    string Term,
    int TotalCredits,
    int? MinCredits,
    int MaxCredits,
    IReadOnlyList<PayloadCourse> Planned,
    IReadOnlyList<PayloadCourse> NotEligible,
    IReadOnlyList<PlannerReason> Warnings,
    PayloadProjection Projection,
    PayloadCourse? Focus = null,
    WhatIfFacts? WhatIf = null);

public sealed record ExplainRequest(ExplainKind Kind, string? CourseCode, string? Question, string? Language, IReadOnlyList<Guid>? SectionIds);

/// <param name="Available">False when no AI provider is configured: the app shows "Not available" and nothing else changes.</param>
/// <param name="Fallback">True when the reply was replaced by the fixed fallback (timeout, error, discarded reply, declined).</param>
/// <param name="Facts">The planner's own figures for a what-if question.</param>
public sealed record ExplainResponse(bool Available, string? Text, bool Fallback, string? Intent, WhatIfFacts? Facts, bool Cached = false, bool RateLimited = false);

public sealed record LlmReply(string Text, int InputTokens, int OutputTokens);

/// <summary>One chat completion. Implementations: Anthropic Messages API, OpenAI-compatible, and a mock for demos.</summary>
public interface ILlmClient
{
    Task<LlmReply> CompleteAsync(string system, string user, int maxTokens, CancellationToken ct);
}

public interface IExplainerService
{
    Task<bool> AvailableAsync(CancellationToken ct);

    Task<ExplainResponse> ExplainForStudentAsync(Guid studentUserId, ExplainRequest request, CancellationToken ct);

    /// <summary>The same explainer on the advisor's review page, run on the submitted plan.</summary>
    Task<ExplainResponse?> ExplainForAdvisorAsync(Guid advisorUserId, Guid planId, ExplainRequest request, CancellationToken ct);

    /// <summary>Calls the provider with a tiny prompt (no student data) to check the settings.</summary>
    Task<(bool Ok, string Message)> TestAsync(CancellationToken ct);

    Task<AiUsageView> UsageAsync(CancellationToken ct);
}

public sealed record AiUsageDay(string Date, int Requests, int InputTokens, int OutputTokens, decimal EstimatedCostUsd, int Fallbacks, int Discards, int CacheHits);

public sealed record AiUsageEvent(DateTimeOffset At, string Kind, string Reason);

public sealed record AiUsageView(bool Enabled, string Provider, string Model, IReadOnlyList<AiUsageDay> Days, IReadOnlyList<AiUsageEvent> Recent);
