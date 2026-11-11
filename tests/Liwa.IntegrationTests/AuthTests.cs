using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Liwa.Infrastructure.Persistence;
using Liwa.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Liwa.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AuthTests(LiwaApiFactory factory)
{
    [Theory]
    [InlineData("student.new", "Student")]
    [InlineData("advisor.it", "Advisor")]
    [InlineData("admin", "Admin")]
    public async Task Demo_accounts_sign_in_with_their_role(string username, string role)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { username, password = LiwaApiFactory.DemoPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LiwaApiFactory.SignInBody>();
        Assert.Equal(role, body!.User.Role);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
    }

    [Theory]
    [InlineData("student.new", "wrong-password")]
    [InlineData("no.such.user", "Liwa#2027")]
    public async Task Bad_credentials_get_the_same_401_problem(string username, string password)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { username, password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Username or password is incorrect.", problem.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Missing_fields_get_a_400_validation_problem()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Me_returns_profile_for_signed_in_user()
    {
        var client = await factory.SignedInClientAsync("student.senior");
        var me = await client.GetFromJsonAsync<LiwaApiFactory.UserBody>("/api/auth/me");
        Assert.Equal("student.senior", me!.Username);
        Assert.Equal("Omar Al Shamsi", me.DisplayName);
    }

    [Fact]
    public async Task Me_without_token_is_401()
    {
        var response = await factory.CreateClient().GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Tampered_token_is_401()
    {
        var client = await factory.SignedInClientAsync("student.new");
        var token = client.DefaultRequestHeaders.Authorization!.Parameter!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token[..^4] + "AAAA");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Theory]
    [InlineData("student.new", HttpStatusCode.Forbidden)]
    [InlineData("advisor.it", HttpStatusCode.Forbidden)]
    [InlineData("admin", HttpStatusCode.OK)]
    public async Task System_status_is_admin_only(string username, HttpStatusCode expected)
    {
        var client = await factory.SignedInClientAsync(username);
        Assert.Equal(expected, (await client.GetAsync("/api/admin/system")).StatusCode);
    }

    [Fact]
    public async Task Health_endpoints_are_public_and_report_database()
    {
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);

        using var ready = JsonDocument.Parse(await client.GetStringAsync("/api/health/ready"));
        Assert.Equal("Healthy", ready.RootElement.GetProperty("checks").GetProperty("database").GetString());
    }

    [Fact]
    public async Task Demo_seed_is_idempotent()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiwaDbContext>();
        var usersBefore = await db.Users.CountAsync();
        var coursesBefore = await db.Courses.CountAsync();

        await scope.ServiceProvider.GetRequiredService<DemoSeeder>().SeedAsync(default);

        Assert.Equal(usersBefore, await db.Users.CountAsync());
        Assert.Equal(coursesBefore, await db.Courses.CountAsync());
        var usernames = await db.Users.Select(u => u.Username).ToListAsync();
        Assert.All(DemoSeeder.Accounts, a => Assert.Contains(a.Username, usernames));
    }

    [Fact]
    public async Task Passwords_are_stored_hashed()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LiwaDbContext>();
        var demoNames = DemoSeeder.Accounts.Select(a => a.Username).ToList();
        var hashes = await db.Users.Where(u => demoNames.Contains(u.Username)).Select(u => u.PasswordHash).ToListAsync();
        Assert.Equal(demoNames.Count, hashes.Count);
        Assert.All(hashes, h =>
        {
            Assert.StartsWith("PBKDF2-SHA256$", h, StringComparison.Ordinal);
            Assert.DoesNotContain(LiwaApiFactory.DemoPassword, h!, StringComparison.Ordinal);
        });

        // Students created by an import sign in with their university account: they never get a password.
        var imports = scope.ServiceProvider.GetRequiredService<Liwa.Application.Imports.IImportService>();
        var csv = "student_id,display_name,program_code,catalog_year,admit_term\n202799901,Imported Student,BIT,2025,2027SP\n";
        var report = await imports.ImportAsync(Liwa.Application.Imports.ImportKind.Students, new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv)), dryRun: false, null, default);
        Assert.True(report.Saved);
        Assert.True(await db.Users.AnyAsync(u => !demoNames.Contains(u.Username)));
        Assert.All(await db.Users.AsNoTracking().Where(u => !demoNames.Contains(u.Username)).ToListAsync(), u => Assert.Null(u.PasswordHash));
    }
}
