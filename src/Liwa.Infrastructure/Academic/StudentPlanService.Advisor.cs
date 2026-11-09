using System.Text.Json;
using Liwa.Application.Planning;
using Liwa.Domain.Academic;
using Liwa.Domain.Planning;
using Microsoft.EntityFrameworkCore;

namespace Liwa.Infrastructure.Academic;

/// <summary>The advisor's side: review queue, one-page review, decisions and the read-only student record.</summary>
internal sealed partial class StudentPlanService
{
    /// <summary>A sent plan waiting longer than this is marked urgent.</summary>
    private static readonly TimeSpan UrgentAfter = TimeSpan.FromDays(3);

    private static readonly PlanStatus[] Decidable = [PlanStatus.Submitted, PlanStatus.AdvisorReviewing, PlanStatus.NeedsReview];

    public async Task<IReadOnlyList<QueueItem>> GetQueueAsync(Guid advisorUserId, CancellationToken ct)
    {
        var records = await db.StudentRecords.AsNoTracking().Where(r => r.AdvisorUserId == advisorUserId).ToListAsync(ct);
        var recordIds = records.Select(r => r.Id).ToList();
        var plans = await db.Plans.AsNoTracking().Include(p => p.Versions).ThenInclude(v => v.Sections)
            .Where(p => recordIds.Contains(p.StudentRecordId) && p.Status != PlanStatus.Draft)
            .ToListAsync(ct);
        if (plans.Count == 0) return [];

        var userIds = records.Select(r => r.UserId).ToList();
        var names = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var programs = await db.Programs.AsNoTracking().ToDictionaryAsync(p => p.Id, ct);
        var sectionIds = plans.SelectMany(p => p.Current!.Sections.Select(s => s.SectionId)).ToHashSet();
        var credits = await db.Sections.AsNoTracking().Where(s => sectionIds.Contains(s.Id))
            .Join(db.Courses.AsNoTracking(), s => s.CourseId, c => c.Id, (s, c) => new { s.Id, c.Credits })
            .ToDictionaryAsync(x => x.Id, x => x.Credits, ct);
        var now = clock.GetUtcNow();

        return plans.Select(p =>
        {
            var r = records.Single(x => x.Id == p.StudentRecordId);
            var program = programs[r.ProgramId];
            var v = p.Current!;
            var warnings = v.AcknowledgedWarnings is null ? 0 : JsonDocument.Parse(v.AcknowledgedWarnings).RootElement.GetArrayLength();
            var probation = r.Status == AcademicStatus.Probation;
            var waitingLong = p.Status is PlanStatus.Submitted or PlanStatus.AdvisorReviewing && now - p.UpdatedAt > UrgentAfter;
            return new QueueItem(p.Id, r.UserId, names.GetValueOrDefault(r.UserId, r.StudentId), r.StudentId, program.Code, program.Name, program.NameAr,
                p.Status, v.Number, v.Sections.Sum(s => credits.GetValueOrDefault(s.SectionId)), warnings, p.UpdatedAt, probation,
                p.Status == PlanStatus.NeedsReview || (Decidable.Contains(p.Status) && (probation || waitingLong)));
        }).OrderBy(q => q.UpdatedAt).ToList();
    }

