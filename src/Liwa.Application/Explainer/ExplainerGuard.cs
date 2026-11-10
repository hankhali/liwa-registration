using System.Text.Json;
using System.Text.RegularExpressions;
using Liwa.Domain.Planning;

namespace Liwa.Application.Explainer;

public enum ReplyVerdict
{
    Ok = 1,
    UnknownCourse = 2,
    Contradiction = 3,
    WrongLanguage = 4,
    Empty = 5,
}

/// <summary>A what-if question, recognised by the rules or by the model's one classification call.</summary>
public sealed record QuestionIntent(string Kind, string? Course)
{
    public static readonly IReadOnlySet<string> Kinds = new HashSet<string>(StringComparer.Ordinal) { "drop", "add", "summer", "why", "explain" };
}

/// <summary>
/// Grounding enforced in code (brief §12), not in the prompt: what may go to the model, how questions are cleaned,
/// and which replies are thrown away. Pure and deterministic, so it is unit-tested directly.
/// </summary>
public static partial class ExplainerGuard
{
    public const string FallbackEn = "I can't explain that part — ask your advisor.";
    public const string FallbackAr = "لا أستطيع شرح هذا الجزء — اسأل مرشدك.";

    public static string Fallback(string language) => language == "ar" ? FallbackAr : FallbackEn;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [GeneratedRegex(@"\b[A-Z]{2,4}\d{3}L?\b")]
    private static partial Regex CourseCode();

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex Email();

    /// <summary>University IDs and staff IDs (e.g. 202500103, E1001).</summary>
    [GeneratedRegex(@"\b(\d{9}|E\d{4})\b")]
    private static partial Regex UniversityId();

    [GeneratedRegex(@"[؀-ۿ]")]
    private static partial Regex ArabicLetter();

    [GeneratedRegex(@"[A-Za-z]")]
    private static partial Regex LatinLetter();

    [GeneratedRegex(@"\b(Fall|Spring|Summer)\s+(20\d{2})\b", RegexOptions.IgnoreCase)]
    private static partial Regex TermMention();

    /// <summary>
    /// Refuses the payload if it carries anything personal: one of the person's names, an email address, or a
    /// university/staff ID. The model is told about "you" / "the student" only.
    /// </summary>
    public static bool PayloadIsSafe(ExplanationPayload payload, IEnumerable<string> forbiddenNames, out string? reason)
    {
        var text = JsonSerializer.Serialize(payload, Json);
        foreach (var name in forbiddenNames.Where(n => !string.IsNullOrWhiteSpace(n)).SelectMany(NameParts))
            if (text.Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                reason = "name";
                return false;
            }

        if (Email().IsMatch(text)) { reason = "email"; return false; }
        if (UniversityId().IsMatch(text)) { reason = "id"; return false; }
        reason = null;
        return true;
    }

    /// <summary>Names, IDs and emails are removed from a question before it goes anywhere.</summary>
    public static string StripQuestion(string question, IEnumerable<string> forbiddenNames, string language)
    {
        var replacement = language == "ar" ? "الطالب" : "the student";
        var q = Email().Replace(question ?? "", replacement);
        q = UniversityId().Replace(q, replacement);
        foreach (var part in forbiddenNames.Where(n => !string.IsNullOrWhiteSpace(n)).SelectMany(NameParts).OrderByDescending(p => p.Length))
            q = Regex.Replace(q, $@"\b{Regex.Escape(part)}\b", replacement, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        return q.Length > 500 ? q[..500] : q;
    }

    /// <summary>Full names and the parts long enough to identify someone ("Aisha", "Al Nuaimi"), never "Al" or "Dr".</summary>
    private static IEnumerable<string> NameParts(string name)
    {
        var clean = name.Replace("Dr.", "", StringComparison.OrdinalIgnoreCase).Trim();
        yield return clean;
        foreach (var p in clean.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(p => p.Length >= 4)) yield return p;
    }

    /// <summary>The course codes the model may mention: the ones in the payload.</summary>
    public static IReadOnlySet<string> AllowedCodes(ExplanationPayload p)
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(PayloadCourse? c)
        {
            if (c is null) return;
            codes.Add(c.Code);
            foreach (var r in c.Reasons) codes.UnionWith(r.Courses);
        }

        foreach (var c in p.Planned) Add(c);
        foreach (var c in p.NotEligible) Add(c);
        Add(p.Focus);
        foreach (var w in p.Warnings) codes.UnionWith(w.Courses);
        if (p.WhatIf?.Course is { } wc) codes.Add(wc);
        foreach (var r in p.WhatIf?.NewProblems ?? []) codes.UnionWith(r.Courses);
        return codes;
    }

