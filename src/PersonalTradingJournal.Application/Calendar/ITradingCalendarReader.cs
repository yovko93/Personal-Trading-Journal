namespace PersonalTradingJournal.Application.Calendar;

public interface ITradingCalendarReader
{
    Task<TradingCalendarMonth> GetAsync(TradingCalendarQuery query, CancellationToken cancellationToken = default);
}
