using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Liwa.IntegrationTests;

/// <summary>Own database: admins change catalog data, policy, users and settings.</summary>
public sealed class AdminApiFactory : LiwaApiFactory;

public sealed class AdminTests(AdminApiFactory factory) : IClassFixture<AdminApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static readonly string[] Kinds = ["terms", "courses", "programs", "degree-plans", "sections", "students", "transcripts"];

    private static async Task<T> Read<T>(HttpResponseMessage r) => (await r.Content.ReadFromJsonAsync<T>(Json))!;

    private static MultipartFormDataContent File(string csv, string name = "data.csv")
    {
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        content.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        return new MultipartFormDataContent { { content, "file", name } };
    }

    [Fact]
    public async Task Every_export_imports_again_with_nothing_changed()
    {
        var admin = await factory.SignedInClientAsync("admin");
        foreach (var kind in Kinds)
        {
            var csv = await admin.GetStringAsync($"/api/admin/exports/{kind}");
            var report = await Read<ReportBody>(await admin.PostAsync($"/api/admin/imports/{kind}?dryRun=true", File(csv)));
            Assert.True(report.ErrorCount == 0, $"{kind}: {string.Join(" | ", report.Issues.Select(i => $"{i.Line}: {i.Message}"))}");
            Assert.True(report.Created == 0 && report.Updated == 0 && report.Removed == 0, $"{kind}: {report.Created} created, {report.Updated} updated, {report.Removed} removed");
            Assert.True(report.Unchanged > 0, kind);
        }

        var zip = await admin.GetByteArrayAsync("/api/admin/exports");
        using var archive = new ZipArchive(new MemoryStream(zip));
        Assert.Equal(["1-terms.csv", "2-courses.csv", "3-programs.csv", "4-degree-plans.csv", "5-sections.csv", "6-students.csv", "7-transcripts.csv"],
            archive.Entries.Select(e => e.FullName));
    }

    [Fact]
    public async Task A_file_with_errors_is_rejected_whole_with_422_and_line_numbers()
    {
        var admin = await factory.SignedInClientAsync("admin");
        var csv = "course_code,title,credits\nZZZ101,Fine course,3\nZZZ102,Bad credits,99\n";

        var r = await admin.PostAsync("/api/admin/imports/courses?dryRun=false", File(csv));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode);
        var report = await Read<ReportBody>(r);
        Assert.False(report.Saved);
        Assert.Contains(report.Issues, i => i.Line == 3 && i.Column == "credits");
        var courses = await Read<RowsBody>(await admin.GetAsync("/api/admin/catalog/courses"));
        Assert.DoesNotContain(courses.Rows, x => x["course_code"] == "ZZZ101"); // nothing saved
    }

    [Fact]
    public async Task Editing_a_row_goes_through_the_importer_and_creates_a_catalog_version()
    {
        var admin = await factory.SignedInClientAsync("admin");
        var row = (await Read<RowsBody>(await admin.GetAsync("/api/admin/catalog/courses"))).Rows.First(x => x["course_code"] == "MCM221");
        row["title"] = "Advanced Media Writing";

        var preview = await Read<ReportBody>(await admin.PostAsJsonAsync("/api/admin/catalog/courses/rows", new { rows = new[] { row }, dryRun = true }));
        Assert.Equal(1, preview.Updated);
        Assert.False(preview.Saved);

        var bad = row.ToDictionary(); bad["prerequisites"] = "MCM221";
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PostAsJsonAsync("/api/admin/catalog/courses/rows", new { rows = new[] { bad }, dryRun = false })).StatusCode);

        var saved = await Read<ReportBody>(await admin.PostAsJsonAsync("/api/admin/catalog/courses/rows", new { rows = new[] { row }, dryRun = false }));
        Assert.True(saved.Saved);
        Assert.NotNull(saved.CatalogVersion);

        // Advisors see the version history too; students don't.
        var advisor = await factory.SignedInClientAsync("advisor.media");
        var versions = await Read<List<VersionBody>>(await advisor.GetAsync("/api/catalog/versions"));
        var v = versions.First();
        Assert.Equal(saved.CatalogVersion, v.Number);
        Assert.Equal("Edit", v.Source);
        Assert.Equal("courses", v.Kind);
        Assert.Equal("Registrar Admin", v.ActorName);
        Assert.Contains(versions, x => x.Source == "Seed");
        var student = await factory.SignedInClientAsync("student.media");
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/catalog/versions")).StatusCode);
    }

    [Fact]
    public async Task The_prerequisite_editor_checks_rules_live()
    {
        var admin = await factory.SignedInClientAsync("admin");
        async Task<CheckBody> Check(string course, string expr) =>
            await Read<CheckBody>(await admin.PostAsJsonAsync("/api/admin/catalog/prerequisites/check", new { courseCode = course, expression = expr }));

        var ok = await Check("CIT242", "cit122 and (cit123 or mth110:c)");
        Assert.True(ok.Ok);
        Assert.Equal("CIT122 AND (CIT123 OR MTH110:C)", ok.Canonical);
        Assert.Contains("isn't in the catalog", (await Check("CIT242", "CIT999")).Problems.Single(), StringComparison.Ordinal);
        Assert.Contains("circle", (await Check("CIT112", "CIT354")).Problems.Single(), StringComparison.Ordinal); // CIT354 needs CIT123 needs CIT112
        Assert.False((await Check("CIT242", "CIT122 AND (")).Ok);
    }

    [Fact]
    public async Task A_degree_plan_with_students_cannot_be_removed()
    {
        var admin = await factory.SignedInClientAsync("admin");
        var r = await admin.DeleteAsync("/api/admin/catalog/degree-plans/BIT/2025");
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync("/api/admin/catalog/degree-plans/BIT/2031")).StatusCode);
    }

    [Fact]
    public async Task The_credit_rule_test_lists_who_would_change()
    {
        var admin = await factory.SignedInClientAsync("admin");
        var policy = await Read<PolicyBody>(await admin.GetAsync("/api/admin/credit-policy"));
        var probation = policy.Rules.Single(r => r.OnProbation == true && r.Season is null);
        var changed = policy with { Rules = policy.Rules.Select(r => r == probation ? r with { MaxCredits = 9 } : r).ToList() };

        var preview = await Read<PreviewBody>(await admin.PostAsJsonAsync("/api/admin/credit-policy/preview", changed, Json));
        var who = Assert.Single(preview.Changes);
        Assert.Equal("202500102", who.StudentId);
        Assert.Equal(12, who.BeforeMax);
        Assert.Equal(9, who.AfterMax);

        var unreachable = policy with { Rules = [new RuleBody("Everyone", null, null, null, null, null, 12, 18), .. policy.Rules] };
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await admin.PutAsJsonAsync("/api/admin/credit-policy", unreachable, Json)).StatusCode);
    }

    [Fact]
    public async Task Staff_accounts_can_be_created_and_students_reassigned_with_notifications()
    {
        var admin = await factory.SignedInClientAsync("admin");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/users",
            new { username = "advisor.new", displayName = "Dr. New", role = "Advisor", password = "short" })).StatusCode);
        var created = await admin.PostAsJsonAsync("/api/admin/users", new { username = "advisor.new", displayName = "Dr. New Advisor", role = "Advisor", password = "a-long-password-1" });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var newAdvisor = await Read<StaffBody>(created);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/admin/users",
            new { username = "advisor.new", displayName = "Again", role = "Advisor", password = "a-long-password-1" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/users",
            new { username = "student.x", displayName = "Not staff", role = "Student", password = "a-long-password-1" })).StatusCode);

        // The new account signs in.
        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = "advisor.new", password = "a-long-password-1" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var staff = await Read<List<StaffBody>>(await admin.GetAsync("/api/admin/users"));
        var media = staff.Single(s => s.Username == "advisor.media");
        Assert.Equal(3, media.Advisees);
        var me = staff.Single(s => s.Username == "admin");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/admin/users/{me.Id}/active", new { active = false })).StatusCode);

        var moved = await Read<ReassignBody>(await admin.PostAsJsonAsync("/api/admin/users/reassign",
            new { fromAdvisorUserId = media.Id, toAdvisorUserId = newAdvisor.Id, studentIds = new[] { "202500113" } }));
        Assert.Equal(1, moved.Moved);
        var student = await factory.SignedInClientAsync("student.hold");
        Assert.Contains((await Read<AdvisorFlowTests.NotificationsBody>(await student.GetAsync("/api/notifications"))).Items,
            n => n.MessageKey == "notifications.advisorChanged" && n.Parameters!.Contains("Dr. New Advisor", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Settings_keep_secrets_masked_and_never_lock_people_out()
    {
        var admin = await factory.SignedInClientAsync("admin");

        // Username/password sign-in can't be switched off without SSO on, a passing test and confirmation.
        var r = await admin.PutAsJsonAsync("/api/admin/settings/sso", new { enabled = false, localSignInEnabled = false, confirmSsoOnly = true });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        r = await admin.PutAsJsonAsync("/api/admin/settings/sso",
            new { enabled = true, tenantId = "lu.ac.ae", clientId = "11111111-2222-3333-4444-555555555555", clientSecret = "super-secret-value-WXYZ", localSignInEnabled = false, confirmSsoOnly = true });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); // never tested
        r = await admin.PutAsJsonAsync("/api/admin/settings/sso",
            new { enabled = true, tenantId = "lu.ac.ae", clientId = "11111111-2222-3333-4444-555555555555", clientSecret = "super-secret-value-WXYZ", localSignInEnabled = true });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var sso = await Read<SsoBody>(r);
        Assert.Equal("••••WXYZ", sso.ClientSecret);
        Assert.DoesNotContain("super-secret", await (await admin.GetAsync("/api/admin/settings/sso")).Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // AI provider: the key is masked; the mock provider is refused outside Development.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/admin/settings/ai", new { enabled = true, provider = "mock", model = "x" })).StatusCode);
        var ai = await Read<AiBody>(await admin.PutAsJsonAsync("/api/admin/settings/ai",
            new { enabled = true, provider = "anthropic", model = "claude-sonnet-5-5", apiKey = "sk-ant-test-1234" }));
        Assert.Equal("••••1234", ai.ApiKey);
        Assert.Equal("claude-sonnet-5-5", ai.Model);

        var student = await factory.SignedInClientAsync("student.media");
        Assert.Equal(HttpStatusCode.Forbidden, (await student.GetAsync("/api/admin/settings/ai")).StatusCode);
    }

    public sealed record IssueBody(string Severity, int Line, string? Column, string Message);

    public sealed record ReportBody(int RowsRead, int Created, int Updated, int Unchanged, int Removed, bool Saved, int? CatalogVersion, int ErrorCount, List<IssueBody> Issues);

    public sealed record RowsBody(string Kind, List<string> Columns, List<Dictionary<string, string?>> Rows);

    public sealed record VersionBody(int Number, string Source, string Kind, string? FileName, string? ActorName);

    public sealed record CheckBody(bool Ok, string? Canonical, List<string> Problems);

    public sealed record RuleBody(string Name, string? Season, string? Standing, decimal? MinGpa, decimal? MaxGpaExclusive, bool? OnProbation, int MinCredits, int MaxCredits);

    public sealed record PolicyBody(string RepeatGradeRule, bool AllowInProgressPrerequisites, bool GraduatingStudentsExemptFromMinimum, List<RuleBody> Rules);

    public sealed record ChangeBody(string StudentId, int? BeforeMax, int? AfterMax);

    public sealed record PreviewBody(int StudentsChecked, List<ChangeBody> Changes, List<string> Problems);

    public sealed record StaffBody(Guid Id, string Username, string Role, bool IsActive, int Advisees);

    public sealed record ReassignBody(int Moved);

    public sealed record SsoBody(bool Enabled, string? ClientSecret, bool LocalSignInEnabled);

    public sealed record AiBody(bool Enabled, string Provider, string Model, string? ApiKey);
}
