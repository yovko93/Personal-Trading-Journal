namespace PersonalTradingJournal.Application.Trades;

public interface IManualTradeReferenceDataReader
{
    Task<ManualTradeReferenceData> GetAsync(
        bool includeInactiveReferences = false,
        CancellationToken cancellationToken = default);
}
