using Liwa.Api.Infrastructure;
using Liwa.Application;
using Liwa.Infrastructure;
using Liwa.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Sign-in settings saved by an administrator (Settings page) apply from the next start (docs/sso.md).
builder.Configuration.AddInMemoryCollection(Liwa.Infrastructure.Admin.SettingsBootstrap.Load(builder.Configuration));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddLiwaApi(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseLiwaApi();

await app.Services.InitializeDatabaseAsync();

// One-off: create the first administrator on a fresh server (docs/INSTALL.md), then exit.
if (args.Length > 0 && args[0] == "create-admin")
    return await Liwa.Infrastructure.Seeding.AdminBootstrap.RunAsync(app.Services, args, Console.Out);

await app.Services.EnsureAiProviderAllowedAsync();
await app.RunAsync();
return 0;

/// <summary>Exposed for WebApplicationFactory in integration tests.</summary>
public partial class Program;
