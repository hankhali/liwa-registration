using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Liwa.Domain.Planning;
using Liwa.Infrastructure.Seeding;

namespace Liwa.IntegrationTests;

/// <summary>
/// Brief Â§5: the planner gives identical output whether the data comes from the CSV files or from the database.
/// Every demo student's recommendation through the API (database) is compared with the planner run directly on the
/// demo CSV files. Section ids differ between the two sources, so sections are compared by course + section code.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CsvVersusDatabaseTests(LiwaApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static readonly JsonSerializerOptions ConfigJson = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public async Task Every_demo_student_gets_the_same_plan_from_csv_and_from_the_database()
    {
        var csv = CsvPlannerSource.FromDemoFiles();
        var policy = Policy();
        var students = DemoSeeder.Accounts.Where(a => a.Role == Domain.Users.UserRole.Student).ToList();
        Assert.Equal(16, students.Count);

        foreach (var a in students)
        {
            var client = await factory.SignedInClientAsync(a.Username);
            var fromDb = await (await client.PostAsync("/api/students/me/recommendation", null)).Content.ReadFromJsonAsync<PlannerResult>(Json);
            var input = csv.InputFor(a.UniversityId!, policy, DateOnly.FromDateTime(DateTime.Now));
            var fromCsv = Planner.Plan(input);

            Assert.Equal(Normalize(fromCsv, id => csv.SectionLabel(id)), Normalize(fromDb!, null));
        }
    }

    /// <summary>The result with section ids replaced by "course-section" and the credit rule's id left out.</summary>
    private static string Normalize(PlannerResult r, Func<Guid, string>? label)
    {
        var shape = new
        {
            r.TermCode,
            Limits = r.Limits is null ? null : new { r.Limits.MinCredits, r.Limits.MaxCredits },
            r.MaxCredits,
            r.TotalCredits,
            Plan = r.Plan.Select(p => new
            {
                p.CourseCode, p.Credits, p.SectionCode, p.PriorityRank, p.Reasons,
                Alternatives = p.Alternatives.Select(x => new { x.SectionCode, x.Reasons }),
            }),
            r.Courses,
            r.Warnings,
            r.Projection,
        };
        _ = label;
        return JsonSerializer.Serialize(shape, Json);
    }

    private static Domain.Academic.CreditPolicy Policy()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir!.FullName, "Liwa.slnx"))) dir = dir.Parent;
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir.FullName, "src", "Liwa.Api", "appsettings.json")));
        return CreditPolicySeeder.Build(doc.RootElement.GetProperty("CreditPolicy").Deserialize<CreditPolicyDefaults>(ConfigJson)!);
    }
}
