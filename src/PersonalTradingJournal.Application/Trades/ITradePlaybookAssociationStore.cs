using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Application.Trades;

/// <summary>A read-only, transient confirmation. Tokens are opaque and must never be logged.</summary>
public sealed record TradePlaybookAssociationPreview(Guid TradeId, Guid? SetupId, string? SetupName,
    TradingSetupPlaybookRevision? Definition, string TradeToken, string SelectionToken);

/// <summary>Append-only classification transition; null Definition is deliberately not a capture.</summary>
public sealed record TradePlaybookAssociation(int Sequence, Guid? SetupId, DateTimeOffset RecordedAtUtc,
    TradingSetupPlaybookRevision? Definition, DateTimeOffset? TradeOpenedAtUtc = null);

public interface ITradePlaybookAssociationStore
{
    Task<TradePlaybookAssociationPreview> PreviewAsync(Guid tradeId, Guid? setupId, CancellationToken cancellationToken = default);
    Task<bool> TryApplyAsync(TradePlaybookAssociationPreview preview, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TradePlaybookAssociation>> ReadHistoryAsync(Guid tradeId, int? beforeSequence = null,
        int limit = 20, CancellationToken cancellationToken = default);
}