    /// <summary>
    /// Discards a reply that names a course not in the payload, contradicts the planner (says a not-eligible course
    /// can be taken, or gives a graduation term the projection doesn't have), or is in the wrong language.
    /// </summary>
    public static ReplyVerdict CheckReply(string reply, ExplanationPayload p, string language)
    {
        if (string.IsNullOrWhiteSpace(reply)) return ReplyVerdict.Empty;

        var allowed = AllowedCodes(p);
        if (CourseCode().Matches(reply).Any(m => !allowed.Contains(m.Value))) return ReplyVerdict.UnknownCourse;

        var arabic = ArabicLetter().Count(reply);
        var latin = LatinLetter().Count(CourseCode().Replace(reply, ""));
        if (language == "ar" && arabic < latin) return ReplyVerdict.WrongLanguage;
        if (language == "en" && arabic > 0) return ReplyVerdict.WrongLanguage;

        // A not-eligible course described as takeable, in the same sentence.
        var blocked = p.NotEligible.Select(c => c.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var sentence in Regex.Split(reply, @"(?<=[.!?؟])\s+", RegexOptions.None, TimeSpan.FromSeconds(1)))
        {
            var mentioned = CourseCode().Matches(sentence).Select(m => m.Value).Where(blocked.Contains).ToList();
            if (mentioned.Count == 0) continue;
            var lower = sentence.ToLowerInvariant();
            var says = lower.Contains("you can take", StringComparison.Ordinal) || lower.Contains("you are eligible", StringComparison.Ordinal)
                       || lower.Contains("you're eligible", StringComparison.Ordinal) || sentence.Contains("يمكنك أخذ", StringComparison.Ordinal)
                       || sentence.Contains("يمكنك تسجيل", StringComparison.Ordinal);
            var negated = lower.Contains("can't", StringComparison.Ordinal) || lower.Contains("cannot", StringComparison.Ordinal) || lower.Contains("not", StringComparison.Ordinal)
                          || lower.Contains(" if ", StringComparison.Ordinal) || lower.Contains("once", StringComparison.Ordinal) || lower.Contains("after", StringComparison.Ordinal)
                          || sentence.Contains("لا ", StringComparison.Ordinal) || sentence.Contains("بعد", StringComparison.Ordinal) || sentence.Contains("إذا", StringComparison.Ordinal);
            if (says && !negated) return ReplyVerdict.Contradiction;
        }

        // A graduation term the planner's projections don't have.
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var proj in new[] { p.Projection, p.WhatIf?.Before, p.WhatIf?.After })
            if (proj is not null)
            {
                if (proj.GraduationWithoutSummer is { } a) terms.Add(TermLabel(a));
                if (proj.GraduationWithSummer is { } b) terms.Add(TermLabel(b));
            }

        terms.Add(p.Term);
        foreach (Match m in TermMention().Matches(reply))
            if (terms.Count > 0 && !terms.Contains($"{Cap(m.Groups[1].Value)} {m.Groups[2].Value}") && LooksLikeGraduation(reply, m.Index))
                return ReplyVerdict.Contradiction;

