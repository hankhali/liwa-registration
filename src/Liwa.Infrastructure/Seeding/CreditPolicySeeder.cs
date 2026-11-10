using Liwa.Domain.Academic;
using Liwa.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Liwa.Infrastructure.Seeding;

/// <summary>
/// Default credit policy, read from configuration ("CreditPolicy" in appsettings.json). Numbers are never
/// hard-coded; they are starting values to confirm with the registrar and are edited by admins later (M6).
/// </summary>
public sealed class CreditPolicyDefaults
{
    public const string Section = "CreditPolicy";

    public string Name { get; set; } = "Default credit policy";

    public RepeatGradeRule RepeatGradeRule { get; set; } = RepeatGradeRule.LatestCounts;

    public bool AllowInProgressPrerequisites { get; set; } = true;

    public bool GraduatingStudentsExemptFromMinimum { get; set; } = true;

    public List<CreditRuleDefaults> Rules { get; set; } = [];
}

public sealed class CreditRuleDefaults
{
    public string Name { get; set; } = string.Empty;

    public TermSeason? Season { get; set; }

    public Standing? Standing { get; set; }

    public decimal? MinGpa { get; set; }

    public decimal? MaxGpaExclusive { get; set; }

    public bool? OnProbation { get; set; }

    public int MinCredits { get; set; }

    public int MaxCredits { get; set; }
}

/// <summary>Creates the credit policy from configuration the first time the app starts. Never overwrites admin edits.</summary>
public sealed class CreditPolicySeeder(LiwaDbContext db, IOptions<CreditPolicyDefaults> options, ILogger<CreditPolicySeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct)
    {
        if (await db.CreditPolicies.AnyAsync(ct)) return;

        var defaults = options.Value;
        if (defaults.Rules.Count == 0)
        {
            logger.LogWarning("No credit rules are configured (CreditPolicy:Rules); the planner can't apply credit limits until an admin adds them.");
            return;
        }

        db.CreditPolicies.Add(Build(defaults));
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Created the credit policy from configuration with {Count} rule(s).", defaults.Rules.Count);
    }

    /// <summary>The policy entity for a set of configured defaults (also used by tests on the seeded data).</summary>
    public static CreditPolicy Build(CreditPolicyDefaults defaults)
    {
        var policy = new CreditPolicy(Guid.NewGuid(), defaults.Name);
        policy.SetSwitches(defaults.RepeatGradeRule, defaults.AllowInProgressPrerequisites, defaults.GraduatingStudentsExemptFromMinimum);
        policy.ReplaceRules(defaults.Rules.Select((r, i) =>
            new CreditLimitRule(Guid.NewGuid(), i + 1, r.Name, r.MinCredits, r.MaxCredits)
                .When(r.Season, r.Standing, r.MinGpa, r.MaxGpaExclusive, r.OnProbation)));
        return policy;
    }
}
