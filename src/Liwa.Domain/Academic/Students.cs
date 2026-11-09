namespace Liwa.Domain.Academic;

/// <summary>Academic status as reported by the registrar (PeopleSoft), not computed here.</summary>
public enum AcademicStatus
{
    Good = 1,
    Probation = 2,
}

/// <summary>Class standing by credits earned (used by Standing(...) prerequisites and credit policy).</summary>
public enum Standing
{
    Freshman = 1,
    Sophomore = 2,
    Junior = 3,
    Senior = 4,
}

public enum TranscriptStatus
{
    /// <summary>Finished with a final grade.</summary>
    Completed = 1,

    /// <summary>Currently enrolled; no final grade yet.</summary>
    InProgress = 2,

    /// <summary>Withdrawn (grade W). Never counts.</summary>
    Withdrawn = 3,
}

/// <summary>One course attempt on a student's transcript.</summary>
public sealed class TranscriptEntry
{
    private TranscriptEntry() { } // EF Core

    internal TranscriptEntry(Guid id, Guid studentRecordId, Guid courseId, string termCode, int credits, string? grade, TranscriptStatus status)
    {
        if (credits is < 0 or > Course.MaxCredits) throw new ArgumentOutOfRangeException(nameof(credits));
        if (status == TranscriptStatus.Completed && !Grades.IsKnown(grade))
            throw new ArgumentException($"'{grade}' is not a recognised final grade.", nameof(grade));
        Id = id;
        StudentRecordId = studentRecordId;
        CourseId = courseId;
        TermCode = termCode.Trim().ToUpperInvariant();
        Credits = credits;
        Grade = status == TranscriptStatus.Completed ? Grades.Normalize(grade!) : status == TranscriptStatus.Withdrawn ? "W" : null;
        Status = status;
    }

    public Guid Id { get; private set; }

    public Guid StudentRecordId { get; private set; }

    public Guid CourseId { get; private set; }

    public string TermCode { get; private set; } = string.Empty;

    public int Credits { get; private set; }

    /// <summary>Letter grade for completed attempts ("A", "B+", "F", "P"); "W" if withdrawn; null while in progress.</summary>
    public string? Grade { get; private set; }

    public TranscriptStatus Status { get; private set; }
}

/// <summary>Credit accepted from another institution (or advanced placement). Counts toward the degree, not the GPA.</summary>
public sealed class TransferCredit
{
    private TransferCredit() { } // EF Core

    internal TransferCredit(Guid id, Guid studentRecordId, Guid courseId, int credits, string? source)
    {
        Id = id;
        StudentRecordId = studentRecordId;
        CourseId = courseId;
        Credits = credits;
        Source = string.IsNullOrWhiteSpace(source) ? null : source.Trim();
    }

    public Guid Id { get; private set; }

    public Guid StudentRecordId { get; private set; }

    public Guid CourseId { get; private set; }

    public int Credits { get; private set; }

    public string? Source { get; private set; }
}

/// <summary>What the university knows about a student's studies: program, catalog year, transcript, transfer credit.</summary>
public sealed class StudentRecord
{
    private readonly List<TranscriptEntry> _transcript = [];
    private readonly List<TransferCredit> _transfers = [];

    private StudentRecord() { } // EF Core

    public StudentRecord(Guid id, Guid userId, string studentId, Guid programId, int catalogYear, string admitTermCode)
    {
        if (string.IsNullOrWhiteSpace(studentId)) throw new ArgumentException("Student ID is required.", nameof(studentId));
        Id = id;
        UserId = userId;
        StudentId = studentId.Trim();
        Update(programId, catalogYear, admitTermCode, AcademicStatus.Good, null);
    }

    public Guid Id { get; private set; }

    /// <summary>The student's sign-in account.</summary>
    public Guid UserId { get; private set; }

    /// <summary>University ID (PeopleSoft EMPLID). Unique.</summary>
    public string StudentId { get; private set; } = string.Empty;

    public Guid ProgramId { get; private set; }

    public int CatalogYear { get; private set; }

    public string AdmitTermCode { get; private set; } = string.Empty;

    public AcademicStatus Status { get; private set; }

    public Guid? AdvisorUserId { get; private set; }

    public IReadOnlyList<TranscriptEntry> Transcript => _transcript;

    public IReadOnlyList<TransferCredit> TransferCredits => _transfers;

    public void Update(Guid programId, int catalogYear, string admitTermCode, AcademicStatus status, Guid? advisorUserId)
    {
        if (catalogYear is < 2000 or > 2100) throw new ArgumentOutOfRangeException(nameof(catalogYear));
        ProgramId = programId;
        CatalogYear = catalogYear;
        AdmitTermCode = (admitTermCode ?? string.Empty).Trim().ToUpperInvariant();
        Status = status;
        AdvisorUserId = advisorUserId;
    }

    /// <summary>Replaces the whole academic history (imports always send the full transcript).</summary>
    public void ReplaceHistory(
        IEnumerable<(Guid CourseId, string TermCode, int Credits, string? Grade, TranscriptStatus Status)> attempts,
        IEnumerable<(Guid CourseId, int Credits, string? Source)> transfers)
    {
        _transcript.Clear();
        foreach (var a in attempts)
            _transcript.Add(new TranscriptEntry(Guid.NewGuid(), Id, a.CourseId, a.TermCode, a.Credits, a.Grade, a.Status));
        _transfers.Clear();
        foreach (var t in transfers)
            _transfers.Add(new TransferCredit(Guid.NewGuid(), Id, t.CourseId, t.Credits, t.Source));
    }
}