        return ReplyVerdict.Ok;
    }

    private static bool LooksLikeGraduation(string reply, int index)
    {
        var start = Math.Max(0, index - 60);
        var window = reply[start..Math.Min(reply.Length, index + 20)].ToLowerInvariant();
        return window.Contains("graduat", StringComparison.Ordinal);
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();

    /// <summary>"2030SP" → "Spring 2030".</summary>
    public static string TermLabel(string code)
    {
        if (code.Length != 6) return code;
        var season = code[4..] switch { "SP" => "Spring", "SU" => "Summer", "FA" => "Fall", _ => code[4..] };
        return $"{season} {code[..4]}";
    }

    // ------------------------------------------------------------------ question intent

    /// <summary>
    /// Recognises what-if questions without the model: "drop STA201", "what if I skip summer", "why can't I take ITC301",
    /// "add MGT230", in English or Arabic. Null = not recognised (the model's single classification call decides).
    /// </summary>
    public static QuestionIntent? ParseIntent(string question)
    {
        var q = (question ?? "").Trim();
        var lower = q.ToLowerInvariant();
        var code = CourseCode().Match(q.ToUpperInvariant()) is { Success: true } m ? m.Value : null;

        if (lower.Contains("summer", StringComparison.Ordinal) || q.Contains("صيف", StringComparison.Ordinal)) return new("summer", null);
        if (code is not null)
        {
            if (Regex.IsMatch(lower, @"\b(drop|remove|skip|without|leave out)\b", RegexOptions.None, TimeSpan.FromSeconds(1)) || q.Contains("حذف", StringComparison.Ordinal) || q.Contains("أحذف", StringComparison.Ordinal) || q.Contains("دون", StringComparison.Ordinal) || q.Contains("بدون", StringComparison.Ordinal))
                return new("drop", code);
            if (Regex.IsMatch(lower, @"\b(add|include|take)\b", RegexOptions.None, TimeSpan.FromSeconds(1)) && !lower.Contains("why", StringComparison.Ordinal) && !lower.Contains("can't", StringComparison.Ordinal) && !lower.Contains("cannot", StringComparison.Ordinal)
                || q.Contains("أضيف", StringComparison.Ordinal) || q.Contains("إضافة", StringComparison.Ordinal))
                return new("add", code);
            if (lower.Contains("why", StringComparison.Ordinal) || q.Contains("لماذا", StringComparison.Ordinal) || lower.Contains("can't", StringComparison.Ordinal) || lower.Contains("cannot", StringComparison.Ordinal))
                return new("why", code);
        }

        if (Regex.IsMatch(lower, @"\b(explain|summari[sz]e|what does my plan)\b", RegexOptions.None, TimeSpan.FromSeconds(1)) || q.Contains("اشرح", StringComparison.Ordinal))
            return new("explain", null);
        return null;
    }

    /// <summary>Validates the model's classification reply ({"intent":"drop","course":"STA201"}); anything else is null.</summary>
    public static QuestionIntent? ParseClassification(string reply, IReadOnlySet<string> knownCourses)
    {
        try
        {
            var start = reply.IndexOf('{', StringComparison.Ordinal);
            var end = reply.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            using var doc = JsonDocument.Parse(reply[start..(end + 1)]);
            var kind = doc.RootElement.TryGetProperty("intent", out var i) ? i.GetString() : null;
            var course = doc.RootElement.TryGetProperty("course", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString()?.ToUpperInvariant() : null;
            if (kind is null || !QuestionIntent.Kinds.Contains(kind)) return null;
            if (kind is "drop" or "add" or "why" && (course is null || !knownCourses.Contains(course))) return null;
            return new QuestionIntent(kind, kind is "summer" or "explain" ? null : course);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>"Spring 2030" is one semester later than "Fall 2029" etc.; positive = later. Summer counts when asked.</summary>
    public static int SemesterDelta(string? before, string? after, bool countSummer)
    {
        static int? Ord(string? c) => c is { Length: 6 } && int.TryParse(c[..4], out var y)
            ? y * 3 + (c[4..] switch { "SP" => 0, "SU" => 1, _ => 2 })
            : null;
        var a = Ord(before);
        var b = Ord(after);
        if (a is null || b is null || a == b) return 0;
        var (lo, hi, sign) = a < b ? (a.Value, b.Value, 1) : (b.Value, a.Value, -1);
        var n = 0;
        for (var o = lo + 1; o <= hi; o++) if (countSummer || o % 3 != 1) n++;
        return sign * n;
    }

    public static PayloadProjection Projection(GraduationProjection g) =>
        new(g.WithoutSummer.GraduationTerm, g.WithoutSummer.TermsRemaining, g.WithSummer.GraduationTerm, g.WithSummer.TermsRemaining);
}
