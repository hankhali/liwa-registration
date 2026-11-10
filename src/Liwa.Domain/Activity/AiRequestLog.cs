namespace Liwa.Domain.Activity;

public enum AiOutcome
{
    /// <summary>The model's reply passed every check and was shown.</summary>
    Answered = 1,

    /// <summary>Served from the cache (no provider call).</summary>
    Cached = 2,

    /// <summary>Timeout or provider error after one retry: the fixed fallback was shown.</summary>
    Fallback = 3,

    /// <summary>The reply failed a grounding check and was thrown away: the fallback was shown.</summary>
    Discarded = 4,

    /// <summary>Over the person's hourly limit.</summary>
    RateLimited = 5,

    /// <summary>A question the planner can't compute: politely declined, no provider call.</summary>
    Declined = 6,

    /// <summary>The payload was refused because it contained personal data (should never happen).</summary>
    Refused = 7,
}

/// <summary>
/// One explainer request, for the AI usage page and the hourly limit. Stores counts and reasons only — never the
/// question, the payload or the reply (brief §13: no PII to external services, nothing personal kept).
/// </summary>
public sealed class AiRequestLog
{
    private AiRequestLog() { } // EF Core

    public AiRequestLog(Guid id, DateTimeOffset at, Guid userId, string kind, AiOutcome outcome, int inputTokens, int outputTokens, string? reason)
    {
        Id = id;
        At = at;
        UserId = userId;
        Kind = kind;
        Outcome = outcome;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        Reason = reason;
    }

    public Guid Id { get; private set; }

    public DateTimeOffset At { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>Plan, WhyNot, Question, DraftNote, Classify, Test.</summary>
    public string Kind { get; private set; } = string.Empty;

    public AiOutcome Outcome { get; private set; }

    public int InputTokens { get; private set; }

    public int OutputTokens { get; private set; }

    /// <summary>Why a reply was discarded or fell back, e.g. "UnknownCourse", "timeout".</summary>
    public string? Reason { get; private set; }
}
