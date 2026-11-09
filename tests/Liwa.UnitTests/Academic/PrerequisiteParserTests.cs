using Liwa.Domain.Academic;
using Liwa.Domain.Academic.Prerequisites;

namespace Liwa.UnitTests.Academic;

public sealed class PrerequisiteParserTests
{
    [Fact]
    public void Parses_the_example_from_the_brief()
    {
        var p = PrerequisiteParser.Parse("CSC101 AND (MTH102 OR MTH110:C)");

        var and = Assert.IsType<AllOf>(p);
        Assert.Equal(new CoursePassed("CSC101", null), and.Items[0]);
        var or = Assert.IsType<AnyOf>(and.Items[1]);
        Assert.Equal([new CoursePassed("MTH102", null), new CoursePassed("MTH110", "C")], or.Items);
    }

    [Fact]
    public void And_binds_tighter_than_or()
    {
        var p = Assert.IsType<AnyOf>(PrerequisiteParser.Parse("ACC100 AND BUS100 OR CSC100"));
        Assert.IsType<AllOf>(p.Items[0]);
        Assert.Equal(new CoursePassed("CSC100", null), p.Items[1]);
    }

    [Theory]
    [InlineData("COREQ(ITC110L)", "COREQ(ITC110L)")]
    [InlineData("standing(junior)", "STANDING(Junior)")]
    [InlineData("CREDITS(90)", "CREDITS(90)")]
    [InlineData("permission", "PERMISSION")]
    [InlineData("mcm201:c", "MCM201:C")]
    [InlineData("ITC301 AND STANDING(Senior)", "ITC301 AND STANDING(Senior)")]
    [InlineData("(ACC100 OR BUS100) AND CSC100", "(ACC100 OR BUS100) AND CSC100")]
    [InlineData("ACC100 AND (BUS100 AND CSC100)", "ACC100 AND BUS100 AND CSC100")]
    [InlineData("CREDITS(90) AND PERMISSION", "CREDITS(90) AND PERMISSION")]
    public void Every_condition_type_round_trips_to_canonical_text(string input, string canonical)
    {
        var parsed = PrerequisiteParser.Parse(input)!;
        Assert.Equal(canonical, parsed.ToText());
        Assert.Equal(parsed, PrerequisiteParser.Parse(parsed.ToText()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_text_means_no_prerequisites(string? text) => Assert.Null(PrerequisiteParser.Parse(text));

    [Fact]
    public void Lists_every_course_mentioned()
    {
        var p = PrerequisiteParser.Parse("ITC202 AND ITC220 OR COREQ(ITC202L)")!;
        Assert.Equal(["ITC202", "ITC220", "ITC202L"], p.CourseCodes());
    }

    [Theory]
    [InlineData("CSC101 AND", "end of text")]
    [InlineData("(CSC101 OR MTH101", "')'")]
    [InlineData("CSC101 OR OR MTH101", "both sides")]
    [InlineData("CSC101:Z", "not a letter grade")]
    [InlineData("STANDING(Expert)", "not a standing")]
    [InlineData("CREDITS(lots)", "not a number")]
    [InlineData("HELLO", "not a course code")]
    [InlineData("CSC101 & MTH101", "Unexpected character")]
    [InlineData("CSC101 MTH101", "Unexpected 'MTH101'")]
    public void Mistakes_get_a_plain_message_with_a_position(string text, string expected)
    {
        Assert.False(PrerequisiteParser.TryParse(text, out _, out var error));
        Assert.Contains(expected, error, StringComparison.Ordinal);
        Assert.Contains("at character", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Standing_levels_are_ordered() => Assert.True(Standing.Senior > Standing.Junior);
}
