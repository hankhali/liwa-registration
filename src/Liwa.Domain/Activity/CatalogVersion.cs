namespace Liwa.Domain.Activity;

/// <summary>Where a change to the catalog or student data came from.</summary>
public enum ImportSource
{
    /// <summary>A file uploaded on the Imports page.</summary>
    Upload = 1,

    /// <summary>A row edited on a Catalog screen (it still goes through the importer).</summary>
    Edit = 2,

    /// <summary>The demo data loaded at start-up.</summary>
    Seed = 3,

    /// <summary>A removal (for example a degree plan).</summary>
    Removal = 4,
}

/// <summary>
/// One saved change to the catalog or student data (brief §11: number, source, time, who, row counts). Every saved
/// import, every edited row and every removal creates one, so the history is complete. Visible to advisors.
/// </summary>
public sealed class CatalogVersion
{
    private CatalogVersion() { } // EF Core

    public CatalogVersion(Guid id, int number, ImportSource source, string kind, string? fileName, DateTimeOffset at, Guid? actorUserId,
        int rowsRead, int created, int updated, int unchanged, int removed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        Id = id;
        Number = number;
        Source = source;
        Kind = kind;
        FileName = string.IsNullOrWhiteSpace(fileName) ? null : Path.GetFileName(fileName.Trim());
        At = at;
        ActorUserId = actorUserId;
        RowsRead = rowsRead;
        Created = created;
        Updated = updated;
        Unchanged = unchanged;
        Removed = removed;
    }

    public Guid Id { get; private set; }

    /// <summary>1, 2, 3 … in the order changes were saved.</summary>
    public int Number { get; private set; }

    public ImportSource Source { get; private set; }

    /// <summary>terms, courses, programs, degree-plans, sections, students, transcripts.</summary>
    public string Kind { get; private set; } = string.Empty;

    public string? FileName { get; private set; }

    public DateTimeOffset At { get; private set; }

    /// <summary>Null for the demo seed.</summary>
    public Guid? ActorUserId { get; private set; }

    public int RowsRead { get; private set; }

    public int Created { get; private set; }

    public int Updated { get; private set; }

    public int Unchanged { get; private set; }

    public int Removed { get; private set; }
}
