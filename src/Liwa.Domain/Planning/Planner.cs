using Liwa.Domain.Academic;
using Liwa.Domain.Academic.Prerequisites;

namespace Liwa.Domain.Planning;

/// <summary>
/// The deterministic planner described in docs/planner.md. Pure: no database, no clock, no randomness.
/// </summary>
public static partial class Planner
{
    /// <summary>The projection gives up after this many terms.</summary>
    public const int ProjectionHorizon = 24;

    private enum Sat { No = 0, Conditional = 1, Yes = 2 }

    private sealed record Need(string Code, int Credits, string GroupKey, string GroupName, RequirementKind Kind, int? Suggested, OfferedIn Offered, Prerequisite? Rule);

    /// <summary>Result of checking a prerequisite rule.</summary>
    private sealed class Eval
    {
        public Sat Result { get; set; } = Sat.Yes;

        public List<PlannerReason> Failures { get; } = [];

        public SortedSet<string> WaitingFor { get; } = new(StringComparer.Ordinal);

        public SortedSet<string> PendingCoreqs { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>What the student has (really or in the projection) when a rule is checked.</summary>
    private sealed record State(
        IReadOnlyDictionary<string, string> Grades,
        IReadOnlySet<string> InProgress,
        IReadOnlySet<string> Failed,
        int CreditsEarned,
        IReadOnlySet<string> Permissions,
        bool AllowInProgress,
        bool AssumePermission);

    /// <summary>The term's own switch wins over the policy (brief §4: AllowInProgressPrerequisites with a per-term override).</summary>
    internal static bool AllowInProgress(PlannerInput input) => input.Term.AllowInProgressPrerequisites ?? input.Policy.AllowInProgressPrerequisites;

    /// <summary>A section with this share of its seats taken (or more) gets a NearlyFull warning (brief §4: warn at 90%).</summary>
    public const decimal NearlyFullShare = 0.9m;

    /// <summary>
    /// Puts the input in a canonical order (catalog and sections by code, each group's courses by code), so the result
    /// never depends on the order a database or a file happened to return rows in (brief §5: CSV and database give
    /// identical output). Groups keep their order: it is meaningful (the degree plan's sort order).
    /// </summary>
    internal static PlannerInput Canonical(PlannerInput input) => input with
    {
        Catalog = input.Catalog.OrderBy(c => c.Code, StringComparer.Ordinal).ToList(),
        Sections = input.Sections.OrderBy(s => s.CourseCode, StringComparer.Ordinal).ThenBy(s => s.SectionCode, StringComparer.Ordinal).ToList(),
        DegreePlan = input.DegreePlan.Select(g => g with { Courses = g.Courses.OrderBy(c => c.Code, StringComparer.Ordinal).ToList() }).ToList(),
    };

    public static PlannerResult Plan(PlannerInput input)
    {
        input = Canonical(input);
        var student = input.Student;
        var catalog = input.Catalog.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
        var policy = input.Policy;

        // 1. What's left --------------------------------------------------------------------
        var (needs, chooseRemaining, chooseNames, freeRemaining) = RemainingRequirements(input, catalog);
        var needByCode = needs.ToDictionary(n => n.Code, StringComparer.Ordinal);
        var creditsLeft = needs.Where(n => n.Kind == RequirementKind.AllOf).Sum(n => n.Credits) + chooseRemaining.Values.Sum() + freeRemaining;

        // 2. Priority (critical path) --------------------------------------------------------
        var (chain, unlocks, dependents) = CriticalPath(needs, needByCode);
        var ordered = needs
            .OrderByDescending(n => chain[n.Code])
            .ThenByDescending(n => unlocks[n.Code])
            .ThenBy(n => n.Kind == RequirementKind.AllOf ? 0 : 1)
            .ThenBy(n => SeasonCount(n.Offered))
            .ThenBy(n => n.Suggested ?? int.MaxValue)
            .ThenBy(n => n.Code, StringComparer.Ordinal)
            .ToList();
        var rank = ordered.Select((n, i) => (n.Code, i + 1)).ToDictionary(x => x.Code, x => x.Item2, StringComparer.Ordinal);

        // 3. Eligibility this term ------------------------------------------------------------
        var standing = AcademicSummary.StandingFor(student.CreditsEarned);
        var state = new State(student.Passed, student.InProgress, student.Failed, student.CreditsEarned, student.PermissionsGranted,
            AllowInProgress(input), AssumePermission: false);
        var sectionsByCourse = input.Sections
            .GroupBy(s => s.CourseCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.SectionCode, StringComparer.Ordinal).ToList(), StringComparer.OrdinalIgnoreCase);

        var status = new Dictionary<string, CourseStatus>(StringComparer.Ordinal);
        var reasons = new Dictionary<string, List<PlannerReason>>(StringComparer.Ordinal);
        var evals = new Dictionary<string, Eval>(StringComparer.Ordinal);
        var warnings = new List<PlannerReason>();
        var offeredNoSections = new List<string>();

        foreach (var n in ordered)
        {
            var r = reasons[n.Code] = [];
            if (!n.Offered.Includes(input.Term.Season))
            {
                status[n.Code] = CourseStatus.NotEligible;
                r.Add(PlannerReason.Of(ReasonCode.NotOfferedThisTerm, SeasonsText(n.Offered)));
                continue;
            }

            if (!sectionsByCourse.ContainsKey(n.Code))
            {
                status[n.Code] = CourseStatus.NotEligible;
                r.Add(PlannerReason.Of(ReasonCode.NoSectionsThisTerm));
                offeredNoSections.Add(n.Code);
                continue;
            }

            var e = evals[n.Code] = Evaluate(n.Rule, n.Code, state);
            switch (e.Result)
            {
                case Sat.No:
                    status[n.Code] = CourseStatus.NotEligible;
                    r.AddRange(e.Failures);
                    break;
                case Sat.Conditional:
                    status[n.Code] = CourseStatus.EligibleWithCondition;
                    r.Add(PlannerReason.Of(ReasonCode.WaitingForGrade, e.WaitingFor.Prepend(n.Code)));
                    break;
                default:
                    status[n.Code] = CourseStatus.Eligible;
                    break;
            }
        }

        if (offeredNoSections.Count > 0) warnings.Add(PlannerReason.Of(ReasonCode.OfferedButNoSections, offeredNoSections.Order(StringComparer.Ordinal)));
        if (student.Hold is { } hold) warnings.Add(PlannerReason.Of(ReasonCode.AccountHold, hold));

        // 4. Credit limits ----------------------------------------------------------------------
        var limits = policy.LimitsFor(input.Term.Season, standing, student.Gpa, student.OnProbation, creditsLeft);
        if (limits is null) warnings.Add(PlannerReason.Of(ReasonCode.NoCreditRule));
        var maxCredits = Math.Min(limits?.MaxCredits ?? 0, input.Preferences.MaxCredits ?? int.MaxValue);

        // 5. Greedy packing ----------------------------------------------------------------------
        var chosen = new List<(Need Need, PlannerSection Section, List<string> Partners)>();
        var chosenCodes = new HashSet<string>(StringComparer.Ordinal);
        var alternatives = new Dictionary<string, List<RejectedSection>>(StringComparer.Ordinal);
        var quota = new Dictionary<string, int>(chooseRemaining, StringComparer.Ordinal);
        var total = 0;

        foreach (var n in ordered)
        {
            if (chosenCodes.Contains(n.Code)) continue;
            if (status[n.Code] is not (CourseStatus.Eligible or CourseStatus.EligibleWithCondition)) continue;
            var r = reasons[n.Code];

            if (n.Kind == RequirementKind.ChooseCredits && quota.GetValueOrDefault(n.GroupKey) <= 0)
            {
                r.Add(PlannerReason.Of(ReasonCode.ElectiveQuotaMet, n.GroupName));
                continue;
            }

            // Corequisites not yet passed or in progress must be planned in the same term.
            var partners = evals[n.Code].PendingCoreqs.Where(c => !chosenCodes.Contains(c)).ToList();
            var badPartner = partners.FirstOrDefault(p => !CanPartner(p, n.Code, input, catalog, sectionsByCourse, state));
            if (badPartner is not null)
            {
                r.Add(PlannerReason.Of(ReasonCode.CorequisiteDoesNotFit, [badPartner]));
                continue;
            }

            var group = new[] { n.Code }.Concat(partners).ToList();
            var credits = group.Sum(c => catalog[c].Credits);
            if (total + credits > maxCredits)
            {
                r.Add(PlannerReason.Of(ReasonCode.CreditLimitReached, maxCredits.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                continue;
            }

            // Section options for each course in the group, with the reasons each section is ruled out.
            var options = new Dictionary<string, List<PlannerSection>>(StringComparer.Ordinal);
            var ruledOut = new Dictionary<string, List<RejectedSection>>(StringComparer.Ordinal);
            foreach (var code in group)
            {
                options[code] = [];
                ruledOut[code] = [];
                foreach (var s in sectionsByCourse[code])
                {
                    var why = SectionProblems(s, input, chosen.Select(c => (c.Need.Code, c.Section)));
                    if (why.Count == 0) options[code].Add(s);
                    else ruledOut[code].Add(new RejectedSection(s.Id, s.SectionCode, why));
                }

                options[code] = options[code].OrderByDescending(s => s.SeatsLeft).ThenBy(s => s.SectionCode, StringComparer.Ordinal).ToList();
            }

            var combo = FirstFittingCombination(group, options);
            if (combo is null)
            {
                var sectionReasons = ruledOut.Values.SelectMany(x => x).SelectMany(x => x.Reasons).Distinct().ToList();
                if (partners.Count == 0)
                {
                    r.Add(PlannerReason.Of(ReasonCode.NoSectionFits));
                    r.AddRange(sectionReasons);
                }
                else
                {
                    // Rejected as a pair: each course names the other(s).
                    foreach (var code in group)
                    {
                        var list = reasons.TryGetValue(code, out var existing) ? existing : reasons[code] = [];
                        list.Add(PlannerReason.Of(ReasonCode.CorequisiteDoesNotFit, group.Where(g => g != code)));
                        list.AddRange(ruledOut[code].SelectMany(x => x.Reasons).Distinct());
                    }
                }

                continue;
            }

            foreach (var code in group)
            {
                var pick = combo[code];
                var need = needByCode.TryGetValue(code, out var nn) ? nn : AsNeed(catalog[code]);
                chosen.Add((need, pick, group.Where(g => g != code).ToList()));
                chosenCodes.Add(code);
                if (need.Kind == RequirementKind.ChooseCredits) quota[need.GroupKey] = quota.GetValueOrDefault(need.GroupKey) - need.Credits;

                var alts = ruledOut[code].ToList();
                foreach (var other in options[code].Where(o => o.Id != pick.Id))
                {
                    var clash = combo.Where(kv => kv.Key != code && kv.Value.Meetings.Any(m => other.Meetings.Any(m.Overlaps))).Select(kv => kv.Key).ToList();
                    alts.Add(new RejectedSection(other.Id, other.SectionCode, clash.Count > 0
                        ? [PlannerReason.Of(ReasonCode.ClashesWith, clash)]
                        : [PlannerReason.Of(ReasonCode.FewerSeatsLeft, pick.SectionCode)]));
                }

                alternatives[code] = alts.OrderBy(a => a.SectionCode, StringComparer.Ordinal).ToList();
            }

            total += credits;
        }

        var minCredits = limits?.MinCredits ?? 0;
        if (limits is not null && total < minCredits)
            warnings.Add(PlannerReason.Of(ReasonCode.BelowMinimumCredits, minCredits.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        var conditional = chosen.Where(c => status.GetValueOrDefault(c.Need.Code) == CourseStatus.EligibleWithCondition).Select(c => c.Need.Code).ToList();
        if (conditional.Count > 0) warnings.Add(PlannerReason.Of(ReasonCode.InProgressPrerequisite, conditional));

        // Assemble plan and assessments ---------------------------------------------------------
        var studentSemester = student.CreditsEarned / 15 + 1;
        var plan = chosen.Select(c =>
        {
            var why = new List<PlannerReason>();
            if (c.Need.Kind == RequirementKind.AllOf) why.Add(PlannerReason.Of(ReasonCode.RequiredForDegree));
            else if (c.Need.Kind == RequirementKind.ChooseCredits) why.Add(PlannerReason.Of(ReasonCode.CountsTowardElectives, c.Need.GroupName));
            if (unlocks.GetValueOrDefault(c.Need.Code) > 0)
                why.Add(PlannerReason.Of(ReasonCode.UnlocksOtherCourses, dependents[c.Need.Code].Order(StringComparer.Ordinal),
                    unlocks[c.Need.Code].ToString(System.Globalization.CultureInfo.InvariantCulture)));
            if (SeasonCount(c.Need.Offered) == 1) why.Add(PlannerReason.Of(ReasonCode.OnlyOfferedIn, SeasonsText(c.Need.Offered)));
            if (c.Need.Suggested is { } sem && sem <= studentSemester)
                why.Add(PlannerReason.Of(ReasonCode.SuggestedForThisSemester, sem.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            if (c.Partners.Count > 0) why.Add(PlannerReason.Of(ReasonCode.TakeWithCorequisite, c.Partners));
            if (evals.TryGetValue(c.Need.Code, out var e) && e.Result == Sat.Conditional) why.Add(PlannerReason.Of(ReasonCode.WaitingForGrade, e.WaitingFor.Prepend(c.Need.Code)));
            return new PlannedCourse(c.Need.Code, c.Need.Credits, c.Section.Id, c.Section.SectionCode, rank.GetValueOrDefault(c.Need.Code), why,
                alternatives.GetValueOrDefault(c.Need.Code) ?? []);
        }).OrderBy(p => p.PriorityRank == 0 ? int.MaxValue : p.PriorityRank).ThenBy(p => p.CourseCode, StringComparer.Ordinal).ToList();

        var assessments = ordered.Select(n => new CourseAssessment(
            n.Code, n.Credits, n.GroupKey,
            chosenCodes.Contains(n.Code) ? CourseStatus.Planned : status[n.Code],
            rank[n.Code], unlocks[n.Code], chain[n.Code],
            chosenCodes.Contains(n.Code) ? plan.First(p => p.CourseCode == n.Code).Reasons : reasons[n.Code].Distinct().ToList())).ToList();

        // 6. Graduation projection ------------------------------------------------------------
        var withSummer = Project(input, catalog, ordered, chosen.Select(c => c.Need.Code).ToList(), total, quota, freeRemaining, includeSummer: true);
        var withoutSummer = Project(input, catalog, ordered, chosen.Select(c => c.Need.Code).ToList(), total, quota, freeRemaining, includeSummer: false);
        if (withSummer.GraduationTerm is null || withoutSummer.GraduationTerm is null) warnings.Add(PlannerReason.Of(ReasonCode.GraduationNotReachable));

        // Courses offered in only one season, eligible now, but not in the plan: skipping them waits a year.
        var missed = ordered.Where(n => SeasonCount(n.Offered) == 1 && n.Offered.Includes(input.Term.Season) && !chosenCodes.Contains(n.Code)
                && status[n.Code] is CourseStatus.Eligible or CourseStatus.EligibleWithCondition)
            .Select(n => n.Code).Order(StringComparer.Ordinal).ToList();
        if (missed.Count > 0) warnings.Add(PlannerReason.Of(ReasonCode.FallOnlyMissed, missed, input.Term.Season.ToString()));

        var nearlyFull = chosen.Where(c => IsNearlyFull(c.Section)).Select(c => c.Need.Code).Order(StringComparer.Ordinal).ToList();
        if (nearlyFull.Count > 0) warnings.Add(PlannerReason.Of(ReasonCode.NearlyFull, nearlyFull));

        // The projection assumes department permission (capstone, internship) is granted later; say so (brief §4).
        var assumed = withSummer.Terms.Skip(1).SelectMany(t => t.Courses)
            .Where(code => catalog.TryGetValue(code, out var pc) && NeedsPermission(pc.Prerequisite) && !student.PermissionsGranted.Contains(code))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (assumed.Count > 0) warnings.Add(PlannerReason.Of(ReasonCode.PermissionAssumed, assumed));

        return new PlannerResult(input.Term.Code, limits, maxCredits, plan, total, assessments, warnings, new GraduationProjection(withSummer, withoutSummer));
    }

    internal static bool IsNearlyFull(PlannerSection s) => s.Capacity > 0 && s.SeatsLeft > 0 && s.Capacity - s.SeatsLeft >= s.Capacity * NearlyFullShare;

    private static bool NeedsPermission(Prerequisite? p) => p switch
    {
        DepartmentPermission => true,
        AllOf a => a.Items.Any(NeedsPermission),
        AnyOf o => o.Items.All(NeedsPermission),
        _ => false,
    };

    // ------------------------------------------------------------------------------ step 1

    private static (List<Need> Needs, Dictionary<string, int> ChooseRemaining, Dictionary<string, string> ChooseNames, int FreeRemaining) RemainingRequirements(
        PlannerInput input, Dictionary<string, PlannerCourse> catalog)
    {
        var s = input.Student;
        bool Covered(string code) => s.Passed.ContainsKey(code) || s.InProgress.Contains(code);

        var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var needs = new List<Need>();
        var chooseRemaining = new Dictionary<string, int>(StringComparer.Ordinal);
        var chooseNames = new Dictionary<string, string>(StringComparer.Ordinal);

        // AllOf first, then ChooseCredits, then AnyCourse: a course counts toward the first group it fits.
        foreach (var g in input.DegreePlan.Where(g => g.Kind == RequirementKind.AllOf))
            foreach (var c in g.Courses.Where(c => catalog.ContainsKey(c.Code) && assigned.Add(c.Code)))
                if (!Covered(c.Code)) needs.Add(ToNeed(catalog[c.Code], g, c.SuggestedSemester));

        foreach (var g in input.DegreePlan.Where(g => g.Kind == RequirementKind.ChooseCredits))
        {
            var mine = g.Courses.Where(c => catalog.ContainsKey(c.Code) && !assigned.Contains(c.Code)).ToList();
            foreach (var c in mine) assigned.Add(c.Code);
            var covered = mine.Where(c => Covered(c.Code)).Sum(c => catalog[c.Code].Credits);
            var remaining = Math.Max(0, g.CreditsRequired - covered);
            chooseRemaining[g.Key] = remaining;
            chooseNames[g.Key] = g.Name;
            if (remaining > 0)
                needs.AddRange(mine.Where(c => !Covered(c.Code)).Select(c => ToNeed(catalog[c.Code], g, c.SuggestedSemester)));
        }

        var freeRequired = input.DegreePlan.Where(g => g.Kind == RequirementKind.AnyCourse).Sum(g => g.CreditsRequired);
        var freeCovered = s.Passed.Keys.Concat(s.InProgress)
            .Where(c => catalog.ContainsKey(c) && !assigned.Contains(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Sum(c => catalog[c].Credits);
        return (needs, chooseRemaining, chooseNames, Math.Max(0, freeRequired - freeCovered));
    }

    private static Need ToNeed(PlannerCourse c, PlannerRequirementGroup g, int? suggested) =>
        new(c.Code, c.Credits, g.Key, g.Name, g.Kind, suggested, c.Offered, c.Prerequisite);

    private static Need AsNeed(PlannerCourse c) => new(c.Code, c.Credits, "", "", RequirementKind.AnyCourse, null, c.Offered, c.Prerequisite);

    // ------------------------------------------------------------------------------ step 2

    private static (Dictionary<string, int> Chain, Dictionary<string, int> Unlocks, Dictionary<string, SortedSet<string>> Dependents) CriticalPath(
        List<Need> needs, Dictionary<string, Need> byCode)
    {
        var prereqDependents = needs.ToDictionary(n => n.Code, _ => new SortedSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        var coreqDependents = needs.ToDictionary(n => n.Code, _ => new SortedSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var n in needs.Where(n => n.Rule is not null))
        {
            foreach (var p in PrerequisiteCodes(n.Rule!).Where(byCode.ContainsKey)) prereqDependents[p].Add(n.Code);
            foreach (var c in CorequisiteCodes(n.Rule!).Where(byCode.ContainsKey)) coreqDependents[c].Add(n.Code);
        }

        var chain = new Dictionary<string, int>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        int Chain(string code)
        {
            if (chain.TryGetValue(code, out var known)) return known;
            if (!visiting.Add(code)) return 1; // corequisite pairs refer to each other
            var value = 1;
            foreach (var d in prereqDependents[code]) value = Math.Max(value, 1 + Chain(d));
            foreach (var d in coreqDependents[code]) value = Math.Max(value, Chain(d));
            visiting.Remove(code);
            chain[code] = value;
            return value;
        }

        var unlocks = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var n in needs)
        {
            Chain(n.Code);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var stack = new Stack<string>(prereqDependents[n.Code]);
            while (stack.Count > 0)
            {
                var d = stack.Pop();
                if (d == n.Code || !seen.Add(d)) continue;
                foreach (var x in prereqDependents[d]) stack.Push(x);
            }

            unlocks[n.Code] = seen.Count;
        }

        return (chain, unlocks, prereqDependents);
    }

    private static IEnumerable<string> PrerequisiteCodes(Prerequisite p) => p switch
    {
        CoursePassed c => [c.CourseCode],
        AllOf a => a.Items.SelectMany(PrerequisiteCodes),
        AnyOf a => a.Items.SelectMany(PrerequisiteCodes),
        _ => [],
    };

    private static IEnumerable<string> CorequisiteCodes(Prerequisite p) => p switch
    {
        Corequisite c => [c.CourseCode],
        AllOf a => a.Items.SelectMany(CorequisiteCodes),
        AnyOf a => a.Items.SelectMany(CorequisiteCodes),
        _ => [],
    };

    // ------------------------------------------------------------------------------ step 3

    private static Eval Evaluate(Prerequisite? rule, string course, State st)
    {
        var e = new Eval();
        if (rule is null) return e;

        switch (rule)
        {
            case CoursePassed c:
                if (st.Grades.TryGetValue(c.CourseCode, out var grade))
                {
                    if (c.MinGrade is null || MeetsMinimum(grade, c.MinGrade)) return e;
                    e.Result = Sat.No;
                    e.Failures.Add(PlannerReason.Of(ReasonCode.MinimumGradeNotMet, [c.CourseCode], c.MinGrade));
                }
                else if (st.InProgress.Contains(c.CourseCode) && st.AllowInProgress)
                {
                    e.Result = Sat.Conditional;
                    e.WaitingFor.Add(c.CourseCode);
                }
                else if (st.InProgress.Contains(c.CourseCode))
                {
                    // In-progress prerequisites are switched off (policy or this term): the grade must be posted first.
                    e.Result = Sat.No;
                    e.Failures.Add(PlannerReason.Of(ReasonCode.GradeMustBePosted, [c.CourseCode]));
                }
                else
                {
                    e.Result = Sat.No;
                    e.Failures.Add(st.Failed.Contains(c.CourseCode)
                        ? PlannerReason.Of(ReasonCode.PrerequisiteFailed, [c.CourseCode], c.MinGrade)
                        : PlannerReason.Of(ReasonCode.PrerequisiteNotMet, [c.CourseCode], c.ToText()));
                }

                return e;

            case Corequisite c:
                if (!st.Grades.ContainsKey(c.CourseCode) && !st.InProgress.Contains(c.CourseCode)) e.PendingCoreqs.Add(c.CourseCode);
                return e;

            case StandingAtLeast s:
                if (AcademicSummary.StandingFor(st.CreditsEarned) >= s.Standing) return e;
                e.Result = Sat.No;
                e.Failures.Add(PlannerReason.Of(ReasonCode.StandingRequired, s.Standing.ToString()));
                return e;

            case CreditsEarnedAtLeast cr:
                if (st.CreditsEarned >= cr.Credits) return e;
                e.Result = Sat.No;
                e.Failures.Add(PlannerReason.Of(ReasonCode.CreditsRequired, cr.Credits.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                return e;

            case DepartmentPermission:
                if (st.AssumePermission || st.Permissions.Contains(course)) return e;
                e.Result = Sat.No;
                e.Failures.Add(PlannerReason.Of(ReasonCode.PermissionRequired));
                return e;

            case AllOf all:
                foreach (var part in all.Items.Select(i => Evaluate(i, course, st)))
                {
                    e.Result = (Sat)Math.Min((int)e.Result, (int)part.Result);
                    e.Failures.AddRange(part.Failures);
                    e.WaitingFor.UnionWith(part.WaitingFor);
                    e.PendingCoreqs.UnionWith(part.PendingCoreqs);
                }

                return e;

            case AnyOf any:
            {
                var parts = any.Items.Select(i => Evaluate(i, course, st)).ToList();
                var best = parts.OrderByDescending(p => (int)p.Result).ThenBy(p => p.PendingCoreqs.Count).First();
                if (best.Result != Sat.No) return best;

                // None of the options holds: list the missing courses together ("pass A or B first").
                e.Result = Sat.No;
                var missing = parts.SelectMany(p => p.Failures).Where(f => f.Code == ReasonCode.PrerequisiteNotMet).SelectMany(f => f.Courses).Distinct().ToList();
                if (missing.Count > 0) e.Failures.Add(PlannerReason.Of(ReasonCode.PrerequisiteNotMet, missing, any.ToText()));
                e.Failures.AddRange(parts.SelectMany(p => p.Failures).Where(f => f.Code != ReasonCode.PrerequisiteNotMet).Distinct());
                return e;
            }

            default:
                return e;
        }
    }

    /// <summary>Transfer credit ("TR") and pass ("P") meet any minimum grade (assumption E1).</summary>
    private static bool MeetsMinimum(string grade, string minimum) =>
        string.Equals(grade, "TR", StringComparison.OrdinalIgnoreCase) || Grades.MeetsMinimum(grade, minimum);

    private static bool CanPartner(string partner, string forCourse, PlannerInput input, Dictionary<string, PlannerCourse> catalog,
        Dictionary<string, List<PlannerSection>> sections, State st)
    {
        if (!catalog.TryGetValue(partner, out var course)) return false;
        if (!course.Offered.Includes(input.Term.Season) || !sections.ContainsKey(partner)) return false;
        var e = Evaluate(course.Prerequisite, partner, st);
        // The partner may only be waiting on the course it pairs with.
        return e.Result != Sat.No && e.PendingCoreqs.All(p => string.Equals(p, forCourse, StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------------------------ step 5

    private static List<PlannerReason> SectionProblems(PlannerSection s, PlannerInput input, IEnumerable<(string Code, PlannerSection Section)> chosen)
    {
        var why = new List<PlannerReason>();
        var pref = input.Preferences.Delivery;
        if ((pref == DeliveryPreference.OnlineOnly && s.Delivery != DeliveryMode.Online)
            || (pref == DeliveryPreference.OnCampusOnly && s.Delivery == DeliveryMode.Online))
            why.Add(PlannerReason.Of(ReasonCode.DeliveryNotPreferred, s.Delivery.ToString()));
        if (input.Availability is { Windows.Count: > 0 } a && !a.Fits(s.Meetings))
            why.Add(PlannerReason.Of(ReasonCode.OutsideAvailability));
        if (s.SeatsLeft <= 0)
            why.Add(PlannerReason.Of(ReasonCode.SectionFull));
        var clashes = chosen.Where(c => c.Section.Meetings.Any(m => s.Meetings.Any(m.Overlaps))).Select(c => c.Code).Order(StringComparer.Ordinal).ToList();
        if (clashes.Count > 0)
            why.Add(PlannerReason.Of(ReasonCode.ClashesWith, clashes));
        return why;
    }

    /// <summary>First combination (one section per course) with no clashes between them, trying better sections first.</summary>
    private static Dictionary<string, PlannerSection>? FirstFittingCombination(List<string> courses, Dictionary<string, List<PlannerSection>> options)
    {
        var pick = new Dictionary<string, PlannerSection>(StringComparer.Ordinal);
        bool Search(int i)
        {
            if (i == courses.Count) return true;
            foreach (var s in options[courses[i]])
            {
                if (pick.Values.Any(p => p.Meetings.Any(m => s.Meetings.Any(m.Overlaps)))) continue;
                pick[courses[i]] = s;
                if (Search(i + 1)) return true;
                pick.Remove(courses[i]);
            }

            return false;
        }

        return Search(0) ? pick : null;
    }

    // ------------------------------------------------------------------------------ step 6

    private static TermProjection Project(PlannerInput input, Dictionary<string, PlannerCourse> catalog, List<Need> ordered,
        List<string> plannedNow, int plannedCredits, Dictionary<string, int> quotaAfterPlan, int freeRemaining, bool includeSummer)
    {
        var s = input.Student;
        var grades = new Dictionary<string, string>(s.Passed, StringComparer.OrdinalIgnoreCase);
        foreach (var c in s.InProgress) grades.TryAdd(c, "P"); // assumed passed
        foreach (var c in plannedNow) grades.TryAdd(c, "P");
        var earned = s.CreditsEarned + s.InProgress.Where(catalog.ContainsKey).Sum(c => catalog[c].Credits) + plannedCredits;

        var remaining = ordered.Where(n => !grades.ContainsKey(n.Code)).ToList();
        var quota = new Dictionary<string, int>(quotaAfterPlan, StringComparer.Ordinal);
        var free = freeRemaining;
        var terms = new List<ProjectedTerm>();
        if (plannedNow.Count > 0) terms.Add(new ProjectedTerm(input.Term.Code, plannedNow.Order(StringComparer.Ordinal).ToList(), plannedCredits, 0));

        bool Finished() => remaining.All(n => n.Kind == RequirementKind.ChooseCredits && quota.GetValueOrDefault(n.GroupKey) <= 0)
                           && quota.Values.All(q => q <= 0) && free <= 0;

        if (Finished())
            return new TermProjection(terms.LastOrDefault()?.TermCode ?? (s.CreditsEarned > 0 ? input.Term.Code : null), terms.Count, terms);

        var (season, year) = (input.Term.Season, input.Term.Year);
        for (var step = 0; step < ProjectionHorizon; step++)
        {
            (season, year) = Next(season, year);
            // "Without summer" means no regular summer classes: a summer term is used only for courses that
            // exist only in summer (such as internships), otherwise the degree could never be finished.
            var summerOnlyCourses = !includeSummer && season == TermSeason.Summer;

            var gpaAssumed = s.Gpa;
            var left = remaining.Where(n => n.Kind == RequirementKind.AllOf).Sum(n => n.Credits) + quota.Values.Where(q => q > 0).Sum() + Math.Max(0, free);
            var limits = input.Policy.LimitsFor(season, AcademicSummary.StandingFor(earned), gpaAssumed, s.OnProbation, left);
            var max = Math.Min(limits?.MaxCredits ?? 0, input.Preferences.MaxCredits ?? int.MaxValue);

            var state = new State(grades, new HashSet<string>(), new HashSet<string>(), earned, new HashSet<string>(), false, AssumePermission: true);
            var taken = new List<string>();
            var credits = 0;
            foreach (var n in remaining)
            {
                if (taken.Contains(n.Code) || !n.Offered.Includes(season)) continue;
                if (summerOnlyCourses && n.Offered != OfferedIn.Summer) continue;
                if (n.Kind == RequirementKind.ChooseCredits && quota.GetValueOrDefault(n.GroupKey) <= 0) continue;
                var e = Evaluate(n.Rule, n.Code, state);
                if (e.Result == Sat.No) continue;

                var partners = e.PendingCoreqs.Where(p => !taken.Contains(p)).ToList();
                if (partners.Any(p => !catalog.TryGetValue(p, out var pc) || !pc.Offered.Includes(season)
                                      || Evaluate(pc.Prerequisite, p, state) is { Result: Sat.No })) continue;
                var groupCredits = n.Credits + partners.Sum(p => catalog[p].Credits);
                if (credits + groupCredits > max) continue;

                foreach (var code in new[] { n.Code }.Concat(partners))
                {
                    taken.Add(code);
                    var need = remaining.FirstOrDefault(x => x.Code == code);
                    if (need?.Kind == RequirementKind.ChooseCredits) quota[need.GroupKey] = quota.GetValueOrDefault(need.GroupKey) - need.Credits;
                }

                credits += groupCredits;
            }

            var electives = summerOnlyCourses ? 0 : Math.Max(0, Math.Min(free, max - credits));
            free -= electives;
            foreach (var t in taken) grades.TryAdd(t, "P");
            remaining = remaining.Where(n => !taken.Contains(n.Code)).ToList();
            earned += credits + electives;

            var code2 = TermCodes.Format(year, season);
            if (credits + electives > 0) terms.Add(new ProjectedTerm(code2, taken.Order(StringComparer.Ordinal).ToList(), credits + electives, electives));
            if (Finished()) return new TermProjection(terms.Last().TermCode, terms.Count, terms);
        }

        return new TermProjection(null, terms.Count, terms);
    }

    private static (TermSeason Season, int Year) Next(TermSeason season, int year) => season switch
    {
        TermSeason.Spring => (TermSeason.Summer, year),
        TermSeason.Summer => (TermSeason.Fall, year),
        _ => (TermSeason.Spring, year + 1),
    };

    // ------------------------------------------------------------------------------ helpers

    private static int SeasonCount(OfferedIn o) => (o.HasFlag(OfferedIn.Fall) ? 1 : 0) + (o.HasFlag(OfferedIn.Spring) ? 1 : 0) + (o.HasFlag(OfferedIn.Summer) ? 1 : 0);

    private static string SeasonsText(OfferedIn o) =>
        string.Join(";", new[] { (OfferedIn.Fall, "Fall"), (OfferedIn.Spring, "Spring"), (OfferedIn.Summer, "Summer") }.Where(x => o.HasFlag(x.Item1)).Select(x => x.Item2));
}
