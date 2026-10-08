using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Journals;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.DailyReview;

public sealed record DailyReviewEvidence(DailyReviewQuery Query,
    IReadOnlyList<DailyReviewTradeEvidence> Trades, IReadOnlyList<DailyReviewJournalEvidence> Journals);

public enum DailyReviewTradeInclusion
{
    ClosedOnDate,
    OpenActivityOnDate,
    UnavailableLifecycleActivityOnDate,
}

/// <summary>Missing facts are evidence limitations, never inferred violations or zero amounts.</summary>
[Flags]
public enum DailyReviewTradeQuality
{
    None = 0,
    MissingProjection = 1,
    UnsupportedProjectionVersion = 2,
    MissingExecutions = 4,
    UnknownCommission = 8,
    UnknownFees = 16,
    UnknownGrossPnL = 32,
    UnknownNetPnL = 64,
    MissingClosingTime = 128,
    UnavailableAccount = 256,
    UnavailableInstrument = 512,
    UnavailableSetup = 1024,
    UnavailableMistake = 2048,
}

/// <summary>Original identity plus current metadata. Null activity/name means an unavailable reference.</summary>
public sealed record DailyReviewReference(Guid Id, string? Name, bool? IsActive);

public sealed record DailyReviewAssignedMistake(Guid AssignmentId, DailyReviewReference Mistake,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

/// <summary>Copies the Domain-maintained browse projection without recalculating economics.
/// ProjectionVersion is a format version, not a Trade revision/concurrency token.</summary>
public sealed record DailyReviewTradeFacts(int ProjectionVersion, TradeStatus Status, TradeDirection Direction,
    DateTimeOffset OpenedAtUtc, DateTimeOffset? ClosedAtUtc, decimal OpenQuantity,
    decimal AverageEntryPrice, decimal? AverageExitPrice, decimal? TotalCosts, decimal? GrossPnL, decimal? NetPnL);

/// <summary>One source Trade, including its full ordered execution lifecycle. Only ClosedOnDate
/// is attributed to this day's realized results. Open activity is context, not historical as-of state.</summary>
public sealed record DailyReviewTradeEvidence(Guid TradeId, DailyReviewReference Account,
    DailyReviewReference Instrument, DailyReviewReference? Setup, string PricingCurrency, decimal PricingPointValue,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, DailyReviewTradeInclusion Inclusion,
    DailyReviewTradeFacts? Facts, IReadOnlyList<TradeExecutionDetailItem> Executions,
    IReadOnlyList<DailyReviewAssignedMistake> AssignedMistakes, DailyReviewTradeQuality Quality);

/// <summary>Current persisted fields, unchanged, with the durable Journal revision. No entry is an
/// absent item; an existing entry can have empty fields. All accounts retains null and individual IDs.</summary>
public sealed record DailyReviewJournalEvidence(Guid JournalId, DateOnly TradingDate, Guid? TradingAccountId,
    string? AccountName, DailyJournalAccountState AccountState, string Text, DailyReviewAnswers Answers,
    bool IsDraft, long Revision, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
