using Liwa.Domain.Academic;

namespace Liwa.UnitTests.Academic;

public sealed class AcademicSummaryTests
{
    private static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid(), D = Guid.NewGuid();

    private static StudentRecord Record(params (Guid Course, string Term, int Credits, string? Grade, TranscriptStatus Status)[] attempts) =>
        Record(attempts, []);

    private static StudentRecord Record((Guid, string, int, string?, TranscriptStatus)[] attempts, (Guid, int, string?)[] transfers)
    {
        var r = new StudentRecord(Guid.NewGuid(), Guid.NewGuid(), "202500001", Guid.NewGuid(), 2024, "2025FA");
        r.ReplaceHistory(attempts, transfers);
        return r;
    }

    [Fact]
    public void Gpa_is_credit_weighted_and_rounded_to_two_decimals()
    {
        // A (4.0 × 3) + B+ (3.3 × 4) = 25.2 over 7 credits = 3.6
        var s = AcademicSummary.From(Record((A, "2025FA", 3, "A", TranscriptStatus.Completed), (B, "2025FA", 4, "B+", TranscriptStatus.Completed)), RepeatGradeRule.LatestCounts);
        Assert.Equal(3.60m, s.Gpa);
        Assert.Equal(7, s.CreditsEarned);
    }

    [Fact]
    public void Failing_counts_in_the_gpa_but_earns_no_credit()
    {
        var s = AcademicSummary.From(Record((A, "2025FA", 3, "A", TranscriptStatus.Completed), (B, "2025FA", 3, "F", TranscriptStatus.Completed)), RepeatGradeRule.LatestCounts);
        Assert.Equal(2.00m, s.Gpa);
        Assert.Equal(3, s.CreditsEarned);
    }

    [Fact]
    public void Latest_attempt_counts_by_default_and_highest_can_be_configured()
    {
        var record = Record((A, "2025FA", 3, "C", TranscriptStatus.Completed), (A, "2026SP", 3, "D", TranscriptStatus.Completed));
        Assert.Equal(1.00m, AcademicSummary.From(record, RepeatGradeRule.LatestCounts).Gpa);
        Assert.Equal(2.00m, AcademicSummary.From(record, RepeatGradeRule.HighestCounts).Gpa);
        Assert.Equal(3, AcademicSummary.From(record, RepeatGradeRule.LatestCounts).CreditsEarned); // credit counted once
    }

    [Fact]
    public void Transfer_credit_and_pass_grades_count_toward_the_degree_not_the_gpa()
    {
        var s = AcademicSummary.From(Record(
            [(A, "2025FA", 3, "B", TranscriptStatus.Completed), (B, "2025FA", 1, "P", TranscriptStatus.Completed)],
            [(C, 3, "AP English")]), RepeatGradeRule.LatestCounts);
        Assert.Equal(3.00m, s.Gpa);
        Assert.Equal(7, s.CreditsEarned);
        Assert.Equal(3, s.GpaCredits);
    }

    [Fact]
    public void Courses_in_progress_and_withdrawals_are_reported_separately()
    {
        var s = AcademicSummary.From(Record(
            (A, "2026FA", 3, null, TranscriptStatus.InProgress),
            (B, "2026FA", 3, null, TranscriptStatus.Withdrawn),
            (D, "2026SP", 3, "A", TranscriptStatus.Completed)), RepeatGradeRule.LatestCounts);
        Assert.Equal(3, s.InProgressCredits);
        Assert.Equal(3, s.CreditsEarned);
        Assert.Equal(4.00m, s.Gpa);
    }

    [Fact]
    public void A_student_with_no_grades_has_no_gpa_yet() =>
        Assert.Null(AcademicSummary.From(Record(), RepeatGradeRule.LatestCounts).Gpa);

    [Theory]
    [InlineData(0, Standing.Freshman)]
    [InlineData(29, Standing.Freshman)]
    [InlineData(30, Standing.Sophomore)]
    [InlineData(60, Standing.Junior)]
    [InlineData(89, Standing.Junior)]
    [InlineData(90, Standing.Senior)]
    public void Standing_follows_credits_earned(int credits, Standing expected) => Assert.Equal(expected, AcademicSummary.StandingFor(credits));

