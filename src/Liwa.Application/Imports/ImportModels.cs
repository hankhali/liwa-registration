namespace Liwa.Application.Imports;

/// <summary>What a CSV file contains. Import in this order when loading everything (later kinds refer to earlier ones).</summary>
public enum ImportKind
{
    Terms = 1,
    Courses = 2,
    Programs = 3,
    DegreePlans = 4,
    Sections = 5,
    Students = 6,
    Transcripts = 7,
}

public enum IssueSeverity
{
    /// <summary>The file cannot be imported until this is fixed. Nothing is saved.</summary>
    Error = 1,

    /// <summary>Imported, but someone should look at it.</summary>
    Warning = 2,
}

/// <summary>One problem in a file. <see cref="Line"/> is the line in the CSV file (the header is line 1).</summary>
public sealed record ImportIssue(IssueSeverity Severity, int Line, string? Column, string Message);

/// <summary>
/// The result of importing (or dry-running) one file. Imports are all-or-nothing: if there is any error,
/// nothing is saved. A dry run validates and counts changes but never saves.
/// </summary>
public sealed class ImportReport(ImportKind kind, bool dryRun)
{
    private readonly List<ImportIssue> _issues = [];

    public ImportKind Kind { get; } = kind;

    public bool DryRun { get; } = dryRun;

    public int RowsRead { get; set; }

    public int Created { get; set; }

    public int Updated { get; set; }

    public int Unchanged { get; set; }

    /// <summary>Rows taken away by a replacement (courses dropped from a degree plan, attempts dropped from a transcript).</summary>
    public int Removed { get; set; }

    /// <summary>True when the changes were written to the database.</summary>
    public bool Saved { get; set; }

    /// <summary>The catalog version this save created (null for dry runs and rejected files).</summary>
    public int? CatalogVersion { get; set; }

    public IReadOnlyList<ImportIssue> Issues => _issues;

    public int ErrorCount => _issues.Count(i => i.Severity == IssueSeverity.Error);

    public int WarningCount => _issues.Count(i => i.Severity == IssueSeverity.Warning);

    public bool HasErrors => ErrorCount > 0;

    public void Error(int line, string? column, string message) => _issues.Add(new ImportIssue(IssueSeverity.Error, line, column, message));

    public void Warning(int line, string? column, string message) => _issues.Add(new ImportIssue(IssueSeverity.Warning, line, column, message));
}

public interface IImportService
{
    /// <summary>Validates and (unless <paramref name="dryRun"/>) saves one CSV file; a saved file creates a catalog version.</summary>
    Task<ImportReport> ImportAsync(ImportKind kind, Stream csv, bool dryRun, Guid? actorUserId, CancellationToken ct,
        Liwa.Domain.Activity.ImportSource source = Liwa.Domain.Activity.ImportSource.Upload, string? fileName = null);
}

/// <summary>A saved change, as listed on the Imports page and for advisors.</summary>
public sealed record CatalogVersionView(int Number, string Source, string Kind, string? FileName, DateTimeOffset At, string? ActorName,
    int RowsRead, int Created, int Updated, int Unchanged, int Removed);

/// <summary>The import kinds in their URL form.</summary>
public static class ImportKinds
{
    public static string Slug(ImportKind kind) => kind switch
    {
        ImportKind.DegreePlans => "degree-plans",
        _ => kind.ToString().ToLowerInvariant(),
    };

    public static bool TryParse(string slug, out ImportKind kind) =>
        Enum.TryParse(slug.Replace("-", string.Empty, StringComparison.Ordinal), ignoreCase: true, out kind)
        && !int.TryParse(slug, out _) && Enum.IsDefined(kind);
}
