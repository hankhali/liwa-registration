using System.Text.Json;
using Liwa.Application.Planning;
using Liwa.Domain.Academic;
using Liwa.Domain.Activity;
using Liwa.Domain.Planning;
using Microsoft.EntityFrameworkCore;

namespace Liwa.Infrastructure.Academic;

/// <summary>Grade posting and re-validation of approved plans (brief §10).</summary>
internal sealed partial class StudentPlanService : IPlanRevalidationService
{
    public async Task<RecheckResult> RecheckCurrentTermAsync(Guid? actorUserId, IReadOnlyCollection<Guid>? studentRecordIds, CancellationToken ct)
    {
        var term = await db.Terms.AsNoTracking().FirstOrDefaultAsync(t => t.IsCurrent, ct);
        if (term is null) return new RecheckResult(0, []);

        var query = db.Plans.Include(p => p.Versions).ThenInclude(v => v.Sections)
            .Where(p => p.TermId == term.Id && p.Status == PlanStatus.Approved);
        if (studentRecordIds is not null) query = query.Where(p => studentRecordIds.Contains(p.StudentRecordId));
        var plans = await query.OrderBy(p => p.Id).ToListAsync(ct);

        var moved = new List<RecheckedPlan>();
        foreach (var plan in plans)
        {
            var record = await db.StudentRecords.AsNoTracking().SingleAsync(r => r.Id == plan.StudentRecordId, ct);
            var current = plan.Current!;
            var ctx = await BuildAsync(record.UserId, AvailabilityOf(current), PlannerPreferences.None, ct);
            if (ctx is null) continue;

            var problems = Planner.RevalidationFailures(Planner.Check(ctx.Input, current.Sections.Select(s => s.SectionId).ToList()));
            if (problems.Count == 0) continue;

            var before = plan.Status;
            plan.SetStatus(PlanStatus.NeedsReview, clock.GetUtcNow());
            db.AuditLog.Add(new AuditEntry(Guid.NewGuid(), clock.GetUtcNow(), actorUserId, "plan.needsReview", "Plan", plan.Id.ToString(),
                Snapshot(before, current), Snapshot(plan.Status, current,
                    JsonSerializer.Serialize(problems.Select(p => new { code = p.Code.ToString(), p.Courses, p.Value }), Json))));

            var names = await NamesAsync(record.UserId, record.AdvisorUserId, term.Id, ct);
            var parameters = new { student = names.Student, advisor = names.Advisor, term = names.Term };
            notifier.Notify(record.UserId, "notifications.planNeedsReviewStudent", parameters, "/student/plan");
            if (record.AdvisorUserId is { } advisor)
                notifier.Notify(advisor, "notifications.planNeedsReviewAdvisor", parameters, $"/advisor/plans/{plan.Id}");
            moved.Add(new RecheckedPlan(plan.Id, record.StudentId, problems));
        }

        await db.SaveChangesAsync(ct);
        return new RecheckResult(plans.Count, moved);
    }

    public async Task<GradePostingResult> PostGradeAsync(Guid actorUserId, GradePostingRequest request, CancellationToken ct)
    {
        var grade = Grades.Normalize(request.Grade ?? "");
        if (grade != "W" && !Grades.IsKnown(grade)) return new GradePostingResult(GradePostingOutcome.InvalidGrade, null);

        var record = await db.StudentRecords.Include(r => r.Transcript).Include(r => r.TransferCredits)
            .SingleOrDefaultAsync(r => r.StudentId == request.StudentId.Trim(), ct);
        if (record is null) return new GradePostingResult(GradePostingOutcome.StudentNotFound, null);

        var code = (request.CourseCode ?? "").Trim().ToUpperInvariant();
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(c => c.Code == code, ct);
        if (course is null) return new GradePostingResult(GradePostingOutcome.CourseNotFound, null);

        if (!record.PostGrade(course.Id, request.TermCode ?? "", grade)) return new GradePostingResult(GradePostingOutcome.NotInProgress, null);

        db.AuditLog.Add(new AuditEntry(Guid.NewGuid(), clock.GetUtcNow(), actorUserId, "grade.posted", "StudentRecord", record.Id.ToString(), null,
            JsonSerializer.Serialize(new { studentId = record.StudentId, course = code, term = request.TermCode?.Trim().ToUpperInvariant(), grade }, Json)));
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        return new GradePostingResult(GradePostingOutcome.Posted, await RecheckCurrentTermAsync(actorUserId, [record.Id], ct));
    }
}
