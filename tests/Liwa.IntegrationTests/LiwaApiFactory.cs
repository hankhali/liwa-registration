using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Liwa.IntegrationTests;

/// <summary>
/// Runs the real API against a throwaway PostgreSQL container (Docker required; see README).
/// One container per test class collection; migrations and demo seed run at startup as in production.
/// </summary>
public class LiwaApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DemoPassword = "Liwa#2027";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17-alpine").Build();

    protected virtual int SignInPermitsPerMinute => 1000;

    public async Task InitializeAsync() => await _db.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting applies before Program reads configuration, so eagerly-read values see it.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Liwa", _db.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-0123456789abcdef");
        builder.UseSetting("Security:PasswordHashing:Iterations", "10000");
        builder.UseSetting("DemoSeed:Enabled", "true");
        builder.UseSetting("DemoSeed:Password", DemoPassword);
        builder.UseSetting("RateLimiting:SignInPermitsPerMinute", SignInPermitsPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public async Task<HttpClient> SignedInClientAsync(string username)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password = DemoPassword });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<SignInBody>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    public sealed record SignInBody(string AccessToken, DateTimeOffset ExpiresAt, UserBody User);

    public sealed record UserBody(Guid Id, string Username, string DisplayName, string Role, string? UniversityId);
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<LiwaApiFactory>
{
    public const string Name = "api";
}
