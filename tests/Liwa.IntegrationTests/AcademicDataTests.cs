using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Liwa.Domain.Academic;
using Liwa.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Liwa.IntegrationTests;

/// <summary>The demo data (loaded through the real import pipeline at startup) tells the stories the brief asks for.</summary>
[Collection(ApiCollection.Name)]
public sealed class DemoDataTests(LiwaApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private async Task<T> GetAsync<T>(string username, string url)
    {
        var client = await factory.SignedInClientAsync(username);
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    [Fact]
    public async Task Three_real_catalog_programs_each_add_up_to_123_credits()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiwaDbContext>();
        var credits = await db.Courses.ToDictionaryAsync(c => c.Id, c => c.Credits);
        var plans = await db.DegreePlans.Include(d => d.Groups).ThenInclude(g => g.Courses).ToListAsync();
        var programs = await db.Programs.ToDictionaryAsync(p => p.Id);

        Assert.Equal(["BBA-BIT", "BIT", "BMC"], programs.Values.Select(p => p.Code).Order());
        Assert.Equal(3, plans.Count);
        Assert.All(programs.Values, p => Assert.Equal(123, p.TotalCredits));
        Assert.All(plans, p => Assert.Equal(programs[p.ProgramId].TotalCredits, p.PlannedCredits(credits)));
        // The 2025-2026 catalog: every course in these three programs is 3 credits.
        Assert.All(credits.Values, c => Assert.Equal(3, c));
    }

    [Fact]
    public async Task Spring_2027_is_current_and_its_timetable_follows_uae_rules()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiwaDbContext>();
        var term = await db.Terms.SingleAsync(t => t.IsCurrent);
        var sections = await db.Sections.Where(s => s.TermId == term.Id).ToListAsync();

        Assert.Equal("2027SP", term.Code);
        Assert.True(sections.Count > 100);
        Assert.All(sections, s => Assert.Empty(MeetingRules.Validate(term, s.Delivery, s.Meetings.ToList())));
        Assert.Contains(sections, s => s.Delivery == DeliveryMode.Online);
        Assert.Contains(sections, s => s.Meetings.Any(m => m.Start >= new TimeOnly(17, 0))); // evening classes for working students
    }

    [Fact]
    public async Task The_credit_policy_comes_from_configuration()
    {
        using var scope = factory.Services.CreateScope();
        var policy = await scope.ServiceProvider.GetRequiredService<LiwaDbContext>().CreditPolicies.Include(p => p.Rules).SingleAsync();
        Assert.Equal(5, policy.Rules.Count);
        Assert.Equal(21, policy.LimitsFor(TermSeason.Spring, Standing.Sophomore, 3.50m, false)!.MaxCredits);
        Assert.Equal(12, policy.LimitsFor(TermSeason.Spring, Standing.Sophomore, 1.40m, true)!.MaxCredits);
    }

    [Theory]
    [InlineData("student.new", "BIT", 3, null, false)]           // new first-year with AP English credit only
    [InlineData("student.probation", "BBA-BIT", 42, 1.43, true)] // on probation
    [InlineData("student.honors", "BIT", 30, 3.50, false)]        // exactly at the 3.50 boundary
    [InlineData("student.senior", "BIT", 117, 3.38, false)]       // 6 credits left to graduate
    [InlineData("student.repeat", "BMC", 45, 3.06, false)]        // failed MCM123
    [InlineData("student.hanieh", "BBA-BIT", 18, 4.00, false)]   // the real record (anonymised)
    public async Task Each_demo_student_tells_their_story(string username, string program, int earned, double? gpa, bool probation)
    {
        var s = await GetAsync<StudentSummaryBody>(username, "/api/students/me/summary");
        Assert.Equal(program, s.ProgramCode);
        Assert.Equal(earned, s.CreditsEarned);
        Assert.Equal(123, s.CreditsRequired);
        Assert.Equal(gpa is null ? null : (decimal)gpa, s.Gpa);
        Assert.Equal(probation, s.OnProbation);
        Assert.Equal("2027SP", s.CurrentTerm!.Code);
        Assert.False(string.IsNullOrEmpty(s.AdvisorName));
    }

    [Fact]
    public async Task The_high_gpa_student_has_courses_in_progress()
    {
        var s = await GetAsync<StudentSummaryBody>("student.honors", "/api/students/me/summary");
        Assert.Equal(18, s.InProgressCredits);
    }

    [Fact]
    public async Task The_repeat_student_failed_a_prerequisite()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiwaDbContext>();
        var mcm123 = await db.Courses.SingleAsync(c => c.Code == "MCM123");
        var record = await db.StudentRecords.Include(r => r.Transcript).SingleAsync(r => r.StudentId == "202500105");
        Assert.Contains(record.Transcript, t => t.CourseId == mcm123.Id && t.Grade == "F");
        Assert.Equal("MCM123", (await db.Courses.SingleAsync(c => c.Code == "MCM202")).Prerequisites);
    }

    [Fact]
    public async Task An_advisor_sees_only_their_own_students()
    {
        var students = await GetAsync<List<AdviseeBody>>("advisor.it", "/api/advisor/students");
        Assert.Equal(7, students.Count);
        Assert.All(students, s => Assert.Equal("BIT", s.ProgramCode));
        Assert.Contains(students, s => s.StudentId == "202500103");
        Assert.DoesNotContain(students, s => s.StudentId == "202500102");
    }

    [Fact]
    public async Task The_admin_overview_counts_the_data()
    {
        var o = await GetAsync<OverviewBody>("admin", "/api/admin/overview");
        Assert.Equal("Spring 2026-2027", o.CurrentTerm!.Name); // Liwa names terms by academic year
        Assert.Equal(3, o.Programs);
        Assert.True(o.Courses >= 110);
        Assert.True(o.SectionsThisTerm > 100);
        Assert.True(o.Students >= 16); // 16 demo students (other tests in this collection may import more)
        Assert.NotNull(o.LastImportAt);
    }

    [Theory]
    [InlineData("student.new", "/api/advisor/students")]
    [InlineData("advisor.it", "/api/students/me/summary")]
    [InlineData("advisor.it", "/api/admin/overview")]
    [InlineData("student.new", "/api/admin/overview")]
    public async Task Each_view_is_only_for_its_role(string username, string url)
    {
        var client = await factory.SignedInClientAsync(username);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url)).StatusCode);
    }

    public sealed record TermBody(string Code, string Name);

    public sealed record StudentSummaryBody(string StudentId, string ProgramCode, string ProgramName, int CreditsEarned, int CreditsRequired,
        int InProgressCredits, string Standing, decimal? Gpa, bool OnProbation, string? AdvisorName, TermBody? CurrentTerm);

    public sealed record AdviseeBody(string StudentId, string DisplayName, string ProgramCode, int CreditsEarned, decimal? Gpa, bool OnProbation);

    public sealed record OverviewBody(TermBody? CurrentTerm, int Terms, int Courses, int Programs, int DegreePlans, int SectionsThisTerm, int Students, int Advisors, DateTimeOffset? LastImportAt);
}

