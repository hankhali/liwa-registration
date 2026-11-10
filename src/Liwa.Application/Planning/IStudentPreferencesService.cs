using Liwa.Domain.Planning;

namespace Liwa.Application.Planning;

/// <param name="Preset">Null when the student has never chosen (the app then shows the preset cards).</param>
public sealed record PreferencesView(string? Preset, IReadOnlyList<TimeWindow> Availability, DeliveryPreference Delivery, int? MaxCredits,
    IReadOnlyList<Guid> DraftSectionIds);

public sealed record PreferencesRequest(string Preset, IReadOnlyList<TimeWindow>? Availability, DeliveryPreference? Delivery, int? MaxCredits);

/// <param name="WalkthroughDismissed">The first-login walkthrough was dismissed (students only).</param>
public sealed record UserSettingsView(string PreferredLanguage, bool WalkthroughDismissed);

public interface IStudentPreferencesService
{
    Task<PreferencesView> GetAsync(Guid userId, CancellationToken ct);

    /// <summary>Validates and saves the free time (windows must end after they start, no Sunday, no Friday afternoon).</summary>
    Task<(bool Ok, string? Problem)> SaveAsync(Guid userId, PreferencesRequest request, CancellationToken ct);

    /// <summary>Autosaves the planner's unsaved selection (empty list clears it).</summary>
    Task SaveDraftAsync(Guid userId, IReadOnlyList<Guid> sectionIds, CancellationToken ct);
}

public interface IUserSettingsService
{
    Task<UserSettingsView> GetAsync(Guid userId, CancellationToken ct);

    Task<bool> SetLanguageAsync(Guid userId, string language, CancellationToken ct);

    Task DismissWalkthroughAsync(Guid userId, CancellationToken ct);
}
