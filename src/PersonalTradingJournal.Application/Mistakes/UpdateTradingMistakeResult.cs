namespace PersonalTradingJournal.Application.Mistakes;

public sealed record UpdateTradingMistakeResult(
    TradingMistakeDetails TradingMistake,
    bool WasChanged);