/// <summary>Importing CSV files through the admin endpoint. Own database, because these tests change data.</summary>
public sealed class ImportApiFactory : LiwaApiFactory;

public sealed class ImportTests(ImportApiFactory factory) : IClassFixture<ImportApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private async Task<(HttpStatusCode Status, ReportBody? Report)> ImportAsync(string kind, string csv, bool dryRun, string username = "admin")
    {
        var client = await factory.SignedInClientAsync(username);
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", $"{kind}.csv");
        var response = await client.PostAsync($"/api/admin/imports/{kind}?dryRun={dryRun.ToString().ToLowerInvariant()}", content);
        // Files with errors come back as 422 with the full report (brief §11).
        var hasReport = response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.UnprocessableEntity;
        return (response.StatusCode, hasReport ? await response.Content.ReadFromJsonAsync<ReportBody>(Json) : null);
    }

    private async Task<int> CountAsync(Func<LiwaDbContext, Task<int>> query)
    {
        using var scope = factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<LiwaDbContext>());
    }

    [Fact]
    public async Task A_dry_run_reports_the_changes_but_saves_nothing()
    {
        const string csv = "course_code,title,credits,offered\nTST101,Test Course,3,Fall;Spring\n";

        var (status, report) = await ImportAsync("courses", csv, dryRun: true);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(1, report!.Created);
        Assert.False(report.Saved);
        Assert.Equal(0, await CountAsync(db => db.Courses.CountAsync(c => c.Code == "TST101")));
    }

    [Fact]
    public async Task A_real_import_saves_and_importing_again_changes_nothing()
    {
        const string csv = "course_code,title,title_ar,credits,prerequisites\nTST201,Testing Practice,ممارسة الاختبار,3,cit122 and standing(junior)\n";

        var (_, first) = await ImportAsync("courses", csv, dryRun: false);
        var (_, second) = await ImportAsync("courses", csv, dryRun: false);

        Assert.True(first!.Saved);
        Assert.Equal(1, first.Created);
        Assert.Equal(1, second!.Unchanged);
        Assert.Equal(0, second.Created + second.Updated);
        using var scope = factory.Services.CreateScope();
        var course = await scope.ServiceProvider.GetRequiredService<LiwaDbContext>().Courses.SingleAsync(c => c.Code == "TST201");
        Assert.Equal("CIT122 AND STANDING(Junior)", course.Prerequisites); // stored in canonical form
        Assert.Equal("ممارسة الاختبار", course.TitleAr);
    }

    [Fact]
    public async Task One_bad_row_means_nothing_is_saved_and_every_problem_is_listed()
    {
        const string csv = "course_code,title,credits,prerequisites\nTST301,Good Course,3,\nnot-a-code,Bad Course,9,\nTST302,Bad Prereq,3,XYZ999 AND\n";

        var (status, report) = await ImportAsync("courses", csv, dryRun: false);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.False(report!.Saved);
        Assert.Contains(report.Issues, i => i.Line == 3 && i.Column == "course_code");
        Assert.Contains(report.Issues, i => i.Line == 3 && i.Column == "credits");
        Assert.Contains(report.Issues, i => i.Line == 4 && i.Column == "prerequisites");
        Assert.Equal(0, await CountAsync(db => db.Courses.CountAsync(c => c.Code == "TST301")));
    }

    [Fact]
    public async Task Sections_that_break_uae_calendar_rules_are_rejected()
    {
        const string csv = "term_code,course_code,section,capacity,days,start_time,end_time\n" +
                           "2027SP,CIT122,S9,30,Sun;Tue;Thu,09:00,10:15\n" +
                           "2027SP,CIT123,F9,30,Mon;Wed;Fri,11:00,12:15\n";

        var (_, report) = await ImportAsync("sections", csv, dryRun: true);

        Assert.Contains(report!.Issues, i => i.Line == 2 && i.Message.Contains("Sunday", StringComparison.Ordinal));
        Assert.Contains(report.Issues, i => i.Line == 3 && i.Message.Contains("Jumu'ah", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_missing_column_is_explained()
    {
        var (_, report) = await ImportAsync("students", "student_id,display_name\n202799999,Test Student\n", dryRun: true);
        Assert.Contains(report!.Issues, i => i.Line == 1 && i.Column == "program_code" && i.Severity == "Error");
    }

    [Fact]
    public async Task A_new_student_gets_an_account_without_a_password_and_a_record()
    {
        const string students = "student_id,display_name,email,program_code,catalog_year,admit_term,advisor_username\n202799990,Test Student,202799990@lu.ac.ae,BIT,2025,2027SP,advisor.it\n";
        const string transcript = "student_id,term_code,course_code,grade\n202799990,2026FA,ENG100,A\n202799990,2027SP,MTH105,\n";

        Assert.True((await ImportAsync("students", students, false)).Report!.Saved);
        var (_, history) = await ImportAsync("transcripts", transcript, false);

        Assert.True(history!.Saved);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiwaDbContext>();
        var user = await db.Users.SingleAsync(u => u.Username == "202799990");
        Assert.Null(user.PasswordHash);
        Assert.Equal("202799990@lu.ac.ae", user.Email);
        var record = await db.StudentRecords.Include(r => r.Transcript).SingleAsync(r => r.StudentId == "202799990");
        Assert.Equal(2, record.Transcript.Count);
        Assert.Contains(record.Transcript, t => t.Status == TranscriptStatus.InProgress);
    }

    [Fact]
    public async Task Imports_are_written_to_the_audit_log()
    {
        await ImportAsync("programs", "program_code,name,total_credits\nTEST,Test Program,120\n", dryRun: false);
        Assert.True(await CountAsync(db => db.AuditLog.CountAsync(a => a.Action == "import.programs" && a.ActorUserId != null)) >= 1);
    }

    [Fact]
    public async Task Only_admins_can_import()
    {
        var (status, _) = await ImportAsync("courses", "course_code,title,credits\nTST999,X,3\n", dryRun: true, username: "advisor.it");
        Assert.Equal(HttpStatusCode.Forbidden, status);
    }

    [Fact]
    public async Task An_unknown_kind_of_import_is_explained()
    {
        var (status, _) = await ImportAsync("timetables", "a\n1\n", dryRun: true);
        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    public sealed record IssueBody(string Severity, int Line, string? Column, string Message);

    public sealed record ReportBody(string Kind, bool DryRun, int RowsRead, int Created, int Updated, int Unchanged, bool Saved, List<IssueBody> Issues);
}
