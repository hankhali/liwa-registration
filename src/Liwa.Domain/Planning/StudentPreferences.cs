namespace Liwa.Domain.Planning;

/// <summary>
/// What a student told us about how they want to study (brief §7 AvailabilityWindow + StudentPreferences):
/// the free-time preset or custom windows, delivery and an optional credit cap, plus the autosaved draft
/// selection of the planner. One row per student; it is not a plan version and is never sent to anyone.
/// </summary>
public sealed class StudentPreferences
{
    private StudentPreferences() { } // EF Core

    public StudentPreferences(Guid userId, DateTimeOffset at)
    {
        UserId = userId;
        UpdatedAt = at;
    }

    public Guid UserId { get; private set; }

    /// <summary>"daytime", "evenings", "weekends", "custom" or "any" (skipped: see every class). Null = never chosen.</summary>
    public string? Preset { get; private set; }

    /// <summary>JSON list of free-time windows ({day,start,end}); empty for "any".</summary>
    public string? Availability { get; private set; }

    public DeliveryPreference Delivery { get; private set; } = DeliveryPreference.Any;

    public int? MaxCredits { get; private set; }

    /// <summary>JSON list of section ids the student picked but hasn't saved or sent (autosave).</summary>
    public string? DraftSectionIds { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static readonly IReadOnlySet<string> Presets = new HashSet<string>(StringComparer.Ordinal) { "any", "daytime", "evenings", "weekends", "custom" };

    public void SetAvailability(string preset, string? availabilityJson, DeliveryPreference delivery, int? maxCredits, DateTimeOffset at)
    {
        if (!Presets.Contains(preset)) throw new ArgumentException($"Unknown preset '{preset}'.", nameof(preset));
        if (maxCredits is < 0 or > 30) throw new ArgumentOutOfRangeException(nameof(maxCredits));
        Preset = preset;
        Availability = availabilityJson;
        Delivery = delivery;
        MaxCredits = maxCredits;
        UpdatedAt = at;
    }

    public void SetDraft(string? sectionIdsJson, DateTimeOffset at)
    {
        DraftSectionIds = sectionIdsJson;
        UpdatedAt = at;
    }
}
