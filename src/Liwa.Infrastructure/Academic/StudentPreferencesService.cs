using System.Text.Json;
using Liwa.Application.Planning;
using Liwa.Domain.Planning;
using Liwa.Domain.Users;
using Liwa.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liwa.Infrastructure.Academic;

internal sealed class StudentPreferencesService(LiwaDbContext db, TimeProvider clock) : IStudentPreferencesService, IUserSettingsService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Friday teaching ends at noon (Jumu'ah) unless a term overrides it (brief §3).</summary>
    private static readonly TimeOnly FridayCutoff = new(12, 0);

    public async Task<PreferencesView> GetAsync(Guid userId, CancellationToken ct)
    {
        var p = await db.StudentPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (p is null) return new PreferencesView(null, [], DeliveryPreference.Any, null, []);
        return new PreferencesView(
            p.Preset,
            p.Availability is null ? [] : JsonSerializer.Deserialize<List<TimeWindow>>(p.Availability, Json) ?? [],
            p.Delivery,
            p.MaxCredits,
            p.DraftSectionIds is null ? [] : JsonSerializer.Deserialize<List<Guid>>(p.DraftSectionIds, Json) ?? []);
    }

    public async Task<(bool Ok, string? Problem)> SaveAsync(Guid userId, PreferencesRequest request, CancellationToken ct)
    {
        if (!StudentPreferences.Presets.Contains(request.Preset ?? "")) return (false, "Choose one of the free-time options.");
        if (request.MaxCredits is < 0 or > 30) return (false, "Maximum credits must be between 0 and 30.");
        var windows = request.Availability ?? [];
        if (windows.Count > 50) return (false, "That's too many free-time windows.");
        foreach (var w in windows)
        {
            if (w.End <= w.Start) return (false, "Each free-time window must end after it starts.");
            if (w.Day == DayOfWeek.Sunday) return (false, "Sunday is a weekend day; there are no classes.");
            if (w.Day == DayOfWeek.Friday && w.End > FridayCutoff) return (false, "There are no classes on Friday afternoon.");
        }

        var p = await LoadOrCreateAsync(userId, ct);
        p.SetAvailability(request.Preset!, windows.Count == 0 ? null : JsonSerializer.Serialize(windows, Json),
            request.Delivery ?? DeliveryPreference.Any, request.MaxCredits, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task SaveDraftAsync(Guid userId, IReadOnlyList<Guid> sectionIds, CancellationToken ct)
    {
        var p = await LoadOrCreateAsync(userId, ct);
        p.SetDraft(sectionIds.Count == 0 ? null : JsonSerializer.Serialize(sectionIds.Distinct(), Json), clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
    }

    private async Task<StudentPreferences> LoadOrCreateAsync(Guid userId, CancellationToken ct)
    {
        var p = await db.StudentPreferences.SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (p is not null) return p;
        p = new StudentPreferences(userId, clock.GetUtcNow());
        db.StudentPreferences.Add(p);
        return p;
    }

    // ------------------------------------------------------------------ user settings

    async Task<UserSettingsView> IUserSettingsService.GetAsync(Guid userId, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().SingleAsync(x => x.Id == userId, ct);
        return new UserSettingsView(u.PreferredLanguage, u.WalkthroughDismissedAt is not null);
    }

    public async Task<bool> SetLanguageAsync(Guid userId, string language, CancellationToken ct)
    {
        if (!User.Languages.Contains((language ?? "").Trim().ToLowerInvariant())) return false;
        var u = await db.Users.SingleAsync(x => x.Id == userId, ct);
        u.SetPreferredLanguage(language!);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task DismissWalkthroughAsync(Guid userId, CancellationToken ct)
    {
        var u = await db.Users.SingleAsync(x => x.Id == userId, ct);
        u.DismissWalkthrough(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
    }
}
