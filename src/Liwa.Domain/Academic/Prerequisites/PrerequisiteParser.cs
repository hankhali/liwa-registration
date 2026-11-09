namespace Liwa.Domain.Academic.Prerequisites;

/// <summary>
/// Recursive-descent parser for prerequisite text. Grammar:
/// <code>
/// or      := and ("OR" and)*
/// and     := primary ("AND" primary)*
/// primary := "(" or ")" | COURSE [":" GRADE] | "COREQ" "(" COURSE ")" | "STANDING" "(" LEVEL ")"
///          | "CREDITS" "(" NUMBER ")" | "PERMISSION"
/// </code>
/// </summary>
public static class PrerequisiteParser
{
    /// <summary>Returns null for empty text (no prerequisites).</summary>
    public static Prerequisite? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var p = new State(Tokenize(text));
        var result = p.ParseOr();
        if (!p.AtEnd) throw new PrerequisiteSyntaxException($"Unexpected '{p.Peek.Text}'.", p.Peek.Position);
        return result;
    }

    public static bool TryParse(string? text, out Prerequisite? result, out string? error)
    {
        try
        {
            result = Parse(text);
            error = null;
            return true;
        }
        catch (PrerequisiteSyntaxException ex)
        {
            result = null;
            error = $"{ex.Message} (at character {ex.Position + 1})";
            return false;
        }
    }

    private enum Kind { Word, LParen, RParen, Colon, End }

    private sealed record Token(Kind Kind, string Text, int Position);

    private static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            switch (c)
            {
                case '(': tokens.Add(new Token(Kind.LParen, "(", i++)); continue;
                case ')': tokens.Add(new Token(Kind.RParen, ")", i++)); continue;
                case ':': tokens.Add(new Token(Kind.Colon, ":", i++)); continue;
            }

            if (char.IsLetterOrDigit(c) || c is '+' or '-')
            {
                var start = i;
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] is '+' or '-')) i++;
                tokens.Add(new Token(Kind.Word, text[start..i].ToUpperInvariant(), start));
                continue;
            }

            throw new PrerequisiteSyntaxException($"Unexpected character '{c}'.", i);
        }

        tokens.Add(new Token(Kind.End, "end of text", text.Length));
        return tokens;
    }

    private sealed class State(List<Token> tokens)
    {
        private int _index;

        public Token Peek => tokens[_index];

        public bool AtEnd => Peek.Kind == Kind.End;

        private Token Next() => tokens[_index++];

        private bool IsWord(string word) => Peek.Kind == Kind.Word && Peek.Text == word;

        private Token Expect(Kind kind, string what)
        {
            if (Peek.Kind != kind) throw new PrerequisiteSyntaxException($"Expected {what} but found '{Peek.Text}'.", Peek.Position);
            return Next();
        }

        public Prerequisite ParseOr()
        {
            var items = new List<Prerequisite> { ParseAnd() };
            while (IsWord("OR")) { Next(); items.Add(ParseAnd()); }
            return items.Count == 1 ? items[0] : new AnyOf(Flatten<AnyOf>(items));
        }

        private Prerequisite ParseAnd()
        {
            var items = new List<Prerequisite> { ParsePrimary() };
            while (IsWord("AND")) { Next(); items.Add(ParsePrimary()); }
            return items.Count == 1 ? items[0] : new AllOf(Flatten<AllOf>(items));
        }

        private Prerequisite ParsePrimary()
        {
            var t = Peek;
            if (t.Kind == Kind.LParen)
            {
                Next();
                var inner = ParseOr();
                Expect(Kind.RParen, "')'");
                return inner;
            }

            if (t.Kind != Kind.Word) throw new PrerequisiteSyntaxException($"Expected a course or condition but found '{t.Text}'.", t.Position);
            Next();

            switch (t.Text)
            {
                case "AND" or "OR":
                    throw new PrerequisiteSyntaxException($"'{t.Text}' needs something on both sides.", t.Position);
                case "PERMISSION":
                    return new DepartmentPermission();
                case "COREQ":
                    return new Corequisite(Course(Argument()));
                case "STANDING":
                {
                    var arg = Argument();
                    if (!Enum.TryParse<Standing>(arg.Text, ignoreCase: true, out var standing) || int.TryParse(arg.Text, out _))
                        throw new PrerequisiteSyntaxException($"'{arg.Text}' is not a standing (Freshman, Sophomore, Junior, Senior).", arg.Position);
                    return new StandingAtLeast(standing);
                }
                case "CREDITS":
                {
                    var arg = Argument();
                    if (!int.TryParse(arg.Text, out var credits) || credits is < 0 or > 300)
                        throw new PrerequisiteSyntaxException($"'{arg.Text}' is not a number of credits.", arg.Position);
                    return new CreditsEarnedAtLeast(credits);
                }
            }

            var code = Course(t);
            if (Peek.Kind != Kind.Colon) return new CoursePassed(code, null);
            Next();
            var grade = Expect(Kind.Word, "a grade after ':'");
            if (!Grades.IsLetterGrade(grade.Text))
                throw new PrerequisiteSyntaxException($"'{grade.Text}' is not a letter grade.", grade.Position);
            return new CoursePassed(code, grade.Text);
        }

        private Token Argument()
        {
            Expect(Kind.LParen, "'('");
            var arg = Expect(Kind.Word, "a value");
            Expect(Kind.RParen, "')'");
            return arg;
        }

        private static string Course(Token t) =>
            Academic.Course.IsValidCode(t.Text)
                ? t.Text
                : throw new PrerequisiteSyntaxException($"'{t.Text}' is not a course code.", t.Position);

        private static List<Prerequisite> Flatten<T>(List<Prerequisite> items) where T : Prerequisite =>
            items.SelectMany(i => i switch
            {
                AllOf a when typeof(T) == typeof(AllOf) => a.Items,
                AnyOf a when typeof(T) == typeof(AnyOf) => a.Items,
                _ => [i],
            }).ToList();
    }
}
