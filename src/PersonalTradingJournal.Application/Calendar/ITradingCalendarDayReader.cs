namespace PersonalTradingJournal.Application.Calendar;

public interface ITradingCalendarDayReader
{
    Task<TradingCalendarDayDetails> GetAsync(TradingCalendarDayQuery query,
        CancellationToken cancellationToken = default);
}
