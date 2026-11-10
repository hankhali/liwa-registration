using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Liwa.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class PreferencesTests(LiwaApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static async Task<T> Read<T>(HttpResponseMessage r) => (await r.Content.ReadFromJsonAsync<T>(Json))!;

    [Fact]
    public async Task Free_time_and_the_draft_are_saved_per_student()
    {
        var student = await factory.SignedInClientAsync("student.senior");
        Assert.Null((await Read<PrefsBody>(await student.GetAsync("/api/students/me/preferences"))).Preset);

        var save = await student.PutAsJsonAsync("/api/students/me/preferences", new
        {
            preset = "evenings",
            availability = new[] { new { day = "Monday", start = "17:00", end = "20:00" } },
            delivery = "OnCampusOnly",
            maxCredits = 12,
        });
        Assert.Equal(HttpStatusCode.NoContent, save.StatusCode);
        var draftId = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await student.PutAsJsonAsync("/api/students/me/draft", new { sectionIds = new[] { draftId } })).StatusCode);

        var prefs = await Read<PrefsBody>(await student.GetAsync("/api/students/me/preferences"));
        Assert.Equal("evenings", prefs.Preset);
        Assert.Equal("Monday", Assert.Single(prefs.Availability).Day);
        Assert.Equal("OnCampusOnly", prefs.Delivery);
        Assert.Equal(12, prefs.MaxCredits);
        Assert.Equal([draftId], prefs.DraftSectionIds);
    }

    [Theory]
    [InlineData("Friday", "13:00", "15:00")] // Friday afternoon (Jumu'ah)
    [InlineData("Sunday", "09:00", "10:00")] // weekend
    [InlineData("Monday", "10:00", "09:00")] // ends before it starts
    public async Task Impossible_free_time_is_refused(string day, string start, string end)
    {
        var student = await factory.SignedInClientAsync("student.repeat");
        var r = await student.PutAsJsonAsync("/api/students/me/preferences", new { preset = "custom", availability = new[] { new { day, start, end } } });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Language_and_walkthrough_are_remembered_for_any_role()
    {
        var advisor = await factory.SignedInClientAsync("advisor.media");
        Assert.Equal(HttpStatusCode.NoContent, (await advisor.PutAsJsonAsync("/api/me/language", new { language = "ar" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await advisor.PutAsJsonAsync("/api/me/language", new { language = "fr" })).StatusCode);

        var student = await factory.SignedInClientAsync("student.probation");
        Assert.False((await Read<SettingsBody>(await student.GetAsync("/api/me/settings"))).WalkthroughDismissed);
        Assert.Equal(HttpStatusCode.NoContent, (await student.PostAsync("/api/me/walkthrough/dismiss", null)).StatusCode);

        Assert.True((await Read<SettingsBody>(await student.GetAsync("/api/me/settings"))).WalkthroughDismissed);
        Assert.Equal("ar", (await Read<SettingsBody>(await advisor.GetAsync("/api/me/settings"))).PreferredLanguage);
    }

    public sealed record WindowBody(string Day, string Start, string End);

    public sealed record PrefsBody(string? Preset, List<WindowBody> Availability, string Delivery, int? MaxCredits, List<Guid> DraftSectionIds);

    public sealed record SettingsBody(string PreferredLanguage, bool WalkthroughDismissed);
}
