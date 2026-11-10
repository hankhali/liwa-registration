using Liwa.Application.Explainer;
using Liwa.Domain.Planning;

namespace Liwa.UnitTests.Explainer;

/// <summary>Grounding enforced in code (brief §12): what may go to the model, how questions are cleaned, which replies are thrown away.</summary>
public sealed class ExplainerGuardTests
{
    private static readonly string[] Personal = ["Aisha Al Nuaimi", "202500103", "202500103@lu.ac.ae", "Dr. Ahmed Al Zaabi"];

    private static ExplanationPayload Payload(string language = "en") => new(
        language, "Spring 2027", 15, 12, 21,
        [
            new PayloadCourse("ITC201", "Programming II", 3, "Planned", [PlannerReason.Of(ReasonCode.UnlocksOtherCourses, ["ITC301"], "4")]),
            new PayloadCourse("CSC201", "Data Structures", 3, "Planned", [PlannerReason.Of(ReasonCode.WaitingForGrade, ["CSC201", "CSC102"])]),
        ],
        [new PayloadCourse("ITC401", "Capstone Project I", 3, "NotEligible", [PlannerReason.Of(ReasonCode.StandingRequired, "Senior")])],
        [PlannerReason.Of(ReasonCode.BelowMinimumCredits, "12")],
        new PayloadProjection("2029SP", 5, "2028FA", 4));

    [Fact]
    public void A_clean_payload_is_allowed() =>
        Assert.True(ExplainerGuard.PayloadIsSafe(Payload(), Personal, out _));

    [Theory]
    [InlineData("Aisha")]
    [InlineData("202500103")]
    [InlineData("someone@lu.ac.ae")]
    [InlineData("Al Zaabi")]
    public void A_payload_with_personal_data_is_refused(string leak)
    {
        var p = Payload() with { Planned = [new PayloadCourse("ITC201", $"Programming II for {leak}", 3, "Planned", [])] };
        Assert.False(ExplainerGuard.PayloadIsSafe(p, Personal, out var reason));
        Assert.NotNull(reason);
    }

    [Fact]
    public void Names_ids_and_emails_are_stripped_from_questions()
    {
        var q = ExplainerGuard.StripQuestion("I'm Aisha (202500103, 202500103@lu.ac.ae): can Dr. Ahmed Al Zaabi approve ITC401?", Personal, "en");
        Assert.DoesNotContain("Aisha", q, StringComparison.Ordinal);
        Assert.DoesNotContain("202500103", q, StringComparison.Ordinal);
        Assert.DoesNotContain("@", q, StringComparison.Ordinal);
        Assert.DoesNotContain("Zaabi", q, StringComparison.Ordinal);
        Assert.Contains("ITC401", q, StringComparison.Ordinal); // course codes stay
    }

    [Fact]
    public void A_grounded_reply_passes() =>
        Assert.Equal(ReplyVerdict.Ok, ExplainerGuard.CheckReply(
            "Your plan starts with ITC201 because 4 other courses depend on it. CSC201 can go ahead if you pass CSC102. You could graduate in Spring 2029.", Payload(), "en"));

    [Fact]
    public void A_reply_naming_a_course_not_in_the_payload_is_discarded() =>
        Assert.Equal(ReplyVerdict.UnknownCourse, ExplainerGuard.CheckReply("You should also take MGT999 this semester.", Payload(), "en"));

    [Fact]
    public void A_reply_saying_a_blocked_course_can_be_taken_is_discarded() =>
        Assert.Equal(ReplyVerdict.Contradiction, ExplainerGuard.CheckReply("Good news: you can take ITC401 this semester.", Payload(), "en"));

    [Fact]
    public void A_conditional_mention_of_a_blocked_course_is_fine() =>
        Assert.Equal(ReplyVerdict.Ok, ExplainerGuard.CheckReply("You can take ITC401 once you are a senior.", Payload(), "en"));

    [Fact]
    public void A_graduation_date_the_planner_did_not_give_is_discarded() =>
        Assert.Equal(ReplyVerdict.Contradiction, ExplainerGuard.CheckReply("You will graduate in Fall 2031.", Payload(), "en"));

    [Theory]
    [InlineData("ar", "Your plan starts with ITC201.")]
    [InlineData("en", "تبدأ خطتك بمقرر ITC201.")]
    public void A_reply_in_the_wrong_language_is_discarded(string language, string reply) =>
        Assert.Equal(ReplyVerdict.WrongLanguage, ExplainerGuard.CheckReply(reply, Payload(language), language));

    [Fact]
    public void An_arabic_reply_with_latin_course_codes_passes() =>
        Assert.Equal(ReplyVerdict.Ok, ExplainerGuard.CheckReply("تبدأ خطتك بمقرر ITC201 لأن أربعة مقررات أخرى تعتمد عليه.", Payload("ar"), "ar"));

    [Theory]
    [InlineData("What if I drop STA201?", "drop", "STA201")]
    [InlineData("can I remove csc201", "drop", "CSC201")]
    [InlineData("What if I skip summer?", "summer", null)]
    [InlineData("ماذا لو لم أدرس في الصيف؟", "summer", null)]
    [InlineData("Why can't I take ITC401?", "why", "ITC401")]
    [InlineData("لماذا لا يمكنني أخذ ITC401؟", "why", "ITC401")]
    [InlineData("What if I add MGT230", "add", "MGT230")]
    [InlineData("Explain my plan", "explain", null)]
    public void What_if_questions_are_recognised_without_the_model(string question, string kind, string? course)
    {
        var i = ExplainerGuard.ParseIntent(question);
        Assert.NotNull(i);
        Assert.Equal(kind, i.Kind);
        Assert.Equal(course, i.Course);
    }

    [Fact]
    public void Other_questions_go_to_the_classifier() => Assert.Null(ExplainerGuard.ParseIntent("What's the weather like?"));

    [Fact]
    public void The_classifiers_answer_is_checked_by_code()
    {
        var known = new HashSet<string>(["STA201"]);
        Assert.Equal(new QuestionIntent("drop", "STA201"), ExplainerGuard.ParseClassification("Sure! {\"intent\":\"drop\",\"course\":\"sta201\"}", known));
        Assert.Null(ExplainerGuard.ParseClassification("{\"intent\":\"drop\",\"course\":\"XYZ999\"}", known)); // unknown course
        Assert.Null(ExplainerGuard.ParseClassification("{\"intent\":\"book a flight\"}", known));
        Assert.Null(ExplainerGuard.ParseClassification("no json here", known));
    }

    [Theory]
    [InlineData("2029SP", "2029FA", false, 1)]
    [InlineData("2029SP", "2029FA", true, 2)]
    [InlineData("2029FA", "2029SP", false, -1)]
    [InlineData("2029SP", "2029SP", false, 0)]
    public void Semester_deltas_count_regular_semesters(string before, string after, bool summer, int delta) =>
        Assert.Equal(delta, ExplainerGuard.SemesterDelta(before, after, summer));
}
