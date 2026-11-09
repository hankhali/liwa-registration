using Liwa.Api.Infrastructure;
using Liwa.Application.Abstractions;
using Liwa.Application.Academic;
using Liwa.Application.Imports;
using Liwa.Application.Planning;
using Liwa.Domain.Planning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Liwa.Api.Controllers;

/// <summary>The signed-in student's own program and progress.</summary>
[ApiController]
[Route("api/students/me")]
[Authorize(Policy = Policies.Student)]
public sealed class StudentController(IAcademicQueries queries, IRecommendationService recommendations, IStudentPlanService plans, ICurrentUser user) : ControllerBase
{
    public sealed record CheckRequest(IReadOnlyList<Guid> SectionIds, IReadOnlyList<TimeWindow>? Availability);

    /// <summary>The planning screen: recommendation plus course titles, class times and the saved plan.</summary>
    [HttpPost("planning")]
    [ProducesResponseType<PlanningView>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Planning([FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] RecommendationRequest? request, CancellationToken ct)
    {
        if (Validate(request) is { } problem) return problem;
        var view = await plans.GetViewAsync(user.UserId, ToAvailability(request?.Availability),
            new PlannerPreferences(request?.MaxCredits, request?.Delivery ?? DeliveryPreference.Any), ct);
        return view is null ? NotLoaded() : Ok(view);
    }

    /// <summary>Live check of a hand-picked plan (clashes, credit limits, prerequisites, corequisites).</summary>
    [HttpPost("plan/check")]
    [ProducesResponseType<PlanCheckResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Check(CheckRequest request, CancellationToken ct)
    {
        if (request.SectionIds.Count > 15) return Problem(statusCode: StatusCodes.Status400BadRequest, title: "That's too many classes for one term.");
        var check = await plans.CheckAsync(user.UserId, request.SectionIds, ToAvailability(request.Availability), ct);
        return check is null ? NotLoaded() : Ok(check);
    }

    /// <summary>Saves the plan as a draft, or sends it to the advisor (Submit = true; warnings must be confirmed).</summary>
    [HttpPost("plan")]
    [ProducesResponseType<SavePlanResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Save(SavePlanRequest request, CancellationToken ct)
    {
        if (request.SectionIds.Count > 15) return Problem(statusCode: StatusCodes.Status400BadRequest, title: "That's too many classes for one term.");
        var result = await plans.SaveAsync(user.UserId, request, ct);
        return result.Outcome switch
        {
            SaveOutcome.Saved => Ok(result),
            SaveOutcome.Invalid => BadRequest(result),
            SaveOutcome.NotFound => NotLoaded(),
            _ => Conflict(result),
        };
    }

    private ObjectResult? Validate(RecommendationRequest? request)
    {
        if (request?.MaxCredits is < 0 or > 30)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Maximum credits must be between 0 and 30.");
        if (request?.Availability?.Any(w => w.End <= w.Start) == true)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Each free-time window must end after it starts.");
        return null;
    }

    private static Availability? ToAvailability(IReadOnlyList<TimeWindow>? windows) => windows is { Count: > 0 } ? new Availability(windows) : null;

    private ObjectResult NotLoaded() =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "We can't build a plan yet: your study record or this term's timetable hasn't been loaded.");

    /// <summary>Optional planning preferences. No availability means "show everything".</summary>
    public sealed record RecommendationRequest(IReadOnlyList<TimeWindow>? Availability, int? MaxCredits, DeliveryPreference? Delivery);

    /// <summary>
    /// The planner's recommendation for the current term (plan, reasons, rejected alternatives, every remaining
    /// course with its status, warnings, graduation projection with and without summer). See docs/planner.md.
    /// </summary>
    [HttpPost("recommendation")]
    [ProducesResponseType<PlannerResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Recommendation([FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] RecommendationRequest? request, CancellationToken ct)
    {
        if (request?.MaxCredits is < 0 or > 30)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Maximum credits must be between 0 and 30.");
        if (request?.Availability?.Any(w => w.End <= w.Start) == true)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Each free-time window must end after it starts.");

        var availability = request?.Availability is { Count: > 0 } windows ? new Availability(windows) : null;
        var preferences = new PlannerPreferences(request?.MaxCredits, request?.Delivery ?? DeliveryPreference.Any);
        var result = await recommendations.RecommendAsync(user.UserId, availability, preferences, ct);
        return result is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "We can't build a plan yet: your study record or this term's timetable hasn't been loaded.")
            : Ok(result);
    }

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
