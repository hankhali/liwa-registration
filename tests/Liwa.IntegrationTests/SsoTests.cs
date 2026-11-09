using System.Net;
using System.Net.Http.Json;
using Liwa.Application.Abstractions;
using Liwa.Application.Auth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Liwa.IntegrationTests;

/// <summary>SSO switched off (the default until university IT registers the app).</summary>
[Collection(ApiCollection.Name)]
public sealed class SsoDisabledTests(LiwaApiFactory factory)
{
    [Fact]
    public async Task Sign_in_page_is_told_sso_is_off_and_local_sign_in_is_on()
    {
        var config = await factory.CreateClient().GetFromJsonAsync<SsoConfigBody>("/api/auth/sso/config");

        Assert.False(config!.Enabled);
        Assert.True(config.LocalSignInEnabled);
    }

    [Fact]
    public async Task Starting_sso_while_it_is_off_finds_nothing()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/auth/sso/start")).StatusCode);
    }

    [Fact]
    public async Task A_made_up_handoff_code_is_rejected()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/sso/exchange", new { code = "made-up" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

/// <summary>
/// SSO switched on with placeholder IDs. Microsoft's discovery document is supplied in-process, so these
/// tests never call Microsoft. Local sign-in is switched off to prove that setting works.
/// </summary>
public sealed class SsoEnabledApiFactory : LiwaApiFactory
{
    public const string Tenant = "11111111-2222-3333-4444-555555555555";
    public const string Client = "66666666-7777-8888-9999-000000000000";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Sso:Enabled", "true");
        builder.UseSetting("Sso:TenantId", Tenant);
        builder.UseSetting("Sso:ClientId", Client);
        builder.UseSetting("Sso:ClientSecret", "test-secret");
        builder.UseSetting("Sso:LocalSignInEnabled", "false");
        builder.ConfigureTestServices(services =>
            services.PostConfigure<OpenIdConnectOptions>("entra", o =>
            {
                // The library's own post-configure has already created a network-backed manager; replace it.
                o.Configuration = new OpenIdConnectConfiguration
                {
                    Issuer = $"https://login.microsoftonline.com/{Tenant}/v2.0",
                    AuthorizationEndpoint = $"https://login.microsoftonline.com/{Tenant}/oauth2/v2.0/authorize",
                    TokenEndpoint = $"https://login.microsoftonline.com/{Tenant}/oauth2/v2.0/token",
                };
                o.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(o.Configuration);
            }));
    }
}

public sealed class SsoEnabledTests(SsoEnabledApiFactory factory) : IClassFixture<SsoEnabledApiFactory>
{
    private HttpClient NoRedirectClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Sign_in_page_is_told_sso_is_on_and_local_sign_in_is_off()
    {
        var config = await factory.CreateClient().GetFromJsonAsync<SsoConfigBody>("/api/auth/sso/config");

        Assert.True(config!.Enabled);
        Assert.False(config.LocalSignInEnabled);
    }

    [Fact]
    public async Task Start_sends_the_browser_to_liwas_microsoft_sign_in_with_pkce()
    {
        var response = await NoRedirectClient().GetAsync("/api/auth/sso/start");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.StartsWith($"https://login.microsoftonline.com/{SsoEnabledApiFactory.Tenant}/oauth2/v2.0/authorize", location.ToString());
        var query = System.Web.HttpUtility.ParseQueryString(location.Query);
        Assert.Equal(SsoEnabledApiFactory.Client, query["client_id"]);
        Assert.Equal("code", query["response_type"]);
        // Query is the default for the code flow, so the handler may omit it; it must never be form_post.
        Assert.Contains(query["response_mode"], new[] { null, "query" });
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.False(string.IsNullOrEmpty(query["code_challenge"]));
        Assert.Equal("select_account", query["prompt"]);
        Assert.EndsWith("/api/auth/sso/callback", query["redirect_uri"]);
        Assert.Contains("openid", query["scope"]!.Split(' '));
        // Correlation and nonce cookies are set so the callback can be verified.
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.Contains(".AspNetCore.Correlation.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Arriving_at_complete_without_signing_in_goes_back_to_sign_in_with_a_message()
    {
        var response = await NoRedirectClient().GetAsync("/api/auth/sso/complete");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/login?sso=failed", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task A_handoff_code_gives_a_working_token_once()
    {
        var user = new UserProfile(Guid.NewGuid(), "student.new", "Mariam", "Student", null);
        string code;
        using (var scope = factory.Services.CreateScope())
            code = scope.ServiceProvider.GetRequiredService<ISignInHandoff>().Create(new SignInSuccess("issued-token", DateTimeOffset.UtcNow.AddHours(1), user));

        var client = factory.CreateClient();
        var first = await client.PostAsJsonAsync("/api/auth/sso/exchange", new { code });
        var second = await client.PostAsJsonAsync("/api/auth/sso/exchange", new { code });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("issued-token", (await first.Content.ReadFromJsonAsync<LiwaApiFactory.SignInBody>())!.AccessToken);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Username_and_password_sign_in_can_be_switched_off()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = "admin", password = LiwaApiFactory.DemoPassword });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public sealed record SsoConfigBody(bool Enabled, bool LocalSignInEnabled);

/// <summary>SSO switched on, but Microsoft's sign-in settings can't be loaded (wrong tenant ID, network down).</summary>
public sealed class SsoUnreachableApiFactory : LiwaApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Sso:Enabled", "true");
        builder.UseSetting("Sso:TenantId", SsoEnabledApiFactory.Tenant);
        builder.UseSetting("Sso:ClientId", SsoEnabledApiFactory.Client);
        builder.UseSetting("Sso:ClientSecret", "test-secret");
        builder.ConfigureTestServices(services =>
            services.PostConfigure<OpenIdConnectOptions>("entra", o => o.ConfigurationManager = new FailingConfigurationManager()));
    }

    private sealed class FailingConfigurationManager : IConfigurationManager<OpenIdConnectConfiguration>
    {
        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel) =>
            throw new InvalidOperationException("IDX20803: Unable to obtain configuration (simulated).");

        public void RequestRefresh() { }
    }
}

public sealed class SsoUnreachableTests(SsoUnreachableApiFactory factory) : IClassFixture<SsoUnreachableApiFactory>
{
    [Fact]
    public async Task People_get_a_plain_message_instead_of_an_error_page()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/auth/sso/start");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/login?sso=unavailable", response.Headers.Location!.ToString());
    }
}
