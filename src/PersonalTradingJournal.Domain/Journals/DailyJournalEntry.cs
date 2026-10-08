using PersonalTradingJournal.Domain.Common;

namespace PersonalTradingJournal.Domain.Journals;

/// <summary>
/// A user's journal and Daily Review for one trading-calendar date and one exact Account scope.
/// A null Account identifies the independent all-accounts journal for that date.
/// </summary>
public sealed class DailyJournalEntry : AuditableEntity
{
    /// <summary>The maximum text length, measured in UTF-16 code units.</summary>
    public const int MaximumTextLength = 100_000;

    public DailyJournalEntry(
        DateOnly tradingDate,
        Guid? tradingAccountId,
        string text,
        bool isDraft,
        DateTimeOffset createdAtUtc)
        : this(tradingDate, tradingAccountId, text, isDraft, createdAtUtc, DailyReviewAnswers.Empty)
    {
    }

    public DailyJournalEntry(
        DateOnly tradingDate,
        Guid? tradingAccountId,
        string text,
        bool isDraft,
        DateTimeOffset createdAtUtc,
        DailyReviewAnswers review)
        : base(createdAtUtc)
    {
        ValidateAccountId(tradingAccountId);
        ValidateText(text);
        ArgumentNullException.ThrowIfNull(review);
        ValidateCompletion(isDraft, text, review);

        TradingDate = tradingDate;
        TradingAccountId = tradingAccountId;
        Text = text;
        Review = review;
        IsDraft = isDraft;
        Revision = 1;
    }

    public DailyJournalEntry(
        DateOnly tradingDate,
        Guid? tradingAccountId,
        string text,
        DateTimeOffset createdAtUtc)
        : this(tradingDate, tradingAccountId, text, isDraft: true, createdAtUtc)
    {
    }

    private DailyJournalEntry(
        Guid id,
        DateOnly tradingDate,
        Guid? tradingAccountId,
        string text,
        bool isDraft,
        long revision,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        DailyReviewAnswers review)
        : base(id, createdAtUtc, updatedAtUtc)
    {
        ValidateAccountId(tradingAccountId);
        ValidateText(text);
        ArgumentNullException.ThrowIfNull(review);
        if (revision < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(revision), revision, "The journal revision must be positive.");
        }

        TradingDate = tradingDate;
        TradingAccountId = tradingAccountId;
        Text = text;
        Review = review;
        IsDraft = isDraft;
        Revision = revision;
    }

    /// <summary>The selected New York calendar date, independent of audit timestamps.</summary>
    public DateOnly TradingDate { get; }

    public Guid? TradingAccountId { get; private set; }

    /// <summary>Exact plain text, including empty content, whitespace and line endings.</summary>
    public string Text { get; private set; }

    /// <summary>The exact structured answers stored with this revision.</summary>
    public DailyReviewAnswers Review { get; private set; }

    /// <summary>Committed state. A completed review requires explicit reopening to edit;
    /// opening an editor alone does not change this state.</summary>
    public bool IsDraft { get; private set; }

    public long Revision { get; private set; }

    public static DailyJournalEntry Rehydrate(
        Guid id,
        DateOnly tradingDate,
        Guid? tradingAccountId,
        string text,
        bool isDraft,
        long revision,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc,
        DailyReviewAnswers? review = null)
    {
        return new DailyJournalEntry(
            id, tradingDate, tradingAccountId, text, isDraft, revision,
            createdAtUtc, updatedAtUtc, review ?? DailyReviewAnswers.Empty);
    }

    public bool UpdateContent(string text, bool isDraft, DateTimeOffset updatedAtUtc)
        => UpdateContent(text, isDraft, updatedAtUtc, Review);

    /// <summary>Applies one revision. Explicit reopening may be combined atomically with the edit;
    /// callers must still enforce the loaded revision token before applying it.</summary>
    public bool UpdateContent(
        string text,
        bool isDraft,
        DateTimeOffset updatedAtUtc,
        DailyReviewAnswers review,
        bool reopenCompleted = false,
        DailyJournalAccountScope? targetScope = null)
    {
        ValidateText(text);
        ArgumentNullException.ThrowIfNull(review);
        Guid? accountId = targetScope is null ? TradingAccountId : targetScope.TradingAccountId;
        bool unchangedContent = Text == text && Review == review && TradingAccountId == accountId;
        if (unchangedContent && IsDraft == isDraft)
        {
            return false;
        }

        if (!IsDraft && !unchangedContent && !reopenCompleted)
        {
            throw new InvalidOperationException(
                "A completed review must be reopened before its journal text, answers or Account scope can be edited.");
        }

        ValidateCompletion(isDraft, text, review);
        long nextRevision = checked(Revision + 1);
        SetUpdatedAtUtc(updatedAtUtc);
        Text = text;
        Review = review;
        IsDraft = isDraft;
        TradingAccountId = accountId;
        Revision = nextRevision;
        return true;
    }

    /// <summary>Completion requires a Unicode letter or digit in Journal text; answers are optional.</summary>
    public static bool CanComplete(string? text) => DailyReviewAnswers.HasMeaningfulText(text);

    private static void ValidateCompletion(bool isDraft, string text, DailyReviewAnswers review)
    {
        if (!isDraft && !CanComplete(text))
        {
            throw new ArgumentException(
                "Journal text is required to save as Completed. Include at least one letter or digit; review answers are optional.",
                nameof(text));
        }
    }

    private static void ValidateAccountId(Guid? tradingAccountId)
    {
        if (tradingAccountId == Guid.Empty)
        {
            throw new ArgumentException(
                "A journal Account scope must be a non-empty identifier or null for all accounts.",
                nameof(tradingAccountId));
        }
    }

    private static void ValidateText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > MaximumTextLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(text), $"Journal text cannot exceed {MaximumTextLength} UTF-16 code units.");
        }
    }
}
