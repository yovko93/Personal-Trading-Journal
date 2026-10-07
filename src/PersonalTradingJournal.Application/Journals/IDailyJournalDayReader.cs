namespace PersonalTradingJournal.Application.Journals;

/// <summary>All journals for one explicit New York journal date. Null aggregates every
/// Account scope; a non-null Account filters exactly. This does not change write identity.</summary>
public interface IDailyJournalDayReader
{
    Task<IReadOnlyList<DailyJournalDetails>> GetDayAsync(DateOnly tradingDate, Guid? accountId = null,
        CancellationToken cancellationToken = default);
}
