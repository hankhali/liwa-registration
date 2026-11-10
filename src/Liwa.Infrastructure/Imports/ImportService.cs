using System.Text.Json;
using Liwa.Application.Imports;
using Liwa.Domain.Academic;
using Liwa.Domain.Academic.Prerequisites;
using Liwa.Domain.Activity;
using Liwa.Domain.Users;
using Liwa.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Liwa.Infrastructure.Imports;

/// <summary>
/// Validates and saves CSV files described in docs/import-contract.md. All-or-nothing: every check runs
/// first, and the changes are written in a single SaveChanges only when there are no errors and it is not
/// a dry run. Re-importing the same file changes nothing ("unchanged").
/// </summary>
public sealed partial class ImportService(LiwaDbContext db, TimeProvider clock, ILogger<ImportService> logger) : IImportService
{
    /// <summary>Required and optional columns per file type (lower-case).</summary>
    public static readonly IReadOnlyDictionary<ImportKind, (string[] Required, string[] Optional)> Columns =
        new Dictionary<ImportKind, (string[], string[])>
        {
            [ImportKind.Terms] = (["term_code", "name", "season", "year", "start_date", "end_date"], ["teaching_days", "friday_afternoon", "is_current", "in_progress_prerequisites"]),
            [ImportKind.Courses] = (["course_code", "title", "credits"], ["title_ar", "department", "prerequisites", "offered", "description", "active"]),
            [ImportKind.Programs] = (["program_code", "name", "total_credits"], ["name_ar", "college"]),
            [ImportKind.DegreePlans] = (["program_code", "catalog_year", "group_key", "group_type"], ["group_name", "group_credits", "course_code", "suggested_semester"]),
            [ImportKind.Sections] = (["term_code", "course_code", "section", "capacity"], ["delivery", "instructor", "room", "enrolled", "days", "start_time", "end_time"]),
            [ImportKind.Students] = (["student_id", "display_name", "program_code", "catalog_year", "admit_term"], ["email", "username", "status", "advisor_username", "hold"]),
            [ImportKind.Transcripts] = (["student_id", "course_code"], ["term_code", "credits", "grade", "status", "source"]),
        };

