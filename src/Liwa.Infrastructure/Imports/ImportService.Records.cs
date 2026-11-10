using System.Text.RegularExpressions;
using Liwa.Application.Imports;
using Liwa.Domain.Academic;
using Liwa.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Liwa.Infrastructure.Imports;

public sealed partial class ImportService
{
    // ---------------------------------------------------------------- Degree plans

    private sealed record GroupDraft(string Key, string Name, RequirementKind Kind, int Credits, int Order, int Line, List<(Guid CourseId, string Code, int? Semester)> Courses);

    private async Task ImportDegreePlansAsync(CsvTable table, ImportReport report, CancellationToken ct)
    {
        var programs = await db.Programs.ToDictionaryAsync(p => p.Code, ct);
        var courses = await db.Courses.ToDictionaryAsync(c => c.Code, ct);
        var creditsById = courses.Values.ToDictionary(c => c.Id, c => c.Credits);
        var existing = await db.DegreePlans.Include(d => d.Groups).ThenInclude(g => g.Courses).ToListAsync(ct);

        var planRows = table.Rows.GroupBy(r => Key(r["program_code"]?.ToUpperInvariant(), r["catalog_year"]));
        foreach (var plan in planRows)
        {
            var errorsBefore = report.ErrorCount;
            var first = new RowReader(plan.First(), report);
            var programCode = first.Required("program_code").ToUpperInvariant();
            var catalogYear = first.RequiredInt("catalog_year", 2000, 2100);
            if (!first.Ok) continue;
            if (!programs.TryGetValue(programCode, out var program))
            {
                first.Fail("program_code", $"Program {programCode} doesn't exist. Import programs first.");
                continue;
            }

            var groups = new Dictionary<string, GroupDraft>();
            var coursesInPlan = new Dictionary<Guid, string>();
            foreach (var row in plan)
            {
                var r = new RowReader(row, report);
                var key = r.Required("group_key").ToUpperInvariant();
                var kind = r.Enum<RequirementKind>("group_type");
                var credits = r.OptionalInt("group_credits", 0, 300) ?? 0;
                var name = r.Optional("group_name") ?? key;
                var semester = r.OptionalInt("suggested_semester", 1, 12);
                var code = r.Optional("course_code")?.ToUpperInvariant();
                if (!r.Ok) continue;

                if (!groups.TryGetValue(key, out var group))
                {
                    if (kind != RequirementKind.AllOf && credits == 0) { r.Fail("group_credits", $"Group {key} needs group_credits (how many credits to choose)."); continue; }
                    group = new GroupDraft(key, name, kind, credits, groups.Count + 1, row.Line, []);
                    groups[key] = group;
                }
                else if (group.Kind != kind || group.Credits != credits || group.Name != name)
                {
                    r.Fail("group_key", $"Rows for group {key} disagree on group_name, group_type or group_credits (see line {group.Line}).");
                    continue;
                }

                if (code is null)
                {
                    if (kind != RequirementKind.AnyCourse) r.Fail("course_code", $"Group {key} ({kind}) needs a course_code on every row.");
                    continue;
                }

                if (!courses.TryGetValue(code, out var course)) { r.Fail("course_code", $"{code} isn't in the catalog. Import courses first."); continue; }
                if (coursesInPlan.TryGetValue(course.Id, out var otherGroup))
                {
                    if (otherGroup == key) { r.Fail("course_code", $"{code} is listed twice in group {key}."); continue; }
                    r.Warn("course_code", $"{code} is in both {otherGroup} and {key}; it will count toward the first group it fits.");
                }
                else coursesInPlan[course.Id] = key;

                group.Courses.Add((course.Id, code, semester));
            }

            foreach (var g in groups.Values.Where(g => g.Kind == RequirementKind.ChooseCredits))
            {
                var available = g.Courses.Sum(c => creditsById[c.CourseId]);
                if (available < g.Credits)
                    report.Error(g.Line, "group_credits", $"Group {g.Key} asks for {g.Credits} credits but its courses only add up to {available}.");
            }

            if (report.ErrorCount > errorsBefore) continue;

            var planned = groups.Values.Sum(g => g.Kind == RequirementKind.AllOf ? g.Courses.Sum(c => creditsById[c.CourseId]) : g.Credits);
            if (planned != program.TotalCredits)
                report.Warning(plan.First().Line, null, $"{programCode} {catalogYear} adds up to {planned} credits, but the program needs {program.TotalCredits}.");

            var existingPlan = existing.FirstOrDefault(d => d.ProgramId == program.Id && d.CatalogYear == catalogYear);
            var newSignature = string.Join(';', groups.Values.OrderBy(g => g.Order)
                .Select(g => Key(g.Key, g.Name, g.Kind, g.Credits, g.Order, string.Join(',', g.Courses.Select(c => Key(c.CourseId, c.Semester))))));
            if (existingPlan is not null && Signature(existingPlan) == newSignature)
            {
                report.Unchanged++;
                continue;
            }

            var isNew = existingPlan is null;
            var target = existingPlan ?? new DegreePlan(Guid.NewGuid(), program.Id, catalogYear);
            if (isNew) db.DegreePlans.Add(target);
            target.ClearGroups();
            foreach (var g in groups.Values.OrderBy(g => g.Order))
            {
                var group = target.AddGroup(g.Key, g.Name, g.Kind, g.Credits, g.Order);
                foreach (var c in g.Courses) group.AddCourse(c.CourseId, c.Semester);
            }

            if (isNew) report.Created++;
            else report.Updated++;
        }
    }

