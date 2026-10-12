using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Application.Setups;

public interface ITradingSetupPlaybookHistoryReader
{
    Task<TradingSetupPlaybookRevision?> GetRevisionAsync(Guid setupId, int revision, CancellationToken cancellationToken = default);

    /// <summary>Descending revision order; exclusive beforeRevision cursor, null for newest. Limit 1–100.</summary>
    Task<IReadOnlyList<TradingSetupPlaybookRevision>> GetHistoryAsync(Guid setupId, int? beforeRevision,
        int limit, CancellationToken cancellationToken = default);
}
