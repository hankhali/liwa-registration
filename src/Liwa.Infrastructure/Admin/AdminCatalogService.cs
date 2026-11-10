using System.Globalization;
using System.IO.Compression;
using System.Text;
using Liwa.Application.Admin;
using Liwa.Application.Imports;
using Liwa.Domain.Academic;
using Liwa.Domain.Academic.Prerequisites;
using Liwa.Domain.Activity;
using Liwa.Infrastructure.Imports;
using Liwa.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liwa.Infrastructure.Admin;

/// <summary>
/// Catalog screens and exports (brief §11). Everything is expressed in the import-file format: a catalog table is the
/// export file; an edited row becomes a one-row file sent through the importer; an export imported again is unchanged.
/// </summary>
internal sealed class AdminCatalogService(LiwaDbContext db, IImportService imports, TimeProvider clock) : IAdminCatalogService
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static readonly ImportKind[] Order =
        [ImportKind.Terms, ImportKind.Courses, ImportKind.Programs, ImportKind.DegreePlans, ImportKind.Sections, ImportKind.Students, ImportKind.Transcripts];

    public static readonly IReadOnlyDictionary<ImportKind, string[]> ExportColumns = new Dictionary<ImportKind, string[]>
    {
        [ImportKind.Terms] = ["term_code", "name", "season", "year", "start_date", "end_date", "teaching_days", "friday_afternoon", "is_current", "in_progress_prerequisites"],
        [ImportKind.Courses] = ["course_code", "title", "title_ar", "credits", "department", "prerequisites", "offered", "description", "active"],
        [ImportKind.Programs] = ["program_code", "name", "name_ar", "college", "total_credits"],
        [ImportKind.DegreePlans] = ["program_code", "catalog_year", "group_key", "group_name", "group_type", "group_credits", "course_code", "suggested_semester"],
        [ImportKind.Sections] = ["term_code", "course_code", "section", "delivery", "instructor", "room", "capacity", "enrolled", "days", "start_time", "end_time"],
        [ImportKind.Students] = ["student_id", "display_name", "email", "username", "program_code", "catalog_year", "admit_term", "status", "advisor_username", "hold"],
        [ImportKind.Transcripts] = ["student_id", "term_code", "course_code", "credits", "grade", "status", "source"],
    };

    // ------------------------------------------------------------------ rows

    public async Task<CatalogRows> RowsAsync(ImportKind kind, CancellationToken ct)
    {
        var columns = ExportColumns[kind];
        var rows = await ExportRowsAsync(kind, ct);
        return new CatalogRows(ImportKinds.Slug(kind), columns,
            rows.Select(r => (IReadOnlyDictionary<string, string?>)columns.Select((c, i) => (c, r[i])).ToDictionary(x => x.c, x => x.Item2)).ToList());
    }

    public async Task<ImportReport> EditAsync(ImportKind kind, RowEditRequest request, Guid actorUserId, CancellationToken ct)
    {
        var (required, optional) = ImportService.Columns[kind];
        var known = required.Concat(optional).ToList();
        var used = known.Where(c => request.Rows.Any(r => r.ContainsKey(c))).ToList();
        foreach (var c in required.Where(c => !used.Contains(c))) used.Add(c);
        var csv = Csv(used, request.Rows.Select(r => used.Select(c => r.GetValueOrDefault(c)).ToArray()));
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        return await imports.ImportAsync(kind, stream, request.DryRun, actorUserId, ct, ImportSource.Edit, $"edit-{ImportKinds.Slug(kind)}.csv");
    }

    // ------------------------------------------------------------------ exports

    public async Task<string> ExportCsvAsync(ImportKind kind, CancellationToken ct) => Csv(ExportColumns[kind], await ExportRowsAsync(kind, ct));

    public async Task<byte[]> ExportZipAsync(CancellationToken ct)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (kind, i) in Order.Select((k, i) => (k, i)))
            {
                var entry = zip.CreateEntry($"{i + 1}-{ImportKinds.Slug(kind)}.csv", CompressionLevel.Optimal);
                await using var w = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                await w.WriteAsync(await ExportCsvAsync(kind, ct));
            }

        return ms.ToArray();
    }

    private async Task<List<string?[]>> ExportRowsAsync(ImportKind kind, CancellationToken ct)
    {
        switch (kind)
        {
            case ImportKind.Terms:
                return (await db.Terms.AsNoTracking().ToListAsync(ct)).OrderBy(t => TermCodes.SortKey(t.Code))
                    .Select(t => new string?[]
                    {
                        t.Code, t.Name, t.Season.ToString(), t.Year.ToString(Inv), t.StartDate.ToString("yyyy-MM-dd", Inv), t.EndDate.ToString("yyyy-MM-dd", Inv),
                        Days(t.TeachingDays), YesNo(t.AllowFridayAfternoon), YesNo(t.IsCurrent),
                        t.AllowInProgressPrerequisites is { } b ? YesNo(b) : null,
                    }).ToList();

            case ImportKind.Courses:
                return (await db.Courses.AsNoTracking().OrderBy(c => c.Code).ToListAsync(ct))
                    .Select(c => new string?[] { c.Code, c.Title, c.TitleAr, c.Credits.ToString(Inv), c.Department, c.Prerequisites, Offered(c.Offered), c.Description, YesNo(c.IsActive) })
                    .ToList();

            case ImportKind.Programs:
                return (await db.Programs.AsNoTracking().OrderBy(p => p.Code).ToListAsync(ct))
                    .Select(p => new string?[] { p.Code, p.Name, p.NameAr, p.College, p.TotalCredits.ToString(Inv) }).ToList();

            case ImportKind.DegreePlans:
            {
                var programs = await db.Programs.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Code, ct);
                var codes = await db.Courses.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Code, ct);
                var plans = await db.DegreePlans.AsNoTracking().Include(d => d.Groups).ThenInclude(g => g.Courses).ToListAsync(ct);
                return plans.OrderBy(p => programs[p.ProgramId], StringComparer.Ordinal).ThenBy(p => p.CatalogYear)
                    .SelectMany(p => p.Groups.OrderBy(g => g.SortOrder).SelectMany(g =>
                    {
                        string?[] Row(string? course, int? sem) =>
                        [
                            programs[p.ProgramId], p.CatalogYear.ToString(Inv), g.Key, g.Name, g.Kind.ToString(),
                            g.Kind == RequirementKind.AllOf ? null : g.CreditsRequired.ToString(Inv), course, sem?.ToString(Inv),
                        ];
                        return g.Courses.Count == 0
                            ? [Row(null, null)]
                            : g.Courses.Select(c => Row(codes[c.CourseId], c.SuggestedSemester));
                    })).ToList();
            }

            case ImportKind.Sections:
            {
                var terms = await db.Terms.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Code, ct);
                var codes = await db.Courses.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Code, ct);
                var sections = await db.Sections.AsNoTracking().ToListAsync(ct);
                return sections.OrderBy(s => TermCodes.SortKey(terms[s.TermId])).ThenBy(s => codes[s.CourseId], StringComparer.Ordinal).ThenBy(s => s.SectionCode, StringComparer.Ordinal)
                    .SelectMany(s =>
                    {
                        var head = new string?[] { terms[s.TermId], codes[s.CourseId], s.SectionCode, s.Delivery.ToString(), s.Instructor, s.Room, s.Capacity.ToString(Inv), s.Enrolled.ToString(Inv) };
                        if (s.Meetings.Count == 0) return [[.. head, null, null, null]];
                        // One row per meeting time; rows of the same section add meetings (import contract).
                        return s.Meetings.GroupBy(m => (m.Start, m.End)).Select(g => (string?[])
                            [.. head, string.Join(';', g.Select(m => m.Day.ToString()[..3])), g.Key.Start.ToString("HH:mm", Inv), g.Key.End.ToString("HH:mm", Inv)]);
                    }).ToList();
            }

            case ImportKind.Students:
            {
                var users = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, ct);
                var programs = await db.Programs.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Code, ct);
                return (await db.StudentRecords.AsNoTracking().OrderBy(r => r.StudentId).ToListAsync(ct))
                    .Select(r => new string?[]
                    {
                        r.StudentId, users[r.UserId].DisplayName, users[r.UserId].Email, users[r.UserId].Username, programs[r.ProgramId], r.CatalogYear.ToString(Inv),
                        r.AdmitTermCode, r.Status.ToString(), r.AdvisorUserId is { } a && users.TryGetValue(a, out var adv) ? adv.Username : null, r.Hold,
                    }).ToList();
            }

            case ImportKind.Transcripts:
            {
                var codes = await db.Courses.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Code, ct);
                var records = await db.StudentRecords.AsNoTracking().Include(r => r.Transcript).Include(r => r.TransferCredits).OrderBy(r => r.StudentId).ToListAsync(ct);
                return records.SelectMany(r =>
                    r.Transcript.OrderBy(t => TermCodes.SortKey(t.TermCode)).ThenBy(t => codes[t.CourseId], StringComparer.Ordinal)
                        .Select(t => new string?[]
                        {
                            r.StudentId, t.TermCode, codes[t.CourseId], t.Credits.ToString(Inv), t.Status == TranscriptStatus.Completed ? t.Grade : null, t.Status.ToString(), null,
                        })
                        .Concat(r.TransferCredits.OrderBy(t => codes[t.CourseId], StringComparer.Ordinal)
                            .Select(t => new string?[] { r.StudentId, null, codes[t.CourseId], t.Credits.ToString(Inv), null, "Transfer", t.Source })))
                    .ToList();
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private static string YesNo(bool b) => b ? "yes" : "no";

    private static string Days(WeekDays d) =>
        string.Join(';', new[] { WeekDays.Monday, WeekDays.Tuesday, WeekDays.Wednesday, WeekDays.Thursday, WeekDays.Friday, WeekDays.Saturday, WeekDays.Sunday }
            .Where(x => (d & x) != 0).Select(x => x.ToString()[..3]));

    private static string Offered(OfferedIn o) =>
        o == OfferedIn.None ? "None" : string.Join(';', new[] { OfferedIn.Fall, OfferedIn.Spring, OfferedIn.Summer }.Where(x => (o & x) != 0));

    public static string Csv(IReadOnlyList<string> columns, IEnumerable<string?[]> rows)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(',', columns)).Append('\n');
        foreach (var r in rows) sb.Append(string.Join(',', r.Select(Escape))).Append('\n');
        return sb.ToString();
    }

    private static string Escape(string? v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        return v.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{v.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : v;
    }

    // ------------------------------------------------------------------ prerequisite editor

    public async Task<PrerequisiteCheckResult> CheckPrerequisiteAsync(PrerequisiteCheckRequest request, CancellationToken ct)
    {
        var code = (request.CourseCode ?? "").Trim().ToUpperInvariant();
        var courses = await db.Courses.AsNoTracking().ToListAsync(ct);
        if (!PrerequisiteParser.TryParse(request.Expression, out var rule, out var error))
            return new PrerequisiteCheckResult(false, null, [$"Can't read this rule: {error}"]);
        if (rule is null) return new PrerequisiteCheckResult(true, null, []);

        var problems = new List<string>();
        var known = courses.Select(c => c.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = rule.CourseCodes().Distinct(StringComparer.OrdinalIgnoreCase).Where(c => !known.Contains(c)).ToList();
        if (unknown.Count > 0) problems.Add($"Refers to {string.Join(", ", unknown)}, which isn't in the catalog.");
        if (rule.CourseCodes().Contains(code, StringComparer.OrdinalIgnoreCase)) problems.Add($"{code} can't be its own prerequisite.");

        if (problems.Count == 0)
        {
            var rules = courses.ToDictionary(c => c.Code, c => PrerequisiteParser.TryParse(c.Prerequisites, out var p, out _) ? p : null, StringComparer.OrdinalIgnoreCase);
            rules[code] = rule;
            if (PrerequisiteGraph.FindCycle(rules) is { } cycle)
                problems.Add($"These prerequisites go round in a circle, so none of these courses could ever be taken: {string.Join(" → ", cycle)}.");
        }

        return new PrerequisiteCheckResult(problems.Count == 0, rule.ToText(), problems);
    }

    // ------------------------------------------------------------------ removal

    public async Task<RemovalResult> RemoveDegreePlanAsync(string programCode, int catalogYear, Guid actorUserId, CancellationToken ct)
    {
        var code = (programCode ?? "").Trim().ToUpperInvariant();
        var program = await db.Programs.SingleOrDefaultAsync(p => p.Code == code, ct);
        var plan = program is null ? null : await db.DegreePlans.Include(d => d.Groups).ThenInclude(g => g.Courses)
            .SingleOrDefaultAsync(d => d.ProgramId == program.Id && d.CatalogYear == catalogYear, ct);
        if (plan is null) return new RemovalResult(RemovalOutcome.NotFound, 0, null);

        var assigned = await db.StudentRecords.CountAsync(r => r.ProgramId == program!.Id && r.CatalogYear == catalogYear, ct);
        if (assigned > 0) return new RemovalResult(RemovalOutcome.StillAssigned, assigned, null);

        var removed = plan.Groups.Sum(g => Math.Max(1, g.Courses.Count));
        db.DegreePlans.Remove(plan);
        var number = (await db.CatalogVersions.MaxAsync(v => (int?)v.Number, ct) ?? 0) + 1;
        var now = clock.GetUtcNow();
        db.CatalogVersions.Add(new CatalogVersion(Guid.NewGuid(), number, ImportSource.Removal, "degree-plans", null, now, actorUserId, 0, 0, 0, 0, removed));
        db.AuditLog.Add(new AuditEntry(Guid.NewGuid(), now, actorUserId, "catalog.degreePlanRemoved", "DegreePlan", plan.Id.ToString(),
            System.Text.Json.JsonSerializer.Serialize(new { program = program!.Code, catalogYear }), null));
        await db.SaveChangesAsync(ct);
        return new RemovalResult(RemovalOutcome.Removed, 0, number);
    }

    // ------------------------------------------------------------------ versions

    public async Task<IReadOnlyList<CatalogVersionView>> VersionsAsync(int take, CancellationToken ct)
    {
        var versions = await db.CatalogVersions.AsNoTracking().OrderByDescending(v => v.Number).Take(Math.Clamp(take, 1, 500)).ToListAsync(ct);
        var ids = versions.Where(v => v.ActorUserId is not null).Select(v => v.ActorUserId!.Value).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        return versions.Select(v => new CatalogVersionView(v.Number, v.Source.ToString(), v.Kind, v.FileName, v.At,
            v.ActorUserId is { } a ? names.GetValueOrDefault(a) : null, v.RowsRead, v.Created, v.Updated, v.Unchanged, v.Removed)).ToList();
    }
}
