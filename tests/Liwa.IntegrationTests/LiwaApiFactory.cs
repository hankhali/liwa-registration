using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace Liwa.IntegrationTests;

/// <summary>
/// Runs the real API against the locally installed PostgreSQL (no Docker).
/// Each fixture creates its own uniquely named database (liwa_test_xxxxxxxx), the API migrates
/// and seeds it at startup as in production, and the database is dropped when the fixture ends.
/// Server: env LIWA_TEST_PG (an Npgsql connection string to a role with CREATEDB),
/// default Host=localhost;Port=5432;Username=liwa;Password=liwa (see scripts/create-db.sql).
/// </summary>
public class LiwaApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DemoPassword = "Liwa#2027";

    private const string DefaultServer = "Host=localhost;Port=5432;Username=liwa;Password=liwa";

    private readonly string _serverConnection;
    private readonly string _databaseName = $"liwa_test_{Guid.NewGuid():N}"[..18];

    public LiwaApiFactory()
    {
        var configured = Environment.GetEnvironmentVariable("LIWA_TEST_PG");
        _serverConnection = string.IsNullOrWhiteSpace(configured) ? DefaultServer : configured;
    }

    protected virtual int SignInPermitsPerMinute => 1000;

    public string DatabaseConnectionString =>
        new NpgsqlConnectionStringBuilder(_serverConnection) { Database = _databaseName }.ConnectionString;

    private string MaintenanceConnectionString =>
        new NpgsqlConnectionStringBuilder(_serverConnection) { Database = "postgres", Pooling = false }.ConnectionString;

    public async Task InitializeAsync()
    {
        await using var conn = new NpgsqlConnection(MaintenanceConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\" ENCODING 'UTF8' TEMPLATE template0", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var conn = new NpgsqlConnection(MaintenanceConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting applies before Program reads configuration, so eagerly-read values see it.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Liwa", DatabaseConnectionString);
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
