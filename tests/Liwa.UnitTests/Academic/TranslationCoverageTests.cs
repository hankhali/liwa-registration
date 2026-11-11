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

    /// <summary>Translation sections a student reads (UX pass: words a 17-year-old uses).</summary>
    private static readonly string[] StudentSections =
        ["student", "home", "studentStatus", "plan", "planner.reasons", "planner.actions", "planner.studentReasons", "planner.studentActions", "units", "months", "myPlan", "graduation", "availability", "walkthrough", "timetable", "historyStudent", "messages"];

    private static IEnumerable<(string Key, string Value)> Strings(JsonElement e, string prefix) =>
        e.ValueKind == JsonValueKind.Object
            ? e.EnumerateObject().SelectMany(p => Strings(p.Value, prefix + "." + p.Name))
            : e.ValueKind == JsonValueKind.String ? [(prefix, e.GetString()!)] : [];

    private static IEnumerable<(string Key, string Value)> StudentStrings(string lang)
    {
        var root = Load(lang);
        foreach (var section in StudentSections)
        {
            var node = root;
            foreach (var part in section.Split('.')) node = node.GetProperty(part);
            foreach (var s in Strings(node, section)) yield return s;
        }
    }

    [Fact]
    public void Student_text_has_no_jargon()
    {
        // The UX pass's banned words; use "you can take", "you need to pass X first", "class", "semester", "check" instead.
        var banned = new System.Text.RegularExpressions.Regex(
            @"\b(eligib\w*|prerequisites?|corequisites?|critical path|sections?|terms?|validat\w*|projections?)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var offenders = StudentStrings("en")
            .Select(s => (s.Key, Text: System.Text.RegularExpressions.Regex.Replace(s.Value, @"\{\{[^}]*\}\}", "")))
            .Where(s => banned.IsMatch(s.Text))
            .Select(s => $"{s.Key}: \"{s.Text}\"")
            .ToList();
        Assert.True(offenders.Count == 0, "Jargon in student text:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void Student_text_is_fully_arabic_in_arabic()
    {
        var notArabic = StudentStrings("ar")
            .Where(s => System.Text.RegularExpressions.Regex.Replace(s.Value, @"\{\{[^}]*\}\}", "").Any(char.IsLetter))
            .Where(s => !s.Value.Any(ch => ch is >= '؀' and <= 'ۿ'))
            .Select(s => $"{s.Key}: \"{s.Value}\"")
            .ToList();
        Assert.True(notArabic.Count == 0, "Not translated:\n" + string.Join("\n", notArabic));
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
