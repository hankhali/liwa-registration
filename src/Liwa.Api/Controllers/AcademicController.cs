using Liwa.Api.Infrastructure;
using Liwa.Application.Abstractions;
using Liwa.Application.Academic;
using Liwa.Application.Admin;
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
public sealed class StudentController(IAcademicQueries queries, IRecommendationService recommendations, IStudentPlanService plans,
    IStudentPreferencesService preferences, ICurrentUser user) : ControllerBase
{
    public sealed record CheckRequest(IReadOnlyList<Guid> SectionIds, IReadOnlyList<TimeWindow>? Availability);

    public sealed record DraftRequest(IReadOnlyList<Guid> SectionIds);

    /// <summary>Graduation projection for the student's own selection (to compare with the recommendation).</summary>
    [HttpPost("projection")]
    [ProducesResponseType<GraduationProjection>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Projection(DraftRequest request, CancellationToken ct)
    {
        if (request.SectionIds.Count > 15) return Problem(statusCode: StatusCodes.Status400BadRequest, title: "That's too many classes for one term.");
        return await plans.ProjectAsync(user.UserId, request.SectionIds, ct) is { } p ? Ok(p) : NotLoaded();
    }

    /// <summary>The student's saved free time, delivery, credit cap and autosaved draft. Preset is null until chosen.</summary>
    [HttpGet("preferences")]
    public Task<PreferencesView> Preferences(CancellationToken ct) => preferences.GetAsync(user.UserId, ct);

    [HttpPut("preferences")]
    public async Task<IActionResult> SavePreferences(PreferencesRequest request, CancellationToken ct)
    {
        var (ok, problem) = await preferences.SaveAsync(user.UserId, request, ct);
        return ok ? NoContent() : Problem(statusCode: StatusCodes.Status400BadRequest, title: problem);
    }

    /// <summary>Autosave of the planner's unsaved selection. Not a plan version and not sent to anyone.</summary>
    [HttpPut("draft")]
    public async Task<IActionResult> SaveDraft(DraftRequest request, CancellationToken ct)
    {
        if (request.SectionIds.Count > 15) return Problem(statusCode: StatusCodes.Status400BadRequest, title: "That's too many classes for one term.");
        await preferences.SaveDraftAsync(user.UserId, request.SectionIds, ct);
        return NoContent();
    }

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

    /// <summary>Accepts the advisor's proposed changes; the plan becomes approved.</summary>
    [HttpPost("plan/accept")]
    public async Task<IActionResult> Accept(CancellationToken ct) => await plans.AcceptProposalAsync(user.UserId, ct) switch
    {
        SaveOutcome.Saved => NoContent(),
        SaveOutcome.NotFound => NotLoaded(),
        _ => Problem(statusCode: StatusCodes.Status409Conflict, title: "There are no proposed changes to accept."),
    };

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
public sealed class AdvisorController(IAdvisorService advisor, ICurrentUser user) : ControllerBase
{
    public sealed record ProposalCheckRequest(IReadOnlyList<Guid> SectionIds);

    /// <summary>Own students with standing, GPA, plan status, projected graduation and last activity.</summary>
    [HttpGet("students")]
    public Task<IReadOnlyList<AdviseeSummary>> Students(CancellationToken ct) => advisor.GetAdviseesAsync(user.UserId, ct);

    /// <summary>Counts for the advisor home page: awaiting review, needs review, approved this term, changes suggested.</summary>
    [HttpGet("overview")]
    public Task<AdvisorOverview> Overview(CancellationToken ct) => advisor.GetOverviewAsync(user.UserId, ct);

    /// <summary>Read-only student record with transcript (own students only).</summary>
    [HttpGet("students/{studentUserId:guid}")]
    [ProducesResponseType<StudentRecordView>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Student(Guid studentUserId, CancellationToken ct) =>
        await advisor.GetStudentRecordAsync(user.UserId, studentUserId, ct) is { } view ? Ok(view) : NotFound();

    /// <summary>Plans sent by the advisor's students (oldest first). Filtering and sorting happen in the app.</summary>
    [HttpGet("plans")]
    public Task<IReadOnlyList<QueueItem>> Plans(CancellationToken ct) => advisor.GetQueueAsync(user.UserId, ct);

    /// <summary>The one-page review. Opening a newly sent plan marks it "Advisor reviewing".</summary>
    [HttpGet("plans/{planId:guid}")]
    [ProducesResponseType<PlanReview>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Review(Guid planId, CancellationToken ct)
    {
        var (outcome, review) = await advisor.OpenReviewAsync(user.UserId, planId, ct);
        return outcome switch
        {
            DecisionOutcome.Done => Ok(review),
            DecisionOutcome.NotYourStudent => Forbid(),
            _ => NotFound(),
        };
    }

    /// <summary>Live check of the sections an advisor wants to propose.</summary>
    [HttpPost("plans/{planId:guid}/check")]
    public async Task<IActionResult> CheckProposal(Guid planId, ProposalCheckRequest request, CancellationToken ct) =>
        await advisor.CheckProposalAsync(user.UserId, planId, request.SectionIds, ct) is { } check ? Ok(check) : NotFound();

    /// <summary>Approve (optional note), reject (comment required) or propose changes (sections and note required).</summary>
    [HttpPost("plans/{planId:guid}/decision")]
    public async Task<IActionResult> Decide(Guid planId, DecisionRequest request, CancellationToken ct)
    {
        var result = await advisor.DecideAsync(user.UserId, planId, request, ct);
        return result.Outcome switch
        {
            DecisionOutcome.Done => Ok(result),
            DecisionOutcome.NotFound => NotFound(),
            DecisionOutcome.NotYourStudent => Forbid(),
            DecisionOutcome.NoteRequired => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Please write a note for the student."),
            DecisionOutcome.PlanHasErrors => BadRequest(result),
            _ => Conflict(result),
        };
    }
}

/// <summary>The signed-in user's own settings (any role): message language and the first-login walkthrough.</summary>
[ApiController]
[Route("api/me")]
public sealed class MeController(IUserSettingsService settings, IStudentPlanService plans, ICurrentUser user) : ControllerBase
{
    public sealed record LanguageRequest(string Language);

    /// <summary>Terms for the term selector in the top bar; only the current one is open for planning.</summary>
    [HttpGet("terms")]
    public Task<IReadOnlyList<TermOption>> Terms(CancellationToken ct) => plans.TermsAsync(ct);

    [HttpGet("settings")]
    public Task<UserSettingsView> Settings(CancellationToken ct) => settings.GetAsync(user.UserId, ct);

    /// <summary>The language for emails and other generated messages ("en" or "ar"); the app sets it when the user switches.</summary>
    [HttpPut("language")]
    public async Task<IActionResult> Language(LanguageRequest request, CancellationToken ct) =>
        await settings.SetLanguageAsync(user.UserId, request.Language, ct)
            ? NoContent()
            : Problem(statusCode: StatusCodes.Status400BadRequest, title: "Language must be \"en\" or \"ar\".");

    [HttpPost("walkthrough/dismiss")]
    public async Task<IActionResult> DismissWalkthrough(CancellationToken ct)
    {
        await settings.DismissWalkthroughAsync(user.UserId, ct);
        return NoContent();
    }
}

/// <summary>The signed-in user's notifications (any role).</summary>
[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController(INotificationService notifications, ICurrentUser user) : ControllerBase
{
    [HttpGet]
    public Task<NotificationList> Get(CancellationToken ct) => notifications.GetAsync(user.UserId, ct);

    [HttpPost("read")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(user.UserId, ct);
        return NoContent();
    }
}

/// <summary>Data loading for administrators. Contract: docs/import-contract.md.</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = Policies.Admin)]
public sealed class AdminDataController(IAcademicQueries queries, IImportService imports, IPlanRevalidationService revalidation, ICurrentUser user) : ControllerBase
{
    public const long MaxUploadBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Posts one final grade for a course in progress, then re-checks the student's approved plan: a prerequisite
    /// or corequisite that is no longer met moves it to "Needs review" and notifies the student and the advisor.
    /// </summary>
    [HttpPost("grades")]
    [ProducesResponseType<RecheckResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PostGrade(GradePostingRequest request, CancellationToken ct)
    {
        var result = await revalidation.PostGradeAsync(user.UserId, request, ct);
        return result.Outcome switch
        {
            GradePostingOutcome.Posted => Ok(result.Recheck),
            GradePostingOutcome.InvalidGrade => Problem(statusCode: StatusCodes.Status400BadRequest, title: $"'{request.Grade}' isn't a final grade (A to F, P or W)."),
            GradePostingOutcome.StudentNotFound => Problem(statusCode: StatusCodes.Status404NotFound, title: $"There is no student {request.StudentId}."),
            GradePostingOutcome.CourseNotFound => Problem(statusCode: StatusCodes.Status404NotFound, title: $"There is no course {request.CourseCode}."),
            _ => Problem(statusCode: StatusCodes.Status409Conflict, title: $"{request.CourseCode} in {request.TermCode} isn't in progress for this student, so there is no grade to post."),
        };
    }

    /// <summary>Re-checks every approved plan for the current term (e.g. after a batch of grades was posted).</summary>
    [HttpPost("recheck")]
    public Task<RecheckResult> Recheck(CancellationToken ct) => revalidation.RecheckCurrentTermAsync(user.UserId, null, ct);

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
        if (!ImportKinds.TryParse(kind, out var importKind)) return UnknownKind(kind);
        if (file is null || file.Length == 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Choose a CSV file to upload.");

        await using var stream = file.OpenReadStream();
        var report = await imports.ImportAsync(importKind, stream, dryRun, user.UserId, ct, Domain.Activity.ImportSource.Upload, file.FileName);
        return await ImportResult(importKind, report, ct);
    }

    /// <summary>
    /// Errors are listed by line and column with HTTP 422, and the whole file is rejected (brief §11). New grades in a
    /// saved transcripts file re-check approved plans (brief §10).
    /// </summary>
    private async Task<IActionResult> ImportResult(ImportKind kind, ImportReport report, CancellationToken ct)
    {
        if (report.HasErrors) return UnprocessableEntity(report);
        if (report.Saved && kind == ImportKind.Transcripts) await revalidation.RecheckCurrentTermAsync(user.UserId, null, ct);
        return Ok(report);
    }

    private ObjectResult UnknownKind(string kind) =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: $"'{kind}' isn't a kind of data. Use one of: terms, courses, programs, degree-plans, sections, students, transcripts.");

    /// <summary>A catalog table in the import-file format (the same columns an export file has).</summary>
    [HttpGet("catalog/{kind}")]
    public async Task<IActionResult> CatalogRows(string kind, [FromServices] IAdminCatalogService catalog, CancellationToken ct) =>
        ImportKinds.TryParse(kind, out var k) ? Ok(await catalog.RowsAsync(k, ct)) : UnknownKind(kind);

    /// <summary>Edits rows through the importer: same checks, preview (dryRun=true) and catalog version as a file.</summary>
    [HttpPost("catalog/{kind}/rows")]
    public async Task<IActionResult> EditRows(string kind, RowEditRequest request, [FromServices] IAdminCatalogService catalog, CancellationToken ct)
    {
        if (!ImportKinds.TryParse(kind, out var k)) return UnknownKind(kind);
        if (request.Rows.Count is 0 or > 500) return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Send between 1 and 500 rows.");
        return await ImportResult(k, await catalog.EditAsync(k, request, user.UserId, ct), ct);
    }

    /// <summary>Live check of a prerequisite rule while it's typed: can it be read, unknown courses, cycles.</summary>
    [HttpPost("catalog/prerequisites/check")]
    public Task<PrerequisiteCheckResult> CheckPrerequisite(PrerequisiteCheckRequest request, [FromServices] IAdminCatalogService catalog, CancellationToken ct) =>
        catalog.CheckPrerequisiteAsync(request, ct);

    /// <summary>Removes a degree plan; 409 while students are still assigned to it.</summary>
    [HttpDelete("catalog/degree-plans/{programCode}/{catalogYear:int}")]
    public async Task<IActionResult> RemoveDegreePlan(string programCode, int catalogYear, [FromServices] IAdminCatalogService catalog, CancellationToken ct)
    {
        var r = await catalog.RemoveDegreePlanAsync(programCode, catalogYear, user.UserId, ct);
        return r.Outcome switch
        {
            RemovalOutcome.Removed => Ok(r),
            RemovalOutcome.StillAssigned => Problem(statusCode: StatusCodes.Status409Conflict,
                title: $"{r.StudentsAssigned} students still follow this degree plan. Move them to another plan first."),
            _ => NotFound(),
        };
    }

    /// <summary>One file in the import format (round-trips: importing it again changes nothing).</summary>
    [HttpGet("exports/{kind}")]
    public async Task<IActionResult> Export(string kind, [FromServices] IAdminCatalogService catalog, CancellationToken ct) =>
        ImportKinds.TryParse(kind, out var k)
            ? File(System.Text.Encoding.UTF8.GetBytes(await catalog.ExportCsvAsync(k, ct)), "text/csv; charset=utf-8", $"{ImportKinds.Slug(k)}.csv")
            : UnknownKind(kind);

    /// <summary>All seven files in a zip, numbered in import order.</summary>
    [HttpGet("exports")]
    public async Task<IActionResult> ExportAll([FromServices] IAdminCatalogService catalog, CancellationToken ct) =>
        File(await catalog.ExportZipAsync(ct), "application/zip", "liwa-data.zip");

    [HttpGet("credit-policy")]
    public async Task<IActionResult> CreditPolicy([FromServices] ICreditPolicyAdmin policy, CancellationToken ct) =>
        await policy.GetAsync(ct) is { } p ? Ok(p) : NotFound();

    /// <summary>The credit-rule test: lists every student whose limits would change (nothing is saved).</summary>
    [HttpPost("credit-policy/preview")]
    public Task<CreditPolicyPreview> PreviewCreditPolicy(CreditPolicyDto proposed, [FromServices] ICreditPolicyAdmin policy, CancellationToken ct) =>
        policy.PreviewAsync(proposed, ct);

    [HttpPut("credit-policy")]
    public async Task<IActionResult> SaveCreditPolicy(CreditPolicyDto proposed, [FromServices] ICreditPolicyAdmin policy, CancellationToken ct)
    {
        var r = await policy.SaveAsync(proposed, user.UserId, ct);
        return r.Problems.Count > 0 ? UnprocessableEntity(r) : Ok(r);
    }

    [HttpGet("users")]
    public Task<IReadOnlyList<StaffUser>> Users([FromServices] IStaffAdmin staff, CancellationToken ct) => staff.ListAsync(ct);

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(CreateStaffRequest request, [FromServices] IStaffAdmin staff, CancellationToken ct) =>
        UserResult(await staff.CreateAsync(request, user.UserId, ct));

    [HttpPut("users/{id:guid}/password")]
    public async Task<IActionResult> SetPassword(Guid id, PasswordRequest request, [FromServices] IStaffAdmin staff, CancellationToken ct) =>
        UserResult(await staff.SetPasswordAsync(id, request.Password, user.UserId, ct));

    [HttpPut("users/{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, ActiveRequest request, [FromServices] IStaffAdmin staff, CancellationToken ct) =>
        UserResult(await staff.SetActiveAsync(id, request.Active, user.UserId, ct));

    /// <summary>Moves students from one advisor to another and tells everyone involved.</summary>
    [HttpPost("users/reassign")]
    public async Task<IActionResult> Reassign(ReassignRequest request, [FromServices] IStaffAdmin staff, CancellationToken ct)
    {
        var (outcome, problem, result) = await staff.ReassignAsync(request, user.UserId, ct);
        return outcome == UserAdminOutcome.Done ? Ok(result) : UserResult(new UserAdminResult(outcome, problem));
    }

    private IActionResult UserResult(UserAdminResult r) => r.Outcome switch
    {
        UserAdminOutcome.Done => r.User is null ? NoContent() : Ok(r.User),
        UserAdminOutcome.NotFound => Problem(statusCode: StatusCodes.Status404NotFound, title: r.Problem ?? "There is no such staff account."),
        UserAdminOutcome.Conflict => Problem(statusCode: StatusCodes.Status409Conflict, title: r.Problem),
        _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: r.Problem),
    };

    [HttpGet("settings/sso")]
    public Task<SsoSettingsView> SsoSettings([FromServices] ISettingsService settings, CancellationToken ct) => settings.GetSsoAsync(ct);

    [HttpPut("settings/sso")]
    public async Task<IActionResult> SaveSsoSettings(SsoSettingsRequest request, [FromServices] ISettingsService settings, CancellationToken ct)
    {
        var (ok, problem) = await settings.SaveSsoAsync(request, user.UserId, ct);
        return ok ? Ok(await settings.GetSsoAsync(ct)) : Problem(statusCode: StatusCodes.Status400BadRequest, title: problem);
    }

    [HttpPost("settings/sso/test")]
    public Task<ConnectionTestResult> TestSso([FromServices] ISettingsService settings, CancellationToken ct) => settings.TestSsoAsync(ct);

    [HttpGet("settings/ai")]
    public Task<AiSettingsView> AiSettings([FromServices] ISettingsService settings, CancellationToken ct) => settings.GetAiAsync(ct);

    [HttpPut("settings/ai")]
    public async Task<IActionResult> SaveAiSettings(AiSettingsRequest request, [FromServices] ISettingsService settings, CancellationToken ct)
    {
        var (ok, problem) = await settings.SaveAiAsync(request, user.UserId, ct);
        return ok ? Ok(await settings.GetAiAsync(ct)) : Problem(statusCode: StatusCodes.Status400BadRequest, title: problem);
    }
}

/// <summary>Catalog version history: every saved import, edit and removal (admins and advisors).</summary>
[ApiController]
[Route("api/catalog")]
[Authorize(Policy = Policies.Staff)]
public sealed class CatalogVersionsController(IAdminCatalogService catalog) : ControllerBase
{
    [HttpGet("versions")]
    public Task<IReadOnlyList<CatalogVersionView>> Versions([FromQuery] int take = 100, CancellationToken ct = default) => catalog.VersionsAsync(take, ct);
}
