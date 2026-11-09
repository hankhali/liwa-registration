namespace Liwa.Domain.Planning;

/// <summary>Where a student's plan is in the advisor review workflow (transitions arrive in M4/M5).</summary>
public enum PlanStatus
{
    Draft = 1,
    Submitted = 2,
    AdvisorReviewing = 3,
    Approved = 4,
    Rejected = 5,
    ChangesProposed = 6,

    /// <summary>An approved plan whose prerequisites failed when grades posted.</summary>
    NeedsReview = 7,
}

/// <summary>A student's plan for one term. Every change creates a new <see cref="PlanVersion"/>; nothing is overwritten.</summary>
public sealed class Plan
{
    private readonly List<PlanVersion> _versions = [];

    private Plan() { } // EF Core

    public Plan(Guid id, Guid studentRecordId, Guid termId, DateTimeOffset createdAt)
    {
        Id = id;
        StudentRecordId = studentRecordId;
        TermId = termId;
        Status = PlanStatus.Draft;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid StudentRecordId { get; private set; }

    public Guid TermId { get; private set; }

    public PlanStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<PlanVersion> Versions => _versions;

    public PlanVersion? Current => _versions.MaxBy(v => v.Number);

    public PlanVersion AddVersion(IEnumerable<Guid> sectionIds, Guid authorUserId, string? note, DateTimeOffset at)
    {
        var version = new PlanVersion(Guid.NewGuid(), Id, (Current?.Number ?? 0) + 1, authorUserId, note, at, sectionIds);
        _versions.Add(version);
        UpdatedAt = at;
        return version;
    }

    public void SetStatus(PlanStatus status, DateTimeOffset at)
    {
        Status = status;
        UpdatedAt = at;
    }
}

/// <summary>An immutable snapshot of a plan's sections.</summary>
public sealed class PlanVersion
{
    private readonly List<PlanVersionSection> _sections = [];

    private PlanVersion() { } // EF Core

    internal PlanVersion(Guid id, Guid planId, int number, Guid authorUserId, string? note, DateTimeOffset createdAt, IEnumerable<Guid> sectionIds)
    {
        Id = id;
        PlanId = planId;
        Number = number;
        AuthorUserId = authorUserId;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        CreatedAt = createdAt;
        _sections.AddRange(sectionIds.Distinct().Select(s => new PlanVersionSection(id, s)));
    }

    public Guid Id { get; private set; }

    public Guid PlanId { get; private set; }

    public int Number { get; private set; }

    public Guid AuthorUserId { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<PlanVersionSection> Sections => _sections;
}

public sealed class PlanVersionSection
{
    private PlanVersionSection() { } // EF Core

    internal PlanVersionSection(Guid planVersionId, Guid sectionId)
    {
        PlanVersionId = planVersionId;
        SectionId = sectionId;
    }

    public Guid PlanVersionId { get; private set; }

    public Guid SectionId { get; private set; }
}