    public async Task<(DecisionOutcome Outcome, PlanReview? Review)> OpenReviewAsync(Guid advisorUserId, Guid planId, CancellationToken ct)
    {
        var (outcome, plan, record) = await LoadForAdvisorAsync(advisorUserId, planId, tracking: true, ct);
        if (outcome != DecisionOutcome.Done) return (outcome, null);

        if (plan!.Status == PlanStatus.Submitted)
        {
            var before = plan.Status;
            plan.SetStatus(PlanStatus.AdvisorReviewing, clock.GetUtcNow());
            var advisor = await db.Users.Where(u => u.Id == advisorUserId).Select(u => u.DisplayName).SingleAsync(ct);
            RecordChange(plan, advisorUserId, "plan.reviewing", before, plan.Current, null, record!.UserId,
                "notifications.planReviewing", new { advisor }, "/student");
            await db.SaveChangesAsync(ct);
        }

        var version = plan.Current!;
        var windows = version.Availability is null ? null : JsonSerializer.Deserialize<List<TimeWindow>>(version.Availability, Json);
        var availability = windows is { Count: > 0 } ? new Availability(windows) : null;
        var ctx = await BuildAsync(record!.UserId, availability, PlannerPreferences.None, ct);
        var view = await GetViewAsync(record.UserId, availability, PlannerPreferences.None, ct);
        var summary = await queries.GetStudentSummaryAsync(record.UserId, ct);
        if (ctx is null || view is null || summary is null) return (DecisionOutcome.NotFound, null);

        var ids = version.Sections.Select(s => s.SectionId).ToList();
        var confirmed = version.AcknowledgedWarnings is null
            ? []
            : JsonSerializer.Deserialize<List<ConfirmedWarning>>(version.AcknowledgedWarnings, Json) ?? [];
        var name = await db.Users.AsNoTracking().Where(u => u.Id == record.UserId).Select(u => u.DisplayName).SingleAsync(ct);

        return (DecisionOutcome.Done, new PlanReview(plan.Id, record.UserId, plan.Status, version.Number, summary, name, view, ids,
            Planner.Check(ctx.Input, ids), confirmed, Planner.ProjectPlan(ctx.Input, ids), view.History));
    }

    public async Task<PlanCheckResult?> CheckProposalAsync(Guid advisorUserId, Guid planId, IReadOnlyCollection<Guid> sectionIds, CancellationToken ct)
    {
        var (outcome, plan, record) = await LoadForAdvisorAsync(advisorUserId, planId, tracking: false, ct);
        if (outcome != DecisionOutcome.Done) return null;
        var ctx = await BuildAsync(record!.UserId, AvailabilityOf(plan!.Current!), PlannerPreferences.None, ct);
        return ctx is null ? null : Planner.Check(ctx.Input, sectionIds);
    }

    public async Task<DecisionResult> DecideAsync(Guid advisorUserId, Guid planId, DecisionRequest request, CancellationToken ct)
    {
        var (outcome, plan, record) = await LoadForAdvisorAsync(advisorUserId, planId, tracking: true, ct);
        if (outcome != DecisionOutcome.Done) return new DecisionResult(outcome, null);
        if (!Decidable.Contains(plan!.Status)) return new DecisionResult(DecisionOutcome.NotDecidable, null);

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (request.Decision != Decision.Approve && note is null) return new DecisionResult(DecisionOutcome.NoteRequired, null);

        var current = plan.Current!;
        var ctx = await BuildAsync(record!.UserId, AvailabilityOf(current), PlannerPreferences.None, ct);
        if (ctx is null) return new DecisionResult(DecisionOutcome.NotFound, null);

        var before = plan.Status;
        var now = clock.GetUtcNow();
        var advisor = await db.Users.Where(u => u.Id == advisorUserId).Select(u => u.DisplayName).SingleAsync(ct);
        PlanCheckResult? check = null;
        string action, messageKey;

        switch (request.Decision)
        {
            case Decision.Approve:
                // Approving is disabled while the live checks show errors.
                check = Planner.Check(ctx.Input, current.Sections.Select(s => s.SectionId).ToList());
                if (!check.IsValid) return new DecisionResult(DecisionOutcome.PlanHasErrors, check);
                plan.SetStatus(PlanStatus.Approved, now);
                (action, messageKey) = ("plan.approved", "notifications.planApproved");
                break;
            case Decision.Reject:
                plan.SetStatus(PlanStatus.Rejected, now);
                (action, messageKey) = ("plan.rejected", "notifications.planRejected");
                break;
            case Decision.ProposeChanges:
                var ids = request.SectionIds ?? [];
                check = Planner.Check(ctx.Input, ids);
                if (!check.IsValid || ids.Count == 0) return new DecisionResult(DecisionOutcome.PlanHasErrors, check);
                plan.AddVersion(ids, advisorUserId, note, now, null, current.Availability);
                plan.SetStatus(PlanStatus.ChangesProposed, now);
                (action, messageKey) = ("plan.proposed", "notifications.planChangesProposed");
                break;
            default:
                return new DecisionResult(DecisionOutcome.NotDecidable, null);
        }

        RecordChange(plan, advisorUserId, action, before, current, note, record.UserId, messageKey, new { advisor }, "/student");
        await db.SaveChangesAsync(ct);
        return new DecisionResult(DecisionOutcome.Done, check);
    }

