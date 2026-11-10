using System.Text.Json;
using Liwa.Infrastructure.Academic;

namespace Liwa.UnitTests.Academic;

/// <summary>Email stubs are written in the recipient's language and match the in-app messages.</summary>
public sealed class EmailTemplateTests
{
    private static Dictionary<string, JsonElement> Values(object o) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(o))!;

    [Fact]
    public void Templates_render_in_english_and_arabic()
    {
        var values = Values(new { student = "Aisha", advisor = "Dr. Ahmed", term = "Spring 2027" });

        Assert.Equal("Dr. Ahmed approved your plan.", EmailTemplates.Render("notifications.planApproved", "en", values));
        Assert.Equal("اعتمد Dr. Ahmed خطتك.", EmailTemplates.Render("notifications.planApproved", "ar", values));
    }

    [Fact]
    public void Every_email_template_has_an_in_app_message_in_both_languages()
    {
        foreach (var lang in new[] { "en", "ar" })
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "web", "public", "i18n", $"{lang}.json")));
            foreach (var key in EmailTemplates.Keys)
            {
                var node = doc.RootElement;
                foreach (var part in key.Split('.'))
                    Assert.True(node.TryGetProperty(part, out node), $"{lang}.json has no '{key}'");
                Assert.Equal(JsonValueKind.String, node.ValueKind);
            }
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Liwa.slnx"))) dir = dir.Parent;
        return dir!.FullName;
    }
}
