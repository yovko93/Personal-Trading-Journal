namespace PersonalTradingJournal.Application.Trades;

public interface IImportedTradeDescriptionStore
{
    Task<string?> SaveDescriptionAsync(Guid tradeId, string? description, CancellationToken cancellationToken = default);
}