    public async Task<StudentRecordView?> GetStudentRecordAsync(Guid advisorUserId, Guid studentUserId, CancellationToken ct)
    {
        var record = await db.StudentRecords.AsNoTracking().Include(r => r.Transcript).Include(r => r.TransferCredits)
            .SingleOrDefaultAsync(r => r.UserId == studentUserId && r.AdvisorUserId == advisorUserId, ct);
        if (record is null) return null;
        var summary = await queries.GetStudentSummaryAsync(studentUserId, ct);
        if (summary is null) return null;

        var courses = await db.Courses.AsNoTracking().ToDictionaryAsync(c => c.Id, ct);
        var lines = record.Transcript
            .Select(t => new TranscriptLine(t.TermCode, courses[t.CourseId].Code, courses[t.CourseId].Title, courses[t.CourseId].TitleAr, t.Credits, t.Grade, t.Status.ToString()))
            .Concat(record.TransferCredits.Select(t => new TranscriptLine("", courses[t.CourseId].Code, courses[t.CourseId].Title, courses[t.CourseId].TitleAr, t.Credits, "TR", "Transfer")))
            .OrderBy(l => l.TermCode.Length == 0 ? 0 : TermCodes.SortKey(l.TermCode)).ThenBy(l => l.CourseCode, StringComparer.Ordinal)
            .ToList();

        var term = await db.Terms.AsNoTracking().FirstOrDefaultAsync(t => t.IsCurrent, ct);
        var plan = term is null ? null : await db.Plans.AsNoTracking().SingleOrDefaultAsync(p => p.StudentRecordId == record.Id && p.TermId == term.Id, ct);
        var name = await db.Users.AsNoTracking().Where(u => u.Id == studentUserId).Select(u => u.DisplayName).SingleAsync(ct);
        return new StudentRecordView(studentUserId, name, summary, lines, plan?.Status, plan?.Id);
    }

    private async Task<(DecisionOutcome, Plan?, StudentRecord?)> LoadForAdvisorAsync(Guid advisorUserId, Guid planId, bool tracking, CancellationToken ct)
    {
        var query = db.Plans.Include(p => p.Versions).ThenInclude(v => v.Sections).AsQueryable();
        if (!tracking) query = query.AsNoTracking();
        var plan = await query.SingleOrDefaultAsync(p => p.Id == planId, ct);
        if (plan is null || plan.Status == PlanStatus.Draft) return (DecisionOutcome.NotFound, null, null);
        var record = await db.StudentRecords.AsNoTracking().SingleAsync(r => r.Id == plan.StudentRecordId, ct);
        return record.AdvisorUserId == advisorUserId ? (DecisionOutcome.Done, plan, record) : (DecisionOutcome.NotYourStudent, null, null);
    }

    private static Availability? AvailabilityOf(PlanVersion v)
    {
        var windows = v.Availability is null ? null : JsonSerializer.Deserialize<List<TimeWindow>>(v.Availability, Json);
        return windows is { Count: > 0 } ? new Availability(windows) : null;
    }
}
