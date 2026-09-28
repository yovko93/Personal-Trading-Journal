using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Imports.Tradovate;

public sealed record TradovatePreparedTradeCandidate(
    string BrokerSymbol,
    string CanonicalSymbol,
    Guid? ExistingInstrumentId,
    TradovateInstrumentCreationProposal? InstrumentCreationProposal,
    Guid TradingAccountId,
    TradeDirection ProvisionalDirection,
    IReadOnlyList<TradovatePreparedExecution> OrderedExecutions,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    DateTimeOffset OpenedAtNewYork,
    DateTimeOffset? ClosedAtNewYork,
    IReadOnlyList<int> SourceRecordIndices);
