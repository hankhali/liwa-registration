using System.Text;
using Liwa.Application.Imports;

namespace Liwa.UnitTests.Imports;

public sealed class CsvTableTests
{
    [Fact]
    public void Reads_headers_case_insensitively_and_trims_values()
    {
        var t = CsvTable.Parse("Course_Code, Title \nITC201 , Database Systems\n");
        Assert.Equal(["course_code", "title"], t.Headers);
        Assert.Equal("ITC201", t.Rows[0]["course_code"]);
        Assert.Equal("Database Systems", t.Rows[0]["title"]);
    }

    [Fact]
    public void Quoted_values_may_contain_commas_quotes_and_line_breaks_and_line_numbers_stay_right()
    {
        var t = CsvTable.Parse("code,description\nA100,\"Hello, \"\"world\"\"\nsecond line\"\nB100,plain\n");
        Assert.Equal("Hello, \"world\"\nsecond line", t.Rows[0]["description"]);
        Assert.Equal(2, t.Rows[0].Line);
        Assert.Equal(4, t.Rows[1].Line);
    }

    [Fact]
    public void Utf8_with_bom_and_arabic_text_work()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("code,title_ar\r\nITC201,نظم قواعد البيانات\r\n")).ToArray();
        var t = CsvTable.Parse(new MemoryStream(bytes));
        Assert.Equal("code", t.Headers[0]);
        Assert.Equal("نظم قواعد البيانات", t.Rows[0]["title_ar"]);
    }

    [Fact]
    public void Blank_lines_are_skipped_and_blank_values_read_as_null()
    {
        var t = CsvTable.Parse("a,b\n\n1,\n,,\n");
        Assert.Single(t.Rows);
        Assert.Null(t.Rows[0]["b"]);
        Assert.Null(t.Rows[0]["missing"]);
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("a,a\n1,2", "more than once")]
    [InlineData("a,\n1,2", "blank")]
    [InlineData("a,b\n\"1,2", "never closed")]
    [InlineData("a,b\n1,2,3", "only 2 columns")]
    public void Broken_files_get_a_plain_message(string text, string expected)
    {
        var ex = Assert.Throws<CsvFormatException>(() => CsvTable.Parse(text));
        Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Row_reader_collects_every_problem_in_a_row()
    {
        var table = CsvTable.Parse("credits,days,start,current\nlots,Mon;Sun;Xyz,25:00,maybe\n");
        var report = new ImportReport(ImportKind.Sections, dryRun: true);
        var r = new RowReader(table.Rows[0], report);

        r.RequiredInt("credits", 0, 6);
        var days = r.Days("days");
        r.OptionalTime("start");
        r.Bool("current", false);
        r.Required("missing");

        Assert.False(r.Ok);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Sunday], days);
        Assert.Equal(5, report.ErrorCount);
        Assert.All(report.Issues, i => Assert.Equal(2, i.Line));
    }

    [Theory]
    [InlineData("MW", new[] { DayOfWeek.Monday, DayOfWeek.Wednesday })]
    [InlineData("TR", new[] { DayOfWeek.Tuesday, DayOfWeek.Thursday })]
    [InlineData("Mon Wed Fri", new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday })]
    [InlineData("monday;thursday", new[] { DayOfWeek.Monday, DayOfWeek.Thursday })]
    public void Days_accept_common_spellings(string text, DayOfWeek[] expected)
    {
        var table = CsvTable.Parse($"days\n{text}\n");
        var r = new RowReader(table.Rows[0], new ImportReport(ImportKind.Sections, true));
        Assert.Equal(expected, r.Days("days"));
        Assert.True(r.Ok);
    }
}
