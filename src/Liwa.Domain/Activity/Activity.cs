namespace Liwa.Domain.Activity;

/// <summary>
/// Who did what, when. Status changes record the plan versions before and after; imports record their
/// summary. Append-only.
/// </summary>
public sealed class AuditEntry
{
    private AuditEntry() { } // EF Core

    public AuditEntry(Guid id, DateTimeOffset at, Guid? actorUserId, string action, string entityType, string? entityId, string? before, string? after)
    {
        if (string.IsNullOrWhiteSpace(action)) throw new ArgumentException("Action is required.", nameof(action));
        Id = id;
        At = at;
        ActorUserId = actorUserId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        Before = before;
        After = after;
    }

    public Guid Id { get; private set; }

    public DateTimeOffset At { get; private set; }

    /// <summary>Null for system actions (seeding, scheduled jobs).</summary>
    public Guid? ActorUserId { get; private set; }

    /// <summary>e.g. "import.courses", "plan.submitted".</summary>
    public string Action { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public string? EntityId { get; private set; }

    /// <summary>JSON snapshot before the change.</summary>
    public string? Before { get; private set; }

    /// <summary>JSON snapshot after the change.</summary>
    public string? After { get; private set; }
}

/// <summary>
/// A message for one person. Stores a translation key and parameters rather than text, so it shows in
/// the reader's language (English or Arabic).
/// </summary>
public sealed class Notification
{
    private Notification() { } // EF Core

    public Notification(Guid id, Guid userId, DateTimeOffset createdAt, string messageKey, string? parameters, string? link)
    {
        if (string.IsNullOrWhiteSpace(messageKey)) throw new ArgumentException("Message key is required.", nameof(messageKey));
        Id = id;
        UserId = userId;
        CreatedAt = createdAt;
        MessageKey = messageKey;
        Parameters = parameters;
        Link = link;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string MessageKey { get; private set; } = string.Empty;

    /// <summary>JSON object of values for the message, e.g. {"advisor":"Dr. Ahmed"}.</summary>
    public string? Parameters { get; private set; }

    /// <summary>In-app path to open, e.g. "/student/plan".</summary>
    public string? Link { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    public void MarkRead(DateTimeOffset at) => ReadAt ??= at;
}