    public async Task<ImportReport> ImportAsync(ImportKind kind, Stream csv, bool dryRun, Guid? actorUserId, CancellationToken ct)
    {
        var report = new ImportReport(kind, dryRun);
        CsvTable table;
        try
        {
            table = CsvTable.Parse(csv);
        }
        catch (CsvFormatException ex)
        {
            report.Error(ex.Line, null, ex.Message);
            return report;
        }

        report.RowsRead = table.Rows.Count;
        if (!CheckColumns(kind, table, report)) return report;

        switch (kind)
        {
            case ImportKind.Terms: await ImportTermsAsync(table, report, ct); break;
            case ImportKind.Courses: await ImportCoursesAsync(table, report, ct); break;
            case ImportKind.Programs: await ImportProgramsAsync(table, report, ct); break;
            case ImportKind.DegreePlans: await ImportDegreePlansAsync(table, report, ct); break;
            case ImportKind.Sections: await ImportSectionsAsync(table, report, ct); break;
            case ImportKind.Students: await ImportStudentsAsync(table, report, ct); break;
            case ImportKind.Transcripts: await ImportTranscriptsAsync(table, report, ct); break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (report.HasErrors || dryRun)
        {
            db.ChangeTracker.Clear();
            return report;
        }

        db.AuditLog.Add(new AuditEntry(Guid.NewGuid(), clock.GetUtcNow(), actorUserId, $"import.{kind.ToString().ToLowerInvariant()}", "Import", null, null,
            JsonSerializer.Serialize(new { report.RowsRead, report.Created, report.Updated, report.Unchanged, report.WarningCount })));
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        report.Saved = true;
        logger.LogInformation("Imported {Kind}: {Created} created, {Updated} updated, {Unchanged} unchanged, {Warnings} warning(s).",
            kind, report.Created, report.Updated, report.Unchanged, report.WarningCount);
        return report;
    }

    private static bool CheckColumns(ImportKind kind, CsvTable table, ImportReport report)
    {
        var (required, optional) = Columns[kind];
        var missing = required.Where(c => !table.Headers.Contains(c)).ToList();
        foreach (var c in missing) report.Error(1, c, $"The column '{c}' is missing. Required columns: {string.Join(", ", required)}.");
        foreach (var extra in table.Headers.Where(h => !required.Contains(h) && !optional.Contains(h)))
            report.Warning(1, extra, $"The column '{extra}' isn't used and will be ignored.");
        return missing.Count == 0;
    }

    private void Count(object entity, bool isNew, ImportReport report)
    {
        if (isNew) { report.Created++; return; }
        var entry = db.Entry(entity);
        entry.DetectChanges();
        if (entry.State == EntityState.Modified) report.Updated++;
        else report.Unchanged++;
    }

    private static string Key(params object?[] parts) => string.Join('|', parts);

    // ---------------------------------------------------------------- Terms

    private async Task ImportTermsAsync(CsvTable table, ImportReport report, CancellationToken ct)
    {
        var existing = await db.Terms.ToDictionaryAsync(t => t.Code, ct);
        var seen = new HashSet<string>();
        var currentInFile = new List<string>();

        foreach (var row in table.Rows)
        {
            var r = new RowReader(row, report);
            var code = r.Required("term_code").ToUpperInvariant();
            var name = r.Required("name");
            var season = r.Enum<TermSeason>("season");
            var year = r.RequiredInt("year", 2000, 2100);
            var start = r.RequiredDate("start_date");
            var end = r.RequiredDate("end_date");
            var days = r.WeekDays("teaching_days", WeekDays.MondayToFriday);
            var friday = r.Bool("friday_afternoon", false);
            var current = r.Bool("is_current", false);
            bool? inProgress = r.Optional("in_progress_prerequisites") is null ? null : r.Bool("in_progress_prerequisites", true);
            if (!r.Ok) continue;

            if (!TermCodes.TryParse(code, out var codeYear, out var codeSeason)) { r.Fail("term_code", $"'{code}' should look like 2027SP (year + SP, SU or FA)."); continue; }
            if (codeYear != year || codeSeason != season) { r.Fail("term_code", $"'{code}' doesn't match {season} {year}."); continue; }
            if (!seen.Add(code)) { r.Fail("term_code", $"{code} appears more than once in this file."); continue; }
            if (end <= start) { r.Fail("end_date", "The term must end after it starts."); continue; }
            if ((days & WeekDays.Sunday) != 0) { r.Fail("teaching_days", "Sunday is a weekend day in the UAE and can't be a teaching day."); continue; }
            if (days == WeekDays.None) { r.Fail("teaching_days", "A term needs at least one teaching day."); continue; }
            if ((days & WeekDays.Saturday) != 0) r.Warn("teaching_days", "Saturday teaching is on for this term — confirm with the university.");
            if (current) currentInFile.Add(code);

            var isNew = !existing.TryGetValue(code, out var term);
            if (isNew)
            {
                term = new Term(Guid.NewGuid(), code, name, season, year, start, end);
                db.Terms.Add(term);
                existing[code] = term;
            }
            else term!.Update(name, season, year, start, end);

            term.SetTeachingDays(days, friday);
            term.SetCurrent(current);
            term.SetInProgressPrerequisites(inProgress);
            Count(term, isNew, report);
        }

        if (currentInFile.Count > 1)
            report.Error(1, "is_current", $"Only one term can be current, but this file marks {string.Join(", ", currentInFile)}.");
        else if (currentInFile.Count == 1)
            foreach (var t in existing.Values.Where(t => t.Code != currentInFile[0] && t.IsCurrent)) t.SetCurrent(false);
    }

    // ---------------------------------------------------------------- Courses

    private async Task ImportCoursesAsync(CsvTable table, ImportReport report, CancellationToken ct)
    {
        var existing = await db.Courses.ToDictionaryAsync(c => c.Code, ct);
        var codesInFile = table.Rows.Select(r => r["course_code"]?.ToUpperInvariant()).Where(Course.IsValidCode).Select(c => c!).ToHashSet();
        var seen = new HashSet<string>();
        var lineOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in table.Rows)
        {
            var r = new RowReader(row, report);
            var code = r.Required("course_code").ToUpperInvariant();
            var title = r.Required("title");
            var credits = r.RequiredInt("credits", Course.MinCredits, Course.MaxCredits);
            var offered = r.Offered("offered", OfferedIn.FallAndSpring);
            var active = r.Bool("active", true);
            var prereqText = r.Optional("prerequisites");
            if (code.Length > 0 && !Course.IsValidCode(code)) r.Fail("course_code", $"'{code}' isn't a course code. Use 2–4 letters + 3 digits, e.g. ITC201 (labs may end in L).");
            if (!r.Ok) continue;
            if (!seen.Add(code)) { r.Fail("course_code", $"{code} appears more than once in this file."); continue; }

            string? canonicalPrereq = null;
            if (prereqText is not null)
            {
                if (!PrerequisiteParser.TryParse(prereqText, out var parsed, out var error)) { r.Fail("prerequisites", $"Can't read '{prereqText}': {error}"); continue; }
                var unknown = parsed!.CourseCodes().Distinct().Where(c => !existing.ContainsKey(c) && !codesInFile.Contains(c)).ToList();
                if (unknown.Count > 0) { r.Fail("prerequisites", $"Refers to {string.Join(", ", unknown)}, which isn't in the catalog."); continue; }
                if (parsed.CourseCodes().Contains(code)) { r.Fail("prerequisites", $"{code} can't be its own prerequisite."); continue; }
                canonicalPrereq = parsed.ToText();
            }

            if (offered == OfferedIn.None) r.Warn("offered", $"{code} isn't offered in any season.");

            var isNew = !existing.TryGetValue(code, out var course);
            if (isNew)
            {
                course = new Course(Guid.NewGuid(), code, title, credits);
                db.Courses.Add(course);
                existing[code] = course;
            }

            course!.SetDetails(title, r.Optional("title_ar"), credits, r.Optional("department"), r.Optional("description"));
            course.SetPrerequisites(canonicalPrereq);
            course.SetOffering(offered);
            course.SetActive(active);
            Count(course, isNew, report);
            lineOf[code] = r.Line;
        }

        // A prerequisite cycle (A needs B, B needs A) would make both courses impossible to take.
        if (!report.HasErrors)
        {
            var rules = existing.Values.ToDictionary(c => c.Code, c => PrerequisiteParser.TryParse(c.Prerequisites, out var p, out _) ? p : null, StringComparer.OrdinalIgnoreCase);
            if (PrerequisiteGraph.FindCycle(rules) is { } cycle)
                report.Error(lineOf.GetValueOrDefault(cycle[0], 1), "prerequisites",
                    $"These prerequisites go round in a circle, so none of these courses could ever be taken: {string.Join(" → ", cycle)}.");
        }
    }

    // ---------------------------------------------------------------- Programs

    private async Task ImportProgramsAsync(CsvTable table, ImportReport report, CancellationToken ct)
    {
        var existing = await db.Programs.ToDictionaryAsync(p => p.Code, ct);
        var seen = new HashSet<string>();
        foreach (var row in table.Rows)
        {
            var r = new RowReader(row, report);
            var code = r.Required("program_code").ToUpperInvariant();
            var name = r.Required("name");
            var total = r.RequiredInt("total_credits", 1, 300);
            if (!r.Ok) continue;
            if (!seen.Add(code)) { r.Fail("program_code", $"{code} appears more than once in this file."); continue; }

            var isNew = !existing.TryGetValue(code, out var program);
            if (isNew)
            {
                program = new AcademicProgram(Guid.NewGuid(), code, name, total);
                db.Programs.Add(program);
                existing[code] = program;
            }

            program!.Update(name, r.Optional("name_ar"), r.Optional("college"), total);
            Count(program, isNew, report);
        }
    }
}
