using Liwa.Domain.Academic;
using Liwa.Domain.Planning;
using Liwa.Domain.Users;

namespace Liwa.UnitTests.Planning;

/// <summary>Holds, grade posting and the "Needs review" rule (brief §4, §8, §10).</summary>
public sealed class RevalidationTests
{
    [Fact]
    public void A_hold_lets_the_student_plan_but_warns_that_registration_waits()
    {
        var s = new Scenario { Hold = "FINANCE" }.Course("ITC201").Section("ITC201");

        var plan = s.Run();
        var check = Planner.Check(s.Input(), [s.Sections[0].Id]);

        Assert.Equal(["ITC201"], plan.PlannedCodes());
        Assert.Equal("FINANCE", plan.Warnings.Get(ReasonCode.AccountHold).Value);
        Assert.True(check.IsValid);
        Assert.Contains(check.Warnings, w => w.Code == ReasonCode.AccountHold && w.Value == "FINANCE");
    }

    [Fact]
    public void Without_a_hold_there_is_no_hold_warning()
    {
        var s = new Scenario().Course("ITC201").Section("ITC201");
        Assert.DoesNotContain(s.Run().Warnings, w => w.Code == ReasonCode.AccountHold);
        Assert.DoesNotContain(Planner.Check(s.Input(), [s.Sections[0].Id]).Warnings, w => w.Code == ReasonCode.AccountHold);
    }

    [Fact]
    public void A_failed_prerequisite_is_a_revalidation_failure()
    {
        // The plan was approved while CSC102 was in progress; then CSC102 got an F.
        var s = new Scenario().Course("CSC201", prereq: "CSC102").Section("CSC201");
        s.Failed.Add("CSC102");

        var failures = Planner.RevalidationFailures(Planner.Check(s.Input(), [s.Sections[0].Id]));

        Assert.Contains(failures, f => f.Code == ReasonCode.NotEligibleNow);
    }

    [Fact]
    public void A_section_filling_up_does_not_flip_an_approved_plan()
    {
        var s = new Scenario().Course("ITC201").Section("ITC201", seats: 0);

        var check = Planner.Check(s.Input(), [s.Sections[0].Id]);

        Assert.Contains(check.Errors, e => e.Code == ReasonCode.SectionFull);
        Assert.Empty(Planner.RevalidationFailures(check));
    }

    [Fact]
    public void A_missing_corequisite_is_a_revalidation_failure()
    {
        var s = new Scenario().Course("CSC101", prereq: "COREQ(CSC101L)").Course("CSC101L", credits: 1).Section("CSC101").Section("CSC101L");

        var failures = Planner.RevalidationFailures(Planner.Check(s.Input(), [s.Sections[0].Id]));

        Assert.Contains(failures, f => f.Code == ReasonCode.CorequisiteMissing);
    }

    [Fact]
    public void Posting_a_grade_completes_the_course_in_progress()
    {
        var courseId = Guid.NewGuid();
        var record = new StudentRecord(Guid.NewGuid(), Guid.NewGuid(), "202500103", Guid.NewGuid(), 2024, "2025FA");
        record.ReplaceHistory([(courseId, "2026FA", 3, null, TranscriptStatus.InProgress)], []);

        Assert.False(record.PostGrade(courseId, "2027SP", "A")); // wrong term
        Assert.True(record.PostGrade(courseId, "2026fa", "B+"));
        Assert.False(record.PostGrade(courseId, "2026FA", "A")); // no longer in progress

        var entry = Assert.Single(record.Transcript);
        Assert.Equal(TranscriptStatus.Completed, entry.Status);
        Assert.Equal("B+", entry.Grade);
    }

    [Fact]
    public void Posting_W_records_a_withdrawal()
    {
        var courseId = Guid.NewGuid();
        var record = new StudentRecord(Guid.NewGuid(), Guid.NewGuid(), "202500103", Guid.NewGuid(), 2024, "2025FA");
        record.ReplaceHistory([(courseId, "2026FA", 3, null, TranscriptStatus.InProgress)], []);

        Assert.True(record.PostGrade(courseId, "2026FA", "w"));

        Assert.Equal(TranscriptStatus.Withdrawn, Assert.Single(record.Transcript).Status);
    }

    [Fact]
    public void Holds_are_normalised_and_can_be_cleared()
    {
        var record = new StudentRecord(Guid.NewGuid(), Guid.NewGuid(), "202500103", Guid.NewGuid(), 2024, "2025FA");
        record.SetHold(" finance ");
        Assert.Equal("FINANCE", record.Hold);
        record.SetHold("");
        Assert.Null(record.Hold);
    }

    [Fact]
    public void Preferred_language_accepts_only_english_and_arabic()
    {
        var user = new User(Guid.NewGuid(), "student.new", "Mariam", null, UserRole.Student, null, DateTimeOffset.UnixEpoch);
        Assert.Equal("en", user.PreferredLanguage);
        user.SetPreferredLanguage("AR");
        Assert.Equal("ar", user.PreferredLanguage);
        Assert.Throws<ArgumentException>(() => user.SetPreferredLanguage("fr"));
    }
}
