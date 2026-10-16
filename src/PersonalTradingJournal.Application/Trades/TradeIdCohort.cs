namespace PersonalTradingJournal.Application.Trades;

/// <summary>Frozen membership, not a live predicate. Null scope and an empty cohort have different meanings.</summary>
public sealed class TradeIdCohort
{
    public TradeIdCohort(IEnumerable<Guid> tradeIds)
    {
        ArgumentNullException.ThrowIfNull(tradeIds);
        var ids = tradeIds.ToArray();
        if (ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Length)
            throw new ArgumentException("Cohort IDs must be valid and unique.", nameof(tradeIds));
        TradeIds = Array.AsReadOnly(ids);
    }
    public IReadOnlyList<Guid> TradeIds { get; }
}
