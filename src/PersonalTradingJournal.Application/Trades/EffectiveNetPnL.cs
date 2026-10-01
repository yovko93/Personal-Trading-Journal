using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Trades;

public enum NetPnLProvenance { Unavailable, Verified, Estimated }

/// <summary>
/// Read-only alternative to authoritative NetPnL, never a persisted replacement.
/// For closed Trades only: prefer known Net; otherwise use known Gross as an explicitly
/// estimated Net without deducting costs (even any individually known component).
/// </summary>
public sealed record EffectiveNetPnL(decimal? Value, NetPnLProvenance Provenance)
{
    public bool IsEstimated => Provenance == NetPnLProvenance.Estimated;

    public static EffectiveNetPnL Resolve(TradeStatus status, decimal? gross, decimal? net) =>
        status != TradeStatus.Closed ? new(null, NetPnLProvenance.Unavailable)
        : net.HasValue ? new(net, NetPnLProvenance.Verified)
        : gross.HasValue ? new(gross, NetPnLProvenance.Estimated)
        : new(null, NetPnLProvenance.Unavailable);
}
