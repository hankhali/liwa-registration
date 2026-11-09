using Liwa.Application.Planning;
using Liwa.Domain.Academic;
using Liwa.Domain.Academic.Prerequisites;
using Liwa.Domain.Planning;
using Liwa.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liwa.Infrastructure.Academic;

/// <summary>
/// Builds the planner's input from the database. All I/O, including reading today's date, happens here,
/// so the planner itself stays pure.
/// </summary>
internal sealed class RecommendationService(LiwaDbContext db, TimeProvider clock) : IRecommendationService
{
    public async Task<PlannerResult?> RecommendAsync(Guid studentUserId, Availability? availability, PlannerPreferences preferences, CancellationToken ct)
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

        var input = new PlannerInput(
            DateOnly.FromDateTime(clock.GetLocalNow().DateTime),
            new PlannerTerm(term.Code, term.Season, term.Year),
            StudentFacts(record, policy.RepeatGradeRule, codeById, program.TotalCredits),
            courses.Select(c => new PlannerCourse(c.Code, c.Credits, ParseOrNull(c.Prerequisites), c.Offered)).ToList(),
            plan.Groups.OrderBy(g => g.SortOrder).Select(g => new PlannerRequirementGroup(g.Key, g.Name, g.Kind, g.CreditsRequired,
                g.Courses.Select(c => new PlannerRequirementCourse(codeById[c.CourseId], c.SuggestedSemester)).ToList())).ToList(),
            sections.Select(s => new PlannerSection(s.Id, codeById[s.CourseId], s.SectionCode, s.Delivery, s.Meetings.ToList(), s.SeatsLeft)).ToList(),
            policy,
            availability,
            preferences);

        return Planner.Plan(input);
    }

    /// <summary>Reduces a transcript to the facts the planner uses, applying the repeat-grade rule.</summary>
    internal static PlannerStudent StudentFacts(StudentRecord record, RepeatGradeRule rule, IReadOnlyDictionary<Guid, string> codeById, int totalCredits)
    {
        var counting = record.Transcript
            .Where(t => t.Status == TranscriptStatus.Completed && t.Grade is not null)
            .GroupBy(t => t.CourseId)
            .Select(g => rule == RepeatGradeRule.LatestCounts
                ? g.OrderBy(t => TermCodes.SortKey(t.TermCode)).Last()
                : g.OrderBy(t => Grades.PointsFor(t.Grade!) ?? -1m).ThenBy(t => TermCodes.SortKey(t.TermCode)).Last())
            .ToList();

        var passed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in counting.Where(t => Grades.IsPassing(t.Grade!))) passed[codeById[t.CourseId]] = t.Grade!;
        foreach (var t in record.TransferCredits) passed.TryAdd(codeById[t.CourseId], "TR");

        var failed = counting.Where(t => !Grades.IsPassing(t.Grade!)).Select(t => codeById[t.CourseId]).Where(c => !passed.ContainsKey(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var inProgress = record.Transcript.Where(t => t.Status == TranscriptStatus.InProgress).Select(t => codeById[t.CourseId])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var summary = AcademicSummary.From(record, rule);

        return new PlannerStudent(passed, failed, inProgress, summary.CreditsEarned, summary.Gpa, record.Status == AcademicStatus.Probation,
            totalCredits, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private static Prerequisite? ParseOrNull(string? text) => PrerequisiteParser.TryParse(text, out var p, out _) ? p : null;
}
