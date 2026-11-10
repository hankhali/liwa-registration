using System.Text.Json;
using Liwa.Application.Planning;
using Liwa.Domain.Activity;
using Liwa.Domain.Planning;
using Liwa.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Liwa.Infrastructure.Academic;

internal sealed partial class StudentPlanService
{
    /// <summary>Audit snapshot of a plan state: status, version, sections and an optional note.</summary>
    private static string Snapshot(PlanStatus status, PlanVersion? version, string? note = null) =>
        JsonSerializer.Serialize(new
        {
            status = status.ToString(),
            version = version?.Number,
            sections = version?.Sections.Select(s => s.SectionId).ToList() ?? [],
            note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        }, Json);

    /// <summary>Names used in notification messages: student, advisor, term.</summary>
    private sealed record MessageNames(string Student, string Advisor, string Term);

    private async Task<MessageNames> NamesAsync(Guid studentUserId, Guid? advisorUserId, Guid termId, CancellationToken ct)
    {
        var student = await db.Users.AsNoTracking().Where(u => u.Id == studentUserId).Select(u => u.DisplayName).SingleAsync(ct);
        var advisor = advisorUserId is { } a ? await db.Users.AsNoTracking().Where(u => u.Id == a).Select(u => u.DisplayName).SingleOrDefaultAsync(ct) : null;
        var term = await db.Terms.AsNoTracking().Where(t => t.Id == termId).Select(t => t.Name).SingleAsync(ct);
        return new MessageNames(student, advisor ?? "", term);
    }

    /// <summary>
    /// Writes the audit entry for a status change and notifies both sides (brief §4/§10): the other person gets
    /// <paramref name="otherKey"/>, the person who acted gets a "notifications.you.{action}" confirmation.
    /// </summary>
    private void RecordChange(Plan plan, Guid actorUserId, string action, PlanStatus beforeStatus, PlanVersion? beforeVersion, string? note,
        Guid studentUserId, Guid? advisorUserId, string otherKey, MessageNames names)
    {
        db.AuditLog.Add(new AuditEntry(Guid.NewGuid(), clock.GetUtcNow(), actorUserId, action, "Plan", plan.Id.ToString(),
            Snapshot(beforeStatus, beforeVersion), Snapshot(plan.Status, plan.Current, note)));

        var parameters = new { student = names.Student, advisor = names.Advisor, term = names.Term };
        var studentLink = "/student/plan";
        var advisorLink = $"/advisor/plans/{plan.Id}";
        var self = "notifications.you." + action.Replace("plan.", "", StringComparison.Ordinal);
        var actorIsStudent = actorUserId == studentUserId;

        if (actorIsStudent)
        {
            if (advisorUserId is { } advisor) notifier.Notify(advisor, otherKey, parameters, advisorLink);
            notifier.Notify(studentUserId, self, parameters, studentLink);
        }
        else
        {
            notifier.Notify(studentUserId, otherKey, parameters, studentLink);
            notifier.Notify(actorUserId, self, parameters, advisorLink);
        }
    }

    /// <summary>The plan's history from the audit log: who did what, with classes added and removed at each step.</summary>
    private async Task<List<PlanEvent>> HistoryAsync(Guid planId, CancellationToken ct)
    {
        var id = planId.ToString();
        var entries = await db.AuditLog.AsNoTracking().Where(a => a.EntityType == "Plan" && a.EntityId == id).OrderBy(a => a.At).ToListAsync(ct);
        var actorIds = entries.Where(e => e.ActorUserId is not null).Select(e => e.ActorUserId!.Value).Distinct().ToList();
        var actors = await db.Users.AsNoTracking().Where(u => actorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);

        var sectionIds = new HashSet<Guid>();
        var parsed = entries.Select(e => (Entry: e, Before: Parse(e.Before), After: Parse(e.After))).ToList();
        foreach (var (_, b, a) in parsed) { sectionIds.UnionWith(b.Sections); sectionIds.UnionWith(a.Sections); }
        var labels = await db.Sections.AsNoTracking().Where(s => sectionIds.Contains(s.Id))
            .Join(db.Courses.AsNoTracking(), s => s.CourseId, c => c.Id, (s, c) => new { s.Id, Label = c.Code + " (" + s.SectionCode + ")" })
            .ToDictionaryAsync(x => x.Id, x => x.Label, ct);
        string Label(Guid g) => labels.GetValueOrDefault(g, "?");

        return parsed.Select(p =>
        {
            var actor = p.Entry.ActorUserId is { } aid && actors.TryGetValue(aid, out var u) ? u : null;
            var versionChanged = p.After.Version != p.Before.Version;
            return new PlanEvent(
                p.Entry.At,
                actor?.DisplayName ?? "",
                actor is null ? "" : RoleNames.Of(actor.Role),
                p.Entry.Action.Replace("plan.", "", StringComparison.Ordinal),
                Enum.TryParse<PlanStatus>(p.After.Status, out var st) ? st : PlanStatus.Draft,
                p.After.Version,
                p.After.Note,
                versionChanged ? p.After.Sections.Except(p.Before.Sections).Select(Label).Order(StringComparer.Ordinal).ToList() : [],
                versionChanged ? p.Before.Sections.Except(p.After.Sections).Select(Label).Order(StringComparer.Ordinal).ToList() : []);
        }).ToList();
    }

    private sealed record SnapshotData(string? Status, int? Version, List<Guid> Sections, string? Note);

    private static SnapshotData Parse(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new SnapshotData(null, null, [], null);
        var d = JsonSerializer.Deserialize<SnapshotData>(json, Json);
        return d is null ? new SnapshotData(null, null, [], null) : d with { Sections = d.Sections ?? [] };
    }

    public async Task<SaveOutcome> AcceptProposalAsync(Guid studentUserId, CancellationToken ct)
    {
        var record = await db.StudentRecords.AsNoTracking().SingleOrDefaultAsync(s => s.UserId == studentUserId, ct);
        var term = await db.Terms.AsNoTracking().FirstOrDefaultAsync(t => t.IsCurrent, ct);
        if (record is null || term is null) return SaveOutcome.NotFound;
        var plan = await db.Plans.Include(p => p.Versions).ThenInclude(v => v.Sections)
            .SingleOrDefaultAsync(p => p.StudentRecordId == record.Id && p.TermId == term.Id, ct);
        if (plan is null) return SaveOutcome.NotFound;
        if (plan.Status != PlanStatus.ChangesProposed) return SaveOutcome.AlreadyWithAdvisor;

        var before = plan.Status;
        plan.SetStatus(PlanStatus.Approved, clock.GetUtcNow());
        var names = await NamesAsync(studentUserId, record.AdvisorUserId, term.Id, ct);
        RecordChange(plan, studentUserId, "plan.accepted", before, plan.Current, null, studentUserId, record.AdvisorUserId,
            "notifications.planAccepted", names);
        await db.SaveChangesAsync(ct);
        return SaveOutcome.Saved;
    }
}