    private static string Signature(DegreePlan plan) =>
        string.Join(';', plan.Groups.OrderBy(g => g.SortOrder)
            .Select(g => Key(g.Key, g.Name, g.Kind, g.CreditsRequired, g.SortOrder, string.Join(',', g.Courses.Select(c => Key(c.CourseId, c.SuggestedSemester))))));

    // ---------------------------------------------------------------- Sections

    private async Task ImportSectionsAsync(CsvTable table, ImportReport report, CancellationToken ct)
    {
        var terms = await db.Terms.ToDictionaryAsync(t => t.Code, ct);
        var courses = await db.Courses.ToDictionaryAsync(c => c.Code, ct);
        var termCodes = table.Rows.Select(r => r["term_code"]?.ToUpperInvariant()).Where(c => c is not null).Distinct().ToList();
        var termIds = terms.Values.Where(t => termCodes.Contains(t.Code)).Select(t => t.Id).ToList();
        var existing = (await db.Sections.Where(s => termIds.Contains(s.TermId)).ToListAsync(ct))
            .ToDictionary(s => Key(s.TermId, s.CourseId, s.SectionCode));

        var groups = table.Rows.GroupBy(r => Key(r["term_code"]?.ToUpperInvariant(), r["course_code"]?.ToUpperInvariant(), r["section"]?.ToUpperInvariant()));
        foreach (var rows in groups)
        {
            var errorsBefore = report.ErrorCount;
            var r = new RowReader(rows.First(), report);
            var termCode = r.Required("term_code").ToUpperInvariant();
            var courseCode = r.Required("course_code").ToUpperInvariant();
            var sectionCode = r.Required("section").ToUpperInvariant();
            var delivery = r.Enum<DeliveryMode>("delivery", DeliveryMode.OnCampus);
            var capacity = r.RequiredInt("capacity", 0, 1000);
            var enrolled = r.OptionalInt("enrolled", 0, 1000) ?? 0;
            if (!r.Ok) continue;
            if (!terms.TryGetValue(termCode, out var term)) { r.Fail("term_code", $"Term {termCode} doesn't exist. Import terms first."); continue; }
            if (!courses.TryGetValue(courseCode, out var course)) { r.Fail("course_code", $"{courseCode} isn't in the catalog. Import courses first."); continue; }
            if (sectionCode.Length > 10) { r.Fail("section", "Section codes can be at most 10 characters."); continue; }

            // Every row of the same section adds meeting times (e.g. a lecture pattern plus a lab slot).
            var meetings = new List<MeetingTime>();
            foreach (var row in rows)
            {
                var m = new RowReader(row, report);
                var days = m.Days("days");
                var start = m.OptionalTime("start_time");
                var end = m.OptionalTime("end_time");
                if (!m.Ok) continue;
                if (days.Count == 0 && start is null && end is null) continue;
                if (days.Count == 0 || start is null || end is null) { m.Fail("days", "Give days, start_time and end_time together (or leave all three empty for online sections)."); continue; }
                if (end <= start) { m.Fail("end_time", "The class must end after it starts."); continue; }
                meetings.AddRange(days.Select(d => new MeetingTime(d, start.Value, end.Value)));
            }

            if (report.ErrorCount > errorsBefore) continue;

            foreach (var problem in MeetingRules.Validate(term, delivery, meetings)) r.Fail("days", problem);
            var overlaps = meetings.SelectMany((a, i) => meetings.Skip(i + 1).Where(a.Overlaps)).Any();
            if (overlaps) r.Fail("days", $"{courseCode}-{sectionCode} has meeting times that overlap each other.");
            if (report.ErrorCount > errorsBefore) continue;

            if (enrolled > capacity) r.Warn("enrolled", $"{courseCode}-{sectionCode} has more students enrolled ({enrolled}) than seats ({capacity}).");
            if (!course.Offered.Includes(term.Season)) r.Warn("term_code", $"{courseCode} isn't normally offered in {term.Season}, but has a section in {term.Name}.");
            if (!course.IsActive) r.Warn("course_code", $"{courseCode} is marked inactive in the catalog.");

            var key = Key(term.Id, course.Id, sectionCode);
            var isNew = !existing.TryGetValue(key, out var section);
            var meetingsChanged = isNew || !section!.Meetings.SequenceEqual(
                meetings.OrderBy(x => x.Day == DayOfWeek.Sunday ? 7 : (int)x.Day).ThenBy(x => x.Start));
            if (isNew)
            {
                section = new Section(Guid.NewGuid(), term.Id, course.Id, sectionCode);
                db.Sections.Add(section);
                existing[key] = section;
            }

            section!.SetDetails(delivery, r.Optional("instructor"), r.Optional("room"), capacity, enrolled);
            if (meetingsChanged) section.SetMeetings(meetings);

            if (isNew) report.Created++;
            else
            {
                var entry = db.Entry(section!);
                entry.DetectChanges();
                if (meetingsChanged || entry.State == EntityState.Modified) report.Updated++;
                else report.Unchanged++;
            }
        }
    }

