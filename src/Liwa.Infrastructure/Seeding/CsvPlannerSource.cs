using System.Reflection;
using Liwa.Application.Imports;
using Liwa.Domain.Academic;
using Liwa.Domain.Academic.Prerequisites;
using Liwa.Domain.Planning;
using Liwa.Infrastructure.Academic;

namespace Liwa.Infrastructure.Seeding;

/// <summary>
/// Builds the planner's input straight from the demo CSV files (the import-contract format), without a database.
/// Used by the planner tests so they run on the real seeded programs, and to prove the planner gives the same
/// answer whether data comes from CSV or from the database (brief §5). Reuses the import's own row reader and the
/// same student-facts reduction as the database path.
/// </summary>
public sealed class CsvPlannerSource
{
    private readonly Dictionary<string, Course> _courses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (int Total, List<PlannerRequirementGroup> Groups)> _plans = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (StudentRecord Record, string Program)> _students = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, string> _sectionLabels = [];
    private readonly List<PlannerSection> _sections = [];
    private PlannerTerm _term = new("2027SP", TermSeason.Spring, 2027);

    private CsvPlannerSource() { }

    /// <summary>The demo files embedded in Liwa.Infrastructure (Seeding/Demo/*.csv).</summary>
    public static CsvPlannerSource FromDemoFiles()
    {
        var asm = typeof(CsvPlannerSource).Assembly;
        return Load(file => Open(asm, file));
    }

    private static Stream Open(Assembly asm, string file) =>
        asm.GetManifestResourceStream($"Liwa.Infrastructure.Seeding.Demo.{file}") ?? throw new FileNotFoundException(file);

    public static CsvPlannerSource Load(Func<string, Stream> open)
    {
        var s = new CsvPlannerSource();
        CsvTable Table(string f)
        {
            using var stream = open(f);
            return CsvTable.Parse(stream);
        }

        RowReader Reader(CsvRow row, ImportKind kind) => new(row, new ImportReport(kind, dryRun: true));

        foreach (var row in Table("terms.csv").Rows)
        {
            var r = Reader(row, ImportKind.Terms);
            if (!r.Bool("is_current", false)) continue;
            var inProgress = r.Optional("in_progress_prerequisites") is null ? (bool?)null : r.Bool("in_progress_prerequisites", true);
            s._term = new PlannerTerm(r.Required("term_code").ToUpperInvariant(), r.Enum<TermSeason>("season"), r.RequiredInt("year", 2000, 2100), inProgress);
        }

        foreach (var row in Table("courses.csv").Rows)
        {
            var r = Reader(row, ImportKind.Courses);
            var code = r.Required("course_code").ToUpperInvariant();
            var course = new Course(DeterministicId("course", code), code, r.Required("title"), r.RequiredInt("credits", Course.MinCredits, Course.MaxCredits));
            course.SetPrerequisites(PrerequisiteParser.TryParse(r.Optional("prerequisites"), out var p, out _) ? p?.ToText() : null);
            course.SetOffering(r.Offered("offered", OfferedIn.FallAndSpring));
            s._courses[code] = course;
        }

        var totals = Table("programs.csv").Rows.Select(row => Reader(row, ImportKind.Programs))
            .ToDictionary(r => r.Required("program_code").ToUpperInvariant(), r => r.RequiredInt("total_credits", 1, 300), StringComparer.OrdinalIgnoreCase);

        foreach (var plan in Table("degree-plans.csv").Rows.Select(row => Reader(row, ImportKind.DegreePlans))
                     .GroupBy(r => $"{r.Required("program_code").ToUpperInvariant()}|{r.RequiredInt("catalog_year", 2000, 2100)}"))
        {
            var groups = plan.GroupBy(r => r.Required("group_key")).Select(g =>
            {
                var first = g.First();
                var kind = first.Enum<RequirementKind>("group_type");
                return new PlannerRequirementGroup(g.Key, first.Optional("group_name") ?? g.Key, kind, first.OptionalInt("group_credits", 0, 300) ?? 0,
                    g.Where(x => x.Optional("course_code") is not null)
                        .Select(x => new PlannerRequirementCourse(x.Required("course_code").ToUpperInvariant(), x.OptionalInt("suggested_semester", 1, 20))).ToList());
            }).ToList();
            var program = plan.Key.Split('|')[0];
            s._plans[plan.Key] = (totals[program], groups);
        }

        foreach (var section in Table("sections.csv").Rows.Select(row => (Row: row, R: Reader(row, ImportKind.Sections)))
                     .Where(x => string.Equals(x.R.Required("term_code"), s._term.Code, StringComparison.OrdinalIgnoreCase))
                     .GroupBy(x => $"{x.R.Required("course_code").ToUpperInvariant()}-{x.R.Required("section").ToUpperInvariant()}"))
        {
            var r = section.First().R;
            var code = r.Required("course_code").ToUpperInvariant();
            var meetings = section.SelectMany(x =>
            {
                var days = x.R.Days("days");
                var start = x.R.OptionalTime("start_time");
                var end = x.R.OptionalTime("end_time");
                return start is null || end is null ? [] : days.Select(d => new MeetingTime(d, start.Value, end.Value));
            }).OrderBy(m => m.Day == DayOfWeek.Sunday ? 7 : (int)m.Day).ThenBy(m => m.Start).ToList();
            var capacity = r.RequiredInt("capacity", 0, 1000);
            var enrolled = r.OptionalInt("enrolled", 0, 1000) ?? 0;
            var id = DeterministicId("section", section.Key);
            s._sections.Add(new PlannerSection(id, code, r.Required("section").ToUpperInvariant(), r.Enum<DeliveryMode>("delivery", DeliveryMode.OnCampus),
                meetings, Math.Max(0, capacity - enrolled), capacity));
            s._sectionLabels[id] = section.Key;
        }

        foreach (var row in Table("students.csv").Rows)
        {
            var r = Reader(row, ImportKind.Students);
            var id = r.Required("student_id");
            var record = new StudentRecord(DeterministicId("student", id), Guid.Empty, id, Guid.Empty, r.RequiredInt("catalog_year", 2000, 2100), r.Required("admit_term"));
            record.Update(Guid.Empty, record.CatalogYear, record.AdmitTermCode, r.Enum<AcademicStatus>("status", AcademicStatus.Good), null);
            record.SetHold(r.Optional("hold"));
            s._students[id] = (record, $"{r.Required("program_code").ToUpperInvariant()}|{record.CatalogYear}");
        }

        foreach (var student in Table("transcripts.csv").Rows.Select(row => Reader(row, ImportKind.Transcripts)).GroupBy(r => r.Required("student_id")))
        {
            var attempts = new List<(Guid, string, int, string?, TranscriptStatus)>();
            var transfers = new List<(Guid, int, string?)>();
            foreach (var r in student)
            {
                var course = s._courses[r.Required("course_code").ToUpperInvariant()];
                var credits = r.OptionalInt("credits", 0, Course.MaxCredits) ?? course.Credits;
                var grade = r.Optional("grade")?.ToUpperInvariant();
                var status = (r.Optional("status") ?? (grade is null ? "InProgress" : "Completed")).ToUpperInvariant();
                if (status == "TRANSFER") transfers.Add((course.Id, credits, r.Optional("source")));
                else
                    attempts.Add((course.Id, r.Required("term_code").ToUpperInvariant(), credits, status == "COMPLETED" ? grade : null,
                        status switch { "INPROGRESS" => TranscriptStatus.InProgress, "WITHDRAWN" => TranscriptStatus.Withdrawn, _ => TranscriptStatus.Completed }));
            }

            s._students[student.Key].Record.ReplaceHistory(attempts, transfers);
        }

        return s;
    }

