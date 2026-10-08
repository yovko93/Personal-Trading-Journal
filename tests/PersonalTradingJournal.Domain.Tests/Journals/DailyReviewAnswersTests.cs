using PersonalTradingJournal.Domain.Journals;

namespace PersonalTradingJournal.Domain.Tests.Journals;

public sealed class DailyReviewAnswersTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(" \r\n\t", false)]
    [InlineData("— ...!?🙂", false)]
    [InlineData("\u200B\uFEFF\u0301", false)]
    [InlineData("Follow plan", true)]
    [InlineData("É 文", true)]
    [InlineData("٢", true)]
    [InlineData("\U00010400", true)]
    public void MeaningfulTextRequiresUnicodeLetterOrDigit(string? answer, bool meaningful)
    {
        Assert.Equal(meaningful, DailyReviewAnswers.HasMeaningfulText(answer));
    }

    [Fact]
    public void ExactAnswersAndLineEndingsRemainUntouched()
    {
        const string wentWell = "  café 文 🙂\r\n good\n";
        var review = new DailyReviewAnswers(wentWell, "  improve\t", "\r\nnext ");

        Assert.Equal(wentWell, review.WentWell);
        Assert.Equal("  improve\t", review.NeedsImprovement);
        Assert.Equal("\r\nnext ", review.NextTradingDay);
        Assert.True(review.HasMeaningfulContent);
        Assert.False(DailyReviewAnswers.Empty.HasMeaningfulContent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EachAnswerHasIndependentExactUtf16LimitAndRejectsNull(int answerIndex)
    {
        string[] answers = ["Well", "Improve", "Next"];
        answers[answerIndex] = new string('x', DailyReviewAnswers.MaximumAnswerLength);
        DailyReviewAnswers review = Make(answers);
        Assert.True(review.HasMeaningfulContent);

        answers[answerIndex] += "x";
        Assert.Throws<ArgumentOutOfRangeException>(() => Make(answers));
        answers[answerIndex] = null!;
        Assert.Throws<ArgumentNullException>(() => Make(answers));
    }

    private static DailyReviewAnswers Make(string[] answers) => new(answers[0], answers[1], answers[2]);
}
