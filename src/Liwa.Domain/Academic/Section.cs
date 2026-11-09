namespace Liwa.Domain.Academic;

public enum DeliveryMode
{
    OnCampus = 1,
    Online = 2,
    Hybrid = 3,
}

/// <summary>One weekly meeting of a section, e.g. Monday 5:00–6:15 pm.</summary>
public sealed record MeetingTime(DayOfWeek Day, TimeOnly Start, TimeOnly End)
{
    public bool Overlaps(MeetingTime other) => Day == other.Day && Start < other.End && other.Start < End;

    public TimeSpan Duration => End - Start;
}

/// <summary>A scheduled class of a course in a term, e.g. ITC201 section 02 in Spring 2027.</summary>
public sealed class Section
{
    private readonly List<MeetingTime> _meetings = [];

    private Section() { } // EF Core

    public Section(Guid id, Guid termId, Guid courseId, string sectionCode)
    {
        if (string.IsNullOrWhiteSpace(sectionCode)) throw new ArgumentException("Section code is required.", nameof(sectionCode));
        Id = id;
        TermId = termId;
        CourseId = courseId;
        SectionCode = sectionCode.Trim().ToUpperInvariant();
        Delivery = DeliveryMode.OnCampus;
    }

    public Guid Id { get; private set; }

    public Guid TermId { get; private set; }

    public Guid CourseId { get; private set; }

    /// <summary>e.g. "01", "E1" (evening). Unique per course and term.</summary>
    public string SectionCode { get; private set; } = string.Empty;

    public DeliveryMode Delivery { get; private set; }

    public string? Instructor { get; private set; }

    public string? Room { get; private set; }

    public int Capacity { get; private set; }

    public int Enrolled { get; private set; }

    /// <summary>Reserved for a future waitlist feature; not used by the planner yet.</summary>
    public bool Waitlist { get; private set; }

    public IReadOnlyList<MeetingTime> Meetings => _meetings;

    public int SeatsLeft => Math.Max(0, Capacity - Enrolled);

    public void SetDetails(DeliveryMode delivery, string? instructor, string? room, int capacity, int enrolled)
    {
        if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity cannot be negative.");
        if (enrolled < 0) throw new ArgumentOutOfRangeException(nameof(enrolled), "Enrolled cannot be negative.");
        Delivery = delivery;
        Instructor = string.IsNullOrWhiteSpace(instructor) ? null : instructor.Trim();
        Room = string.IsNullOrWhiteSpace(room) ? null : room.Trim();
        Capacity = capacity;
        Enrolled = enrolled;
    }

    /// <summary>Replaces the weekly meetings. Validate against the term with <see cref="MeetingRules"/> first.</summary>
    public void SetMeetings(IEnumerable<MeetingTime> meetings)
    {
        var list = meetings.OrderBy(m => m.Day == DayOfWeek.Sunday ? 7 : (int)m.Day).ThenBy(m => m.Start).ToList();
        if (list.Any(m => m.End <= m.Start)) throw new ArgumentException("Each meeting must end after it starts.", nameof(meetings));
        for (var i = 0; i < list.Count; i++)
            for (var j = i + 1; j < list.Count; j++)
                if (list[i].Overlaps(list[j])) throw new ArgumentException("A section's meetings cannot overlap each other.", nameof(meetings));
        _meetings.Clear();
        _meetings.AddRange(list);
    }

    public bool ClashesWith(Section other) => _meetings.Any(a => other._meetings.Any(a.Overlaps));
}
