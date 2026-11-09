namespace Liwa.Domain.Academic;

/// <summary>
/// UAE calendar rules for section meeting times (brief §4). Returns plain problems rather than throwing,
/// so the import report can list every issue in a file at once.
/// </summary>
public static class MeetingRules
{
    public static readonly TimeOnly FridayCutoff = new(12, 0);

    public static IReadOnlyList<string> Validate(Term term, DeliveryMode delivery, IReadOnlyCollection<MeetingTime> meetings)
    {
        var problems = new List<string>();

        if (meetings.Count == 0 && delivery != DeliveryMode.Online)
            problems.Add("On-campus and hybrid sections need at least one meeting time.");

        foreach (var m in meetings)
        {
            if (m.End <= m.Start)
                problems.Add($"{m.Day} meeting must end after it starts.");

            if (m.Day == DayOfWeek.Sunday)
                problems.Add("Sunday is a weekend day in the UAE; classes cannot meet on Sunday.");
            else if (!term.TeachingDays.Includes(m.Day))
                problems.Add($"{m.Day} is not a teaching day in {term.Name}.");

            if (m.Day == DayOfWeek.Friday && !term.AllowFridayAfternoon && m.End > FridayCutoff)
                problems.Add("Classes cannot run past 12:00 pm on Friday (Jumu'ah).");
        }

        var days = meetings.Select(m => m.Day).ToHashSet();
        if (days.SetEquals([DayOfWeek.Sunday, DayOfWeek.Tuesday, DayOfWeek.Thursday]))
            problems.Add("Sunday/Tuesday/Thursday is not a valid UAE pattern.");

        return problems.Distinct().ToList();
    }
}
