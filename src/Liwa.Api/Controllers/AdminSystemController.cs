using Liwa.Api.Infrastructure;
using Liwa.Infrastructure.Seeding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Liwa.Api.Controllers;

/// <summary>System status for administrators only. Students never see engineering information.</summary>
[ApiController]
[Route("api/admin/system")]
[Authorize(Policy = Policies.Admin)]
public sealed class AdminSystemController(HealthCheckService health, IOptions<DemoSeedOptions> demoSeed, IWebHostEnvironment env) : ControllerBase
{
    public sealed record SystemStatus(string Version, string Environment, string Database, bool DemoSeedEnabled);

    [HttpGet]
    public async Task<SystemStatus> Get(CancellationToken ct)
    {
        var report = await health.CheckHealthAsync(c => c.Tags.Contains("ready"), ct);
        return new SystemStatus(ApiSetup.Version, env.EnvironmentName, report.Status.ToString(), demoSeed.Value.Enabled);
    }
}
