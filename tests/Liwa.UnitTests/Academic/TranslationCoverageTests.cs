using System.Text.Json;
using Liwa.Domain.Planning;

namespace Liwa.UnitTests.Academic;

/// <summary>
/// Every planner reason/warning code must have a plain sentence in English and Arabic (brief §8/§9/§15 step 9:
/// tests fail if any code lacks a translation). Both language files must also have exactly the same keys.
/// </summary>
public sealed class TranslationCoverageTests
{
    private static JsonElement Load(string lang)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir!.FullName, "Liwa.slnx"))) dir = dir.Parent;
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(dir.FullName, "web", "public", "i18n", $"{lang}.json"))).RootElement.Clone();
    }

    private static IEnumerable<string> Keys(JsonElement e, string prefix = "") =>
        e.ValueKind == JsonValueKind.Object
            ? e.EnumerateObject().SelectMany(p => Keys(p.Value, prefix + p.Name + "."))
            : [prefix.TrimEnd('.')];

    [Theory]
    [InlineData("en")]
    [InlineData("ar")]
    public void Every_reason_code_has_a_sentence(string lang)
    {
        var reasons = Load(lang).GetProperty("planner").GetProperty("reasons");
        var missing = Enum.GetNames<ReasonCode>().Where(code =>
            !reasons.TryGetProperty(code, out var v) || v.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(v.GetString())).ToList();
        Assert.True(missing.Count == 0, $"{lang}.json has no planner.reasons for: {string.Join(", ", missing)}");
    }

    [Fact]
    public void English_and_arabic_have_the_same_keys()
    {
        var en = Keys(Load("en")).ToHashSet();
        var ar = Keys(Load("ar")).ToHashSet();
        Assert.Empty(en.Except(ar));
        Assert.Empty(ar.Except(en));
    }

    [Fact]
    public void Arabic_strings_are_actually_arabic()
    {
        var ar = Load("ar");
        var reasons = ar.GetProperty("planner").GetProperty("reasons");
        foreach (var p in reasons.EnumerateObject())
            Assert.True(p.Value.GetString()!.Any(ch => ch is >= '؀' and <= 'ۿ'), $"planner.reasons.{p.Name} is not translated");
    }
}
