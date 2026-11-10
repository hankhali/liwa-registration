using Liwa.Application.Academic;
using Liwa.Domain.Academic;
using Liwa.Domain.Users;
using Liwa.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liwa.Infrastructure.Academic;

internal sealed class AcademicQueries(LiwaDbContext db) : IAcademicQueries
{
    public async Task<StudentSummary?> GetStudentSummaryAsync(Guid userId, CancellationToken ct)
    {
        var record = await db.StudentRecords.AsNoTracking()
            .Include(s => s.Transcript).Include(s => s.TransferCredits)
            .SingleOrDefaultAsync(s => s.UserId == userId, ct);
        if (record is null) return null;

        var program = await db.Programs.AsNoTracking().SingleAsync(p => p.Id == record.ProgramId, ct);
        var advisor = record.AdvisorUserId is { } a
            ? await db.Users.AsNoTracking().Where(u => u.Id == a).Select(u => u.DisplayName).SingleOrDefaultAsync(ct)
            : null;
        var summary = AcademicSummary.From(record, await RepeatRuleAsync(ct));
        var term = await CurrentTermAsync(ct);

        return new StudentSummary(record.StudentId, program.Code, program.Name, program.NameAr, record.CatalogYear,
            summary.CreditsEarned, program.TotalCredits, summary.InProgressCredits, summary.Standing.ToString(),
            summary.Gpa, record.Status == AcademicStatus.Probation, advisor, term);
    }

    public async Task<IReadOnlyList<AdviseeSummary>> GetAdviseesAsync(Guid advisorUserId, CancellationToken ct)
    {
        var records = await db.StudentRecords.AsNoTracking()
            .Include(s => s.Transcript).Include(s => s.TransferCredits)
            .Where(s => s.AdvisorUserId == advisorUserId)
            .ToListAsync(ct);
        if (records.Count == 0) return [];

        var userIds = records.Select(r => r.UserId).ToList();
        var names = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var programs = await db.Programs.AsNoTracking().ToDictionaryAsync(p => p.Id, ct);
        var rule = await RepeatRuleAsync(ct);

        return records
            .Select(r =>
            {
                var s = AcademicSummary.From(r, rule);
                var p = programs[r.ProgramId];
                return new AdviseeSummary(r.UserId, r.StudentId, names.GetValueOrDefault(r.UserId, r.StudentId), p.Code, p.Name, p.NameAr,
                    s.CreditsEarned, p.TotalCredits, s.Gpa, r.Status == AcademicStatus.Probation, s.Standing, r.Hold);
            })
            .OrderBy(a => a.DisplayName, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<DataOverview> GetOverviewAsync(CancellationToken ct)
    {
        var current = await db.Terms.AsNoTracking().FirstOrDefaultAsync(t => t.IsCurrent, ct);
        return new DataOverview(
            current is null ? null : new TermInfo(current.Code, current.Name),
            await db.Terms.CountAsync(ct),
            await db.Courses.CountAsync(c => c.IsActive, ct),
            await db.Programs.CountAsync(ct),
            await db.DegreePlans.CountAsync(ct),
            current is null ? 0 : await db.Sections.CountAsync(s => s.TermId == current.Id, ct),
            await db.StudentRecords.CountAsync(ct),
            await db.Users.CountAsync(u => u.Role == UserRole.Advisor && u.IsActive, ct),
            await db.AuditLog.Where(a => a.Action.StartsWith("import.")).MaxAsync(a => (DateTimeOffset?)a.At, ct));
    }

    private async Task<RepeatGradeRule> RepeatRuleAsync(CancellationToken ct) =>
        await db.CreditPolicies.AsNoTracking().Select(p => (RepeatGradeRule?)p.RepeatGradeRule).FirstOrDefaultAsync(ct) ?? RepeatGradeRule.LatestCounts;

    private async Task<TermInfo?> CurrentTermAsync(CancellationToken ct) =>
        await db.Terms.AsNoTracking().Where(t => t.IsCurrent).Select(t => new TermInfo(t.Code, t.Name)).FirstOrDefaultAsync(ct);
}