    public IReadOnlyCollection<string> StudentIds => _students.Keys;

    public IReadOnlyCollection<string> ProgramKeys => _plans.Keys;

    public PlannerTerm Term => _term;

    /// <summary>"ITC201-01": a section's stable label, the same in CSV and database.</summary>
    public string SectionLabel(Guid sectionId) => _sectionLabels.GetValueOrDefault(sectionId, sectionId.ToString());

    /// <summary>The planner input for a seeded student, exactly as the app would build it.</summary>
    public PlannerInput InputFor(string studentId, CreditPolicy policy, DateOnly today, Availability? availability = null, PlannerPreferences? preferences = null)
    {
        var (record, planKey) = _students[studentId];
        var (total, groups) = _plans[planKey];
        var codeById = _courses.Values.ToDictionary(c => c.Id, c => c.Code);
        var facts = PlannerFacts.StudentFacts(record, policy.RepeatGradeRule, codeById, total);
        return Build(facts, groups, policy, today, availability, preferences);
    }

    /// <summary>A brand-new student in a program (nothing passed), starting this term.</summary>
    public PlannerInput FreshStudent(string programKey, CreditPolicy policy, DateOnly today)
    {
        var (total, groups) = _plans[programKey];
        var facts = new PlannerStudent(new Dictionary<string, string>(), new HashSet<string>(), new HashSet<string>(), 0, null, false, total, new HashSet<string>());
        return Build(facts, groups, policy, today, null, null);
    }

    private PlannerInput Build(PlannerStudent facts, List<PlannerRequirementGroup> groups, CreditPolicy policy, DateOnly today, Availability? availability, PlannerPreferences? preferences) =>
        new(today, _term, facts,
            _courses.Values.Select(c => new PlannerCourse(c.Code, c.Credits, PrerequisiteParser.TryParse(c.Prerequisites, out var p, out _) ? p : null, c.Offered)).ToList(),
            groups, _sections, policy, availability, preferences ?? PlannerPreferences.None);

    /// <summary>Stable ids so CSV runs are reproducible.</summary>
    private static Guid DeterministicId(string kind, string key)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{kind}:{key.ToUpperInvariant()}"));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
