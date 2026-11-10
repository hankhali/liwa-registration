using System.Text.Json;
using Liwa.Application.Planning;
using Liwa.Domain.Academic;
using Liwa.Domain.Academic.Prerequisites;
using Liwa.Domain.Activity;
using Liwa.Domain.Planning;
using Liwa.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liwa.Infrastructure.Academic;

/// <summary>
/// Student planning: builds the planner input from the database (all I/O and "today" live here; the
/// planner stays pure), checks hand-picked plans, and saves versioned plans.
/// </summary>
internal sealed partial class StudentPlanService(LiwaDbContext db, TimeProvider clock, Application.Academic.IAcademicQueries queries, Notifier notifier)
    : IStudentPlanService, IRecommendationService, IAdvisorService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record Context(
        PlannerInput Input,
        StudentRecord Record,
        Term Term,
        IReadOnlyList<Course> Courses,
        IReadOnlyList<Section> Sections,
        string? AdvisorName);

    public async Task<PlannerResult?> RecommendAsync(Guid studentUserId, Availability? availability, PlannerPreferences preferences, CancellationToken ct)
    {
        var ctx = await BuildAsync(studentUserId, availability, preferences, ct);
        return ctx is null ? null : Planner.Plan(ctx.Input);
    }

    public async Task<PlanningView?> GetViewAsync(Guid studentUserId, Availability? availability, PlannerPreferences preferences, CancellationToken ct)
    {
        var ctx = await BuildAsync(studentUserId, availability, preferences, ct);
        if (ctx is null) return null;

        var result = Planner.Plan(ctx.Input);
        var relevant = result.Courses.Select(c => c.CourseCode).Concat(result.Plan.Select(p => p.CourseCode)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var saved = await LoadPlanAsync(ctx.Record.Id, ctx.Term.Id, ct);
        var codeById = ctx.Courses.ToDictionary(c => c.Id, c => c.Code);
        if (saved is not null)
        {
            var savedCourses = ctx.Sections.Where(s => saved.SectionIds.Contains(s.Id)).Select(s => codeById[s.CourseId]);
            relevant.UnionWith(savedCourses);
        }

        return new PlanningView(
            ctx.Term.Code, ctx.Term.Name, ctx.AdvisorName, result,
            ctx.Courses.Where(c => relevant.Contains(c.Code)).OrderBy(c => c.Code, StringComparer.Ordinal)
                .Select(c => new CourseInfo(c.Code, c.Title, c.TitleAr, c.Credits)).ToList(),
            ctx.Sections.Where(s => relevant.Contains(codeById[s.CourseId]))
                .OrderBy(s => codeById[s.CourseId], StringComparer.Ordinal).ThenBy(s => s.SectionCode, StringComparer.Ordinal)
                .Select(s => new SectionInfo(s.Id, codeById[s.CourseId], s.SectionCode, s.Delivery, s.Instructor, s.Room, s.Meetings.ToList(), s.SeatsLeft)).ToList(),
            saved,
            saved is null ? [] : await HistoryAsync(saved.Id, ct));
    }

    public async Task<PlanCheckResult?> CheckAsync(Guid studentUserId, IReadOnlyCollection<Guid> sectionIds, Availability? availability, CancellationToken ct)
    {
        var ctx = await BuildAsync(studentUserId, availability, PlannerPreferences.None, ct);
        return ctx is null ? null : Planner.Check(ctx.Input, sectionIds);
    }

    public async Task<SavePlanResult> SaveAsync(Guid studentUserId, SavePlanRequest request, CancellationToken ct)
    {
        var availability = request.Availability is { Count: > 0 } w ? new Availability(w) : null;
        var ctx = await BuildAsync(studentUserId, availability, PlannerPreferences.None, ct);
        if (ctx is null) return new SavePlanResult(SaveOutcome.NotFound, null, null);

        var check = Planner.Check(ctx.Input, request.SectionIds);
        if (!check.IsValid || request.SectionIds.Count == 0) return new SavePlanResult(SaveOutcome.Invalid, check, null);

        if (request.Submit)
        {
            var confirmed = (request.AcknowledgedWarnings ?? []).ToHashSet(StringComparer.Ordinal);
            if (check.Warnings.Any(x => !confirmed.Contains(Planner.WarningKey(x))))
                return new SavePlanResult(SaveOutcome.WarningsNotConfirmed, check, null);
        }

        var plan = await db.Plans.Include(p => p.Versions).ThenInclude(v => v.Sections)
            .SingleOrDefaultAsync(p => p.StudentRecordId == ctx.Record.Id && p.TermId == ctx.Term.Id, ct);
        if (plan is { Status: PlanStatus.Submitted or PlanStatus.AdvisorReviewing or PlanStatus.Approved })
            return new SavePlanResult(SaveOutcome.AlreadyWithAdvisor, check, ToSaved(plan));

        var now = clock.GetUtcNow();
        var isNew = plan is null;
        plan ??= new Plan(Guid.NewGuid(), ctx.Record.Id, ctx.Term.Id, now);
        if (isNew) db.Plans.Add(plan);
        var before = plan.Current;
        var beforeStatus = plan.Status;

        var acknowledged = request.Submit && check.Warnings.Count > 0
            ? JsonSerializer.Serialize(check.Warnings.Select(x => new { key = Planner.WarningKey(x), code = x.Code.ToString(), x.Courses, x.Value }), Json)
            : null;
        var version = plan.AddVersion(request.SectionIds, studentUserId, request.Note, now, acknowledged,
            request.Availability is { Count: > 0 } ? JsonSerializer.Serialize(request.Availability, Json) : null);
        plan.SetStatus(request.Submit ? PlanStatus.Submitted : PlanStatus.Draft, now);

        // Every status change is logged with the versions before and after, and notifies both sides (brief §10).
        if (request.Submit)
            RecordChange(plan, studentUserId, "plan.submitted", beforeStatus, before, request.Note, studentUserId, ctx.Record.AdvisorUserId,
                "notifications.planSubmitted", await NamesAsync(studentUserId, ctx.Record.AdvisorUserId, ctx.Term.Id, ct));
        else
            db.AuditLog.Add(new AuditEntry(Guid.NewGuid(), now, studentUserId, "plan.saved", "Plan", plan.Id.ToString(),
                Snapshot(beforeStatus, before), Snapshot(plan.Status, version, request.Note)));

        await db.SaveChangesAsync(ct);
        return new SavePlanResult(SaveOutcome.Saved, check, ToSaved(plan));
    }

    // ---------------------------------------------------------------------------------------------

    private async Task<SavedPlan?> LoadPlanAsync(Guid recordId, Guid termId, CancellationToken ct)
    {
        var plan = await db.Plans.AsNoTracking().Include(p => p.Versions).ThenInclude(v => v.Sections)
            .SingleOrDefaultAsync(p => p.StudentRecordId == recordId && p.TermId == termId, ct);
        return plan is null ? null : ToSaved(plan);
    }

    private static SavedPlan ToSaved(Plan plan)
    {
        var current = plan.Current!;
        var availability = current.Availability is null ? null : JsonSerializer.Deserialize<List<TimeWindow>>(current.Availability, Json);
        return new SavedPlan(plan.Id, plan.Status, current.Number, current.Sections.Select(s => s.SectionId).ToList(), plan.UpdatedAt, availability);
    }

    private async Task<Context?> BuildAsync(Guid studentUserId, Availability? availability, PlannerPreferences preferences, CancellationToken ct)
    {
        var record = await db.StudentRecords.AsNoTracking()
            .Include(s => s.Transcript).Include(s => s.TransferCredits)
            .SingleOrDefaultAsync(s => s.UserId == studentUserId, ct);
        if (record is null) return null;

        var term = await db.Terms.AsNoTracking().FirstOrDefaultAsync(t => t.IsCurrent, ct);
        var program = await db.Programs.AsNoTracking().SingleAsync(p => p.Id == record.ProgramId, ct);
        var plan = await db.DegreePlans.AsNoTracking().Include(d => d.Groups).ThenInclude(g => g.Courses)
            .SingleOrDefaultAsync(d => d.ProgramId == record.ProgramId && d.CatalogYear == record.CatalogYear, ct);
        if (term is null || plan is null) return null;

        var courses = await db.Courses.AsNoTracking().ToListAsync(ct);
        var codeById = courses.ToDictionary(c => c.Id, c => c.Code);
        var sections = await db.Sections.AsNoTracking().Where(s => s.TermId == term.Id).ToListAsync(ct);
        var policy = await db.CreditPolicies.AsNoTracking().Include(p => p.Rules).FirstOrDefaultAsync(ct)
                     ?? new CreditPolicy(Guid.Empty, "No credit policy configured");
        var advisorName = record.AdvisorUserId is { } a
            ? await db.Users.AsNoTracking().Where(u => u.Id == a).Select(u => u.DisplayName).SingleOrDefaultAsync(ct)
            : null;

        var input = new PlannerInput(
            DateOnly.FromDateTime(clock.GetLocalNow().DateTime),
            new PlannerTerm(term.Code, term.Season, term.Year),
            PlannerFacts.StudentFacts(record, policy.RepeatGradeRule, codeById, program.TotalCredits),
            courses.Select(c => new PlannerCourse(c.Code, c.Credits, PrerequisiteParser.TryParse(c.Prerequisites, out var p, out _) ? p : null, c.Offered)).ToList(),
            plan.Groups.OrderBy(g => g.SortOrder).Select(g => new PlannerRequirementGroup(g.Key, g.Name, g.Kind, g.CreditsRequired,
                g.Courses.Select(c => new PlannerRequirementCourse(codeById[c.CourseId], c.SuggestedSemester)).ToList())).ToList(),
            sections.Select(s => new PlannerSection(s.Id, codeById[s.CourseId], s.SectionCode, s.Delivery, s.Meetings.ToList(), s.SeatsLeft)).ToList(),
            policy, availability, preferences);

        return new Context(input, record, term, courses, sections, advisorName);
    }
}