    // ---------------------------------------------------------------- Students

    [GeneratedRegex("^[A-Za-z0-9._-]{3,20}$")]
    private static partial Regex StudentIdPattern();

    [GeneratedRegex("^[A-Za-z_]{2,30}$")]
    private static partial Regex HoldPattern();

    private async Task ImportStudentsAsync(CsvTable table, ImportReport report, CancellationToken ct)
    {
        var programs = await db.Programs.ToDictionaryAsync(p => p.Code, ct);
        var users = await db.Users.ToListAsync(ct);
        var usersByName = users.ToDictionary(u => u.Username);
        var usersById = users.ToDictionary(u => u.Id);
        var records = await db.StudentRecords.ToDictionaryAsync(s => s.StudentId, StringComparer.OrdinalIgnoreCase, ct);
        var recordUserIds = records.Values.Select(r => r.UserId).ToHashSet();
        var plans = (await db.DegreePlans.Select(d => new { d.ProgramId, d.CatalogYear }).ToListAsync(ct))
            .Select(p => Key(p.ProgramId, p.CatalogYear)).ToHashSet();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in table.Rows)
        {
            var r = new RowReader(row, report);
            var studentId = r.Required("student_id");
            var name = r.Required("display_name");
            var programCode = r.Required("program_code").ToUpperInvariant();
            var catalogYear = r.RequiredInt("catalog_year", 2000, 2100);
            var admitTerm = r.Required("admit_term").ToUpperInvariant();
            var status = r.Enum<AcademicStatus>("status", AcademicStatus.Good);
            var email = r.Optional("email")?.ToLowerInvariant();
            var username = User.NormalizeUsername(r.Optional("username") ?? studentId);
            var advisorName = r.Optional("advisor_username");
            var hold = r.Optional("hold");
            if (!r.Ok) continue;
            if (hold is not null && !HoldPattern().IsMatch(hold)) { r.Fail("hold", $"'{hold}' isn't a hold type. Use one word, e.g. FINANCE or REGISTRAR, or leave it empty."); continue; }

            if (!StudentIdPattern().IsMatch(studentId)) { r.Fail("student_id", $"'{studentId}' isn't a valid student ID (3–20 letters or digits)."); continue; }
            if (!seen.Add(studentId)) { r.Fail("student_id", $"{studentId} appears more than once in this file."); continue; }
            if (!TermCodes.IsValid(admitTerm)) { r.Fail("admit_term", $"'{admitTerm}' should look like 2025FA."); continue; }
            if (email is not null && !email.Contains('@', StringComparison.Ordinal)) { r.Fail("email", $"'{email}' isn't an email address."); continue; }
            if (!programs.TryGetValue(programCode, out var program)) { r.Fail("program_code", $"Program {programCode} doesn't exist. Import programs first."); continue; }

            Guid? advisorId = null;
            if (advisorName is not null)
            {
                if (!usersByName.TryGetValue(User.NormalizeUsername(advisorName), out var advisor) || advisor.Role != UserRole.Advisor)
                { r.Fail("advisor_username", $"There is no advisor with the username '{advisorName}'."); continue; }
                advisorId = advisor.Id;
            }

            if (!plans.Contains(Key(program.Id, catalogYear)))
                r.Warn("catalog_year", $"There is no {programCode} degree plan for catalog year {catalogYear} yet.");

            var isNew = !records.TryGetValue(studentId, out var record);
            User user;
            if (!isNew) user = usersById[record!.UserId];
            else if (usersByName.TryGetValue(username, out var found))
            {
                if (found.Role != UserRole.Student) { r.Fail("username", $"The username '{username}' belongs to a staff account."); continue; }
                if (recordUserIds.Contains(found.Id)) { r.Fail("username", $"The username '{username}' already belongs to another student."); continue; }
                user = found;
            }
            else
            {
                // New students sign in with their university account (SSO); they get no password here.
                user = new User(Guid.NewGuid(), username, name, null, UserRole.Student, studentId, clock.GetUtcNow());
                db.Users.Add(user);
                usersByName[username] = user;
                usersById[user.Id] = user;
            }

            if (email is not null && users.Concat(usersById.Values).Any(u => u.Id != user.Id && u.Email == email))
            { r.Fail("email", $"{email} is already used by another account."); continue; }

            user.UpdateProfile(name, email, studentId);
            if (isNew)
            {
                record = new StudentRecord(Guid.NewGuid(), user.Id, studentId, program.Id, catalogYear, admitTerm);
                db.StudentRecords.Add(record);
                records[studentId] = record;
                recordUserIds.Add(user.Id);
            }

            record!.Update(program.Id, catalogYear, admitTerm, status, advisorId);
            record.SetHold(hold);

            if (isNew) report.Created++;
            else
            {
                var recordEntry = db.Entry(record!);
                var userEntry = db.Entry(user);
                recordEntry.DetectChanges();
                userEntry.DetectChanges();
                if (recordEntry.State == EntityState.Modified || userEntry.State == EntityState.Modified) report.Updated++;
                else report.Unchanged++;
            }
        }
    }

    // ---------------------------------------------------------------- Transcripts

    private enum AttemptKind { Completed = 1, InProgress = 2, Withdrawn = 3, Transfer = 4 }

    private async Task ImportTranscriptsAsync(CsvTable table, ImportReport report, CancellationToken ct)
    {
        var courses = await db.Courses.ToDictionaryAsync(c => c.Code, ct);
        var ids = table.Rows.Select(r => r["student_id"]).Where(s => s is not null).Distinct().ToList();
        var records = await db.StudentRecords.Where(s => ids.Contains(s.StudentId))
            .Include(s => s.Transcript).Include(s => s.TransferCredits)
            .ToDictionaryAsync(s => s.StudentId, StringComparer.OrdinalIgnoreCase, ct);

        foreach (var student in table.Rows.GroupBy(r => r["student_id"] ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            var errorsBefore = report.ErrorCount;
            if (!records.TryGetValue(student.Key, out var record))
            {
                report.Error(student.First().Line, "student_id",
                    student.Key.Length == 0 ? "'student_id' is required." : $"Student {student.Key} doesn't exist. Import students first.");
                continue;
            }

            var attempts = new List<(Guid CourseId, string TermCode, int Credits, string? Grade, TranscriptStatus Status)>();
            var transfers = new List<(Guid CourseId, int Credits, string? Source)>();
            var seenAttempts = new HashSet<string>();
            foreach (var row in student)
            {
                var r = new RowReader(row, report);
                var code = r.Required("course_code").ToUpperInvariant();
                var grade = r.Optional("grade")?.ToUpperInvariant();
                var kind = r.Enum<AttemptKind>("status", grade is null ? AttemptKind.InProgress : grade == "W" ? AttemptKind.Withdrawn : AttemptKind.Completed);
                var termCode = r.Optional("term_code")?.ToUpperInvariant();
                var credits = r.OptionalInt("credits", 0, Course.MaxCredits);
                if (!r.Ok) continue;
                if (!courses.TryGetValue(code, out var course)) { r.Fail("course_code", $"{code} isn't in the catalog. Import courses first."); continue; }
                var creditValue = credits ?? course.Credits;

                if (kind == AttemptKind.Transfer)
                {
                    transfers.Add((course.Id, creditValue, r.Optional("source")));
                    continue;
                }

                if (termCode is null || !TermCodes.IsValid(termCode)) { r.Fail("term_code", "Give the term as e.g. 2026FA (only transfer credit has no term)."); continue; }
                if (!seenAttempts.Add(Key(course.Id, termCode))) { r.Fail("course_code", $"{code} appears twice for {termCode}."); continue; }

                switch (kind)
                {
                    case AttemptKind.Completed when !Grades.IsKnown(grade):
                        r.Fail("grade", $"'{grade}' isn't a final grade (A, A-, B+, B, B-, C+, C, C-, D+, D, F or P).");
                        continue;
                    case AttemptKind.InProgress when grade is not null:
                        r.Fail("grade", "Courses in progress have no grade yet; leave grade empty.");
                        continue;
                }

                attempts.Add((course.Id, termCode, creditValue, kind == AttemptKind.Completed ? grade : null,
                    kind switch { AttemptKind.InProgress => TranscriptStatus.InProgress, AttemptKind.Withdrawn => TranscriptStatus.Withdrawn, _ => TranscriptStatus.Completed }));
            }

            if (report.ErrorCount > errorsBefore) continue;

            var before = HistorySignature(record.Transcript.Select(t => (t.CourseId, t.TermCode, t.Credits, t.Grade, t.Status)), record.TransferCredits.Select(t => (t.CourseId, t.Credits, t.Source)));
            var after = HistorySignature(attempts, transfers);
            if (before == after) { report.Unchanged++; continue; }

            var hadHistory = record.Transcript.Count + record.TransferCredits.Count > 0;
            record.ReplaceHistory(attempts, transfers);
            if (hadHistory) report.Updated++;
            else report.Created++;
        }
    }

    private static string HistorySignature(
        IEnumerable<(Guid CourseId, string TermCode, int Credits, string? Grade, TranscriptStatus Status)> attempts,
        IEnumerable<(Guid CourseId, int Credits, string? Source)> transfers) =>
        string.Join(';', attempts.Select(a => Key(a.CourseId, a.TermCode, a.Credits, a.Grade, a.Status)).Order(StringComparer.Ordinal))
        + "#" + string.Join(';', transfers.Select(t => Key(t.CourseId, t.Credits, t.Source)).Order(StringComparer.Ordinal));
}
