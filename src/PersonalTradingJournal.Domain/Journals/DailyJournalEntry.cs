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
        ValidateCompletion(isDraft, review);

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

    public Guid? TradingAccountId { get; }

    /// <summary>Exact plain text, including empty content, whitespace and line endings.</summary>
    public string Text { get; private set; }

    /// <summary>The exact structured answers stored with this revision.</summary>
    public DailyReviewAnswers Review { get; private set; }

    /// <summary>A completed review is read-only until explicitly reopened as a draft.</summary>
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

    public bool UpdateContent(
        string text,
        bool isDraft,
        DateTimeOffset updatedAtUtc,
        DailyReviewAnswers review)
    {
        ValidateText(text);
        ArgumentNullException.ThrowIfNull(review);
        bool unchangedContent = Text == text && Review == review;
        if (unchangedContent && IsDraft == isDraft)
        {
            return false;
        }

        if (!IsDraft && !unchangedContent)
        {
            throw new InvalidOperationException(
                "A completed review must be reopened before its journal text or answers can be edited.");
        }

        ValidateCompletion(isDraft, review);
        long nextRevision = checked(Revision + 1);
        SetUpdatedAtUtc(updatedAtUtc);
        Text = text;
        Review = review;
        IsDraft = isDraft;
        Revision = nextRevision;
        return true;
    }

    private static void ValidateCompletion(bool isDraft, DailyReviewAnswers review)
    {
        if (!isDraft && !review.CanComplete)
        {
            throw new ArgumentException(
                "Answer all three Daily Review questions with meaningful text before completing the review.",
                nameof(review));
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
