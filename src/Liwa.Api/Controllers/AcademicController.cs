using Liwa.Api.Infrastructure;
using Liwa.Application.Abstractions;
using Liwa.Application.Academic;
using Liwa.Application.Imports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Liwa.Api.Controllers;

/// <summary>The signed-in student's own program and progress.</summary>
[ApiController]
[Route("api/students/me")]
[Authorize(Policy = Policies.Student)]
public sealed class StudentController(IAcademicQueries queries, ICurrentUser user) : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType<StudentSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var summary = await queries.GetStudentSummaryAsync(user.UserId, ct);
        return summary is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Your student record hasn't been loaded yet.")
            : Ok(summary);
    }
}

/// <summary>The signed-in advisor's students.</summary>
[ApiController]
[Route("api/advisor")]
[Authorize(Policy = Policies.Advisor)]
public sealed class AdvisorController(IAcademicQueries queries, ICurrentUser user) : ControllerBase
{
    [HttpGet("students")]
    public Task<IReadOnlyList<AdviseeSummary>> Students(CancellationToken ct) => queries.GetAdviseesAsync(user.UserId, ct);
}

/// <summary>Data loading for administrators. Contract: docs/import-contract.md.</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = Policies.Admin)]
public sealed class AdminDataController(IAcademicQueries queries, IImportService imports, ICurrentUser user) : ControllerBase
{
    public const long MaxUploadBytes = 10 * 1024 * 1024;

    [HttpGet("overview")]
    public Task<DataOverview> Overview(CancellationToken ct) => queries.GetOverviewAsync(ct);

    /// <summary>
    /// Imports one CSV file. Defaults to a dry run (check only, nothing saved): send dryRun=false to save.
    /// Kinds: terms, courses, programs, degree-plans, sections, students, transcripts.
    /// </summary>
    [HttpPost("imports/{kind}")]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    [ProducesResponseType<ImportReport>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Import(string kind, IFormFile? file, [FromQuery] bool dryRun = true, CancellationToken ct = default)
    {
        if (!Enum.TryParse<ImportKind>(kind.Replace("-", string.Empty, StringComparison.Ordinal), ignoreCase: true, out var importKind)
            || int.TryParse(kind, out _))
            return Problem(statusCode: StatusCodes.Status404NotFound, title: $"'{kind}' isn't a kind of import. Use one of: terms, courses, programs, degree-plans, sections, students, transcripts.");
        if (file is null || file.Length == 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Choose a CSV file to upload.");

        await using var stream = file.OpenReadStream();
        return Ok(await imports.ImportAsync(importKind, stream, dryRun, user.UserId, ct));
    }
}