    [Theory]
    [InlineData("B", "C", true)]
    [InlineData("C", "C", true)]
    [InlineData("C-", "C", false)]
    [InlineData("F", "D", false)]
    [InlineData("P", "C", true)]
    public void Minimum_grades_compare_on_the_letter_scale(string grade, string minimum, bool meets) =>
        Assert.Equal(meets, Grades.MeetsMinimum(grade, minimum));
}

/// <summary>Credit limits are configuration, checked in priority order (defaults mirror appsettings.json).</summary>
public sealed class CreditPolicyTests
{
    private static CreditPolicy DefaultPolicy()
    {
        var p = new CreditPolicy(Guid.NewGuid(), "Test");
        p.ReplaceRules([
            new CreditLimitRule(Guid.NewGuid(), 1, "Summer, on probation", 0, 6).When(season: TermSeason.Summer, onProbation: true),
            new CreditLimitRule(Guid.NewGuid(), 2, "Summer", 0, 9).When(season: TermSeason.Summer),
            new CreditLimitRule(Guid.NewGuid(), 3, "Probation", 9, 12).When(onProbation: true),
            new CreditLimitRule(Guid.NewGuid(), 4, "High GPA", 12, 21).When(minGpa: 3.50m),
            new CreditLimitRule(Guid.NewGuid(), 5, "Standard", 12, 18),
        ]);
        return p;
    }

    [Fact]
    public void A_student_exactly_at_gpa_3_50_gets_the_21_credit_cap()
    {
        var limits = DefaultPolicy().LimitsFor(TermSeason.Spring, Standing.Sophomore, 3.50m, onProbation: false);
        Assert.Equal(21, limits!.MaxCredits);
        Assert.Equal("High GPA", limits.RuleName);
    }

    [Fact]
    public void Just_below_3_50_gets_the_standard_cap() =>
        Assert.Equal(18, DefaultPolicy().LimitsFor(TermSeason.Spring, Standing.Sophomore, 3.49m, false)!.MaxCredits);

    [Fact]
    public void Probation_caps_credits_whatever_the_gpa() =>
        Assert.Equal(12, DefaultPolicy().LimitsFor(TermSeason.Fall, Standing.Junior, 3.80m, onProbation: true)!.MaxCredits);

    [Fact]
    public void Summer_has_its_own_limits() =>
        Assert.Equal(new CreditLimits(0, 9, "Summer"), DefaultPolicy().LimitsFor(TermSeason.Summer, Standing.Senior, 3.9m, false));

    [Fact]
    public void A_graduating_senior_with_6_credits_left_is_not_forced_to_the_12_credit_minimum()
    {
        var limits = DefaultPolicy().LimitsFor(TermSeason.Spring, Standing.Senior, 3.35m, false, creditsLeftToGraduate: 6);
        Assert.Equal(6, limits!.MinCredits);
        Assert.Equal(18, limits.MaxCredits);
    }

    [Fact]
    public void The_graduation_exemption_can_be_switched_off()
    {
        var p = DefaultPolicy();
        p.SetSwitches(RepeatGradeRule.LatestCounts, true, graduatingExemptFromMinimum: false);
        Assert.Equal(12, p.LimitsFor(TermSeason.Spring, Standing.Senior, 3.0m, false, creditsLeftToGraduate: 6)!.MinCredits);
    }

    [Fact]
    public void A_gap_in_the_rules_is_reported_not_guessed()
    {
        var p = new CreditPolicy(Guid.NewGuid(), "Only summer");
        p.ReplaceRules([new CreditLimitRule(Guid.NewGuid(), 1, "Summer", 0, 9).When(season: TermSeason.Summer)]);
        Assert.Null(p.LimitsFor(TermSeason.Fall, Standing.Freshman, null, false));
    }

    [Fact]
    public void Rules_need_unique_priorities() =>
        Assert.Throws<ArgumentException>(() => new CreditPolicy(Guid.NewGuid(), "x").ReplaceRules([
            new CreditLimitRule(Guid.NewGuid(), 1, "a", 0, 9), new CreditLimitRule(Guid.NewGuid(), 1, "b", 0, 9)]));
}
