namespace Liwa.Domain.Academic.Prerequisites;

/// <summary>
/// Checks the catalog's prerequisite rules as a graph (brief §7: seed validation catches cycles).
/// Only "must have passed" edges count: corequisite pairs (a lecture and its lab) point at each other by design.
/// </summary>
public static class PrerequisiteGraph
{
    /// <summary>The courses a rule says must be passed first (corequisites excluded).</summary>
    public static IEnumerable<string> PassedFirst(Prerequisite? rule) => rule switch
    {
        CoursePassed c => [c.CourseCode],
        AllOf a => a.Items.SelectMany(PassedFirst),
        AnyOf o => o.Items.SelectMany(PassedFirst),
        _ => [],
    };

    /// <summary>
    /// Returns one prerequisite cycle as a path (e.g. [A, B, C, A]), or null when there is none. Deterministic:
    /// courses are visited in code order.
    /// </summary>
    public static IReadOnlyList<string>? FindCycle(IReadOnlyDictionary<string, Prerequisite?> rules)
    {
        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); // 1 = on the stack, 2 = done
        var stack = new List<string>();

        List<string>? Visit(string code)
        {
            if (state.TryGetValue(code, out var s))
                return s == 1 ? [.. stack.SkipWhile(x => !string.Equals(x, code, StringComparison.OrdinalIgnoreCase)), code] : null;
            state[code] = 1;
            stack.Add(code);
            if (rules.TryGetValue(code, out var rule))
                foreach (var next in PassedFirst(rule).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal))
                    if (Visit(next) is { } found) return found;
            stack.RemoveAt(stack.Count - 1);
            state[code] = 2;
            return null;
        }

        foreach (var code in rules.Keys.Order(StringComparer.Ordinal))
            if (Visit(code) is { } cycle) return cycle;
        return null;
    }
}
