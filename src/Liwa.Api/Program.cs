using Liwa.Api.Infrastructure;
using Liwa.Application;
using Liwa.Infrastructure;
using Liwa.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddLiwaApi(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseLiwaApi();

await app.Services.InitializeDatabaseAsync();
await app.RunAsync();

/// <summary>Exposed for WebApplicationFactory in integration tests.</summary>
public partial class Program;
