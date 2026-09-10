namespace PersonalTradingJournal.Application.Trades;

public sealed record ManualTradeReferenceData(
    IReadOnlyList<ManualTradeAccountOption> Accounts,
    IReadOnlyList<ManualTradeInstrumentOption> Instruments);
