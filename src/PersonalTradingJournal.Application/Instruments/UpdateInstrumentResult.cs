namespace PersonalTradingJournal.Application.Instruments;

public sealed record UpdateInstrumentResult(
    InstrumentDetails Instrument,
    bool WasChanged);
