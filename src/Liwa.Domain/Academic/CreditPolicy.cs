namespace Liwa.Domain.Academic;

/// <summary>
/// One credit-limit rule. Every condition that is set must match; unset conditions match anything.
/// Rules are checked in <see cref="Priority"/> order (lowest first); the first match wins.
/// </summary>
public sealed class CreditLimitRule
{
    private CreditLimitRule() { } // EF Core

    public CreditLimitRule(Guid id, int priority, string name, int minCredits, int maxCredits)
    {
        if (minCredits < 0 || maxCredits < minCredits) throw new ArgumentException("Credit limits must satisfy 0 ≤ minimum ≤ maximum.", nameof(maxCredits));
        Id = id;
        Priority = priority;
        Name = string.IsNullOrWhiteSpace(name) ? $"Rule {priority}" : name.Trim();
        MinCredits = minCredits;
        MaxCredits = maxCredits;
    }

    public Guid Id { get; private set; }

    public Guid CreditPolicyId { get; private set; }

    public int Priority { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public TermSeason? Season { get; private set; }

    public Standing? Standing { get; private set; }

    /// <summary>Inclusive lower GPA bound (e.g. 3.50 matches a GPA of exactly 3.50).</summary>
    public decimal? MinGpa { get; private set; }

    /// <summary>Exclusive upper GPA bound.</summary>
    public decimal? MaxGpaExclusive { get; private set; }

    public bool? OnProbation { get; private set; }

    public int MinCredits { get; private set; }

    public int MaxCredits { get; private set; }

    public CreditLimitRule When(TermSeason? season = null, Standing? standing = null, decimal? minGpa = null, decimal? maxGpaExclusive = null, bool? onProbation = null)
    {
        Season = season;
        Standing = standing;
        MinGpa = minGpa;
        MaxGpaExclusive = maxGpaExclusive;
        OnProbation = onProbation;
        return this;
    }

    public bool Matches(TermSeason season, Standing standing, decimal? gpa, bool onProbation) =>
        (Season is null || Season == season)
        && (Standing is null || Standing == standing)
        && (OnProbation is null || OnProbation == onProbation)
        && (MinGpa is null || (gpa is not null && gpa.Value >= MinGpa.Value))
        && (MaxGpaExclusive is null || (gpa is not null && gpa.Value < MaxGpaExclusive.Value));

    internal void AttachTo(Guid policyId) => CreditPolicyId = policyId;
}

public sealed record CreditLimits(int MinCredits, int MaxCredits, string RuleName);

/// <summary>
/// Credit limits and planning switches. Fully configurable (brief §8); defaults are seeded from configuration
/// and must be confirmed with the registrar. There is one active policy.
/// </summary>
public sealed class CreditPolicy
{
    private readonly List<CreditLimitRule> _rules = [];

    private CreditPolicy() { } // EF Core

    public CreditPolicy(Guid id, string name)
    {
        Id = id;
        Name = name;
        RepeatGradeRule = RepeatGradeRule.LatestCounts;
        AllowInProgressPrerequisites = true;
        GraduatingStudentsExemptFromMinimum = true;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public RepeatGradeRule RepeatGradeRule { get; private set; }

    /// <summary>Courses in progress count toward prerequisites "with condition" (carried as a plan warning).</summary>
    public bool AllowInProgressPrerequisites { get; private set; }

    /// <summary>A student who needs fewer credits than the minimum to graduate is not forced up to the minimum.</summary>
    public bool GraduatingStudentsExemptFromMinimum { get; private set; }

    public IReadOnlyList<CreditLimitRule> Rules => _rules;

    public void SetSwitches(RepeatGradeRule repeatRule, bool allowInProgressPrerequisites, bool graduatingExemptFromMinimum)
    {
        RepeatGradeRule = repeatRule;
        AllowInProgressPrerequisites = allowInProgressPrerequisites;
        GraduatingStudentsExemptFromMinimum = graduatingExemptFromMinimum;
    }

    public void ReplaceRules(IEnumerable<CreditLimitRule> rules)
    {
        var list = rules.OrderBy(r => r.Priority).ToList();
        if (list.Select(r => r.Priority).Distinct().Count() != list.Count)
            throw new ArgumentException("Each credit rule needs its own priority.", nameof(rules));
        _rules.Clear();
        foreach (var r in list)
        {
            r.AttachTo(Id);
            _rules.Add(r);
        }
    }

    /// <summary>
    /// Credit limits for a student in a term. Returns null when no rule matches (a configuration gap the
    /// admin must fix; the planner reports it rather than guessing).
    /// </summary>
    public CreditLimits? LimitsFor(TermSeason season, Standing standing, decimal? gpa, bool onProbation, int? creditsLeftToGraduate = null)
    {
        var rule = _rules.OrderBy(r => r.Priority).FirstOrDefault(r => r.Matches(season, standing, gpa, onProbation));
        if (rule is null) return null;

        var min = rule.MinCredits;
        if (GraduatingStudentsExemptFromMinimum && creditsLeftToGraduate is { } left && left < min)
            min = Math.Max(0, left);
        return new CreditLimits(min, rule.MaxCredits, rule.Name);
    }
}
