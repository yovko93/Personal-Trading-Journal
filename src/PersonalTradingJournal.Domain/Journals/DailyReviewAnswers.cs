using System.Text;

namespace PersonalTradingJournal.Domain.Journals;

/// <summary>Exact answers to the three Daily Review questions; drafts may be partially answered.</summary>
public sealed record DailyReviewAnswers
{
    /// <summary>The maximum length of each answer, measured in UTF-16 code units.</summary>
    public const int MaximumAnswerLength = 100_000;

    public static DailyReviewAnswers Empty { get; } = new(string.Empty, string.Empty, string.Empty);

    public DailyReviewAnswers(string wentWell, string needsImprovement, string nextTradingDay)
    {
        ValidateAnswer(wentWell, nameof(wentWell));
        ValidateAnswer(needsImprovement, nameof(needsImprovement));
        ValidateAnswer(nextTradingDay, nameof(nextTradingDay));
        WentWell = wentWell;
        NeedsImprovement = needsImprovement;
        NextTradingDay = nextTradingDay;
    }

    public string WentWell { get; }

    public string NeedsImprovement { get; }

    public string NextTradingDay { get; }

    public bool CanComplete =>
        HasMeaningfulText(WentWell) &&
        HasMeaningfulText(NeedsImprovement) &&
        HasMeaningfulText(NextTradingDay);

    /// <summary>An answer must contain a Unicode letter or digit, not only whitespace or punctuation.</summary>
    public static bool HasMeaningfulText(string? text)
    {
        if (text is null)
        {
            return false;
        }

        foreach (Rune rune in text.EnumerateRunes())
        {
            if (Rune.IsLetterOrDigit(rune))
            {
                return true;
            }
        }

        return false;
    }

    private static void ValidateAnswer(string answer, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(answer, parameterName);
        if (answer.Length > MaximumAnswerLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName, $"Each review answer cannot exceed {MaximumAnswerLength} UTF-16 code units.");
        }
    }
}
