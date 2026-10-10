using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.DailyReview;

/// <summary>Pure calculation over one M15.1 snapshot. Unknown Net is never estimated.
/// Invalid scope/duplicate evidence throws ArgumentException; decimal overflow throws
/// OverflowException without returning partial results. No rounding or database access.</summary>
public static class DailyReviewStatisticsCalculator
{
    public static DailyReviewStatistics Calculate(DailyReviewEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(evidence.Query);
        cancellationToken.ThrowIfCancellationRequested();
        var identities = new HashSet<Guid>();
        var trades = new List<DailyReviewTradeEvidence>();
        foreach (var trade in evidence.Trades)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (trade.TradeId == Guid.Empty || !identities.Add(trade.TradeId) ||
                trade.Account.Id == Guid.Empty || !Enum.IsDefined(trade.Inclusion) ||
                evidence.Query.TradingAccountId is { } account && trade.Account.Id != account)
                throw new ArgumentException("Supply unique Trade identities in the requested Account scope.", nameof(evidence));
            if (string.IsNullOrWhiteSpace(trade.PricingCurrency) ||
                trade.PricingCurrency != trade.PricingCurrency.Trim().ToUpperInvariant())
                throw new ArgumentException("Supply the canonical historical pricing currency.", nameof(evidence));
            if (trade.Inclusion == DailyReviewTradeInclusion.ClosedOnDate &&
                (trade.Facts is not { Status: TradeStatus.Closed, ClosedAtUtc: { } close } ||
                 close < evidence.Query.FromUtc || close >= evidence.Query.BeforeUtc))
                throw new ArgumentException("Closed evidence must belong to the requested New York closure date.", nameof(evidence));
            trades.Add(trade);
        }
        // Source order must not affect arithmetic order, provenance or metadata selection.
        var ordered = trades.OrderBy(t => t.TradeId).ToArray();
        var currencies = new List<DailyReviewCurrencyStatistics>();
        foreach (var currency in ordered.GroupBy(t => t.PricingCurrency, StringComparer.Ordinal)
                     .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rows = currency.ToArray();
            var accounts = new List<DailyReviewAccountStatistics>();
            foreach (var account in rows.GroupBy(t => t.Account.Id).OrderBy(g => g.Key))
            {
                var accountRows = account.ToArray();
                accounts.Add(new(accountRows[0].Account, Population(accountRows, cancellationToken),
                    Pnl(accountRows, PnlBasis.Gross, cancellationToken),
                    Pnl(accountRows, PnlBasis.Net, cancellationToken)));
            }
            currencies.Add(new(currency.Key, Population(rows, cancellationToken),
                Pnl(rows, PnlBasis.Gross, cancellationToken), Pnl(rows, PnlBasis.Net, cancellationToken),
                accounts.AsReadOnly()));
        }
        var population = Population(ordered, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return new(evidence.Query, population, currencies.AsReadOnly());
    }

    private static DailyReviewPopulation Population(DailyReviewTradeEvidence[] trades, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var quality = new List<DailyReviewQualityGroup>();
        foreach (var flag in Enum.GetValues<DailyReviewTradeQuality>().Where(f => f != DailyReviewTradeQuality.None))
        {
            token.ThrowIfCancellationRequested();
            var set = Set(trades.Where(t => t.Quality.HasFlag(flag)));
            if (set.Count > 0) quality.Add(new(flag, set));
        }
        return new(Set(trades.Where(IsClosed)),
            Set(trades.Where(t => t.Inclusion == DailyReviewTradeInclusion.OpenActivityOnDate)),
            Set(trades.Where(t => t.Inclusion == DailyReviewTradeInclusion.UnavailableLifecycleActivityOnDate)),
            Set(trades.Where(UnknownCommission)), Set(trades.Where(UnknownFees)),
            Set(trades.Where(t => IsClosed(t) && !KnownCosts(t))), quality.AsReadOnly());
    }

    private static DailyReviewPnlStatistics Pnl(DailyReviewTradeEvidence[] trades, PnlBasis basis, CancellationToken token)
    {
        var accumulator = new PnlAccumulator(basis);
        var known = new List<Guid>();
        var unavailable = new List<Guid>();
        var wins = new List<Guid>();
        var losses = new List<Guid>();
        var breakEvens = new List<Guid>();
        foreach (var trade in trades.Where(IsClosed))
        {
            token.ThrowIfCancellationRequested();
            decimal? value = Gross(trade);
            if (basis == PnlBasis.Net)
                value = value.HasValue && KnownCosts(trade) &&
                    !trade.Quality.HasFlag(DailyReviewTradeQuality.UnknownNetPnL) ? trade.Facts?.NetPnL : null;
            accumulator.Add(value);
            if (value is { } amount)
            {
                known.Add(trade.TradeId);
                (amount > 0m ? wins : amount < 0m ? losses : breakEvens).Add(trade.TradeId);
            }
            else unavailable.Add(trade.TradeId);
        }
        return new(accumulator.Snapshot(checked(known.Count + unavailable.Count)), new(known.AsReadOnly()),
            new(unavailable.AsReadOnly()), new(wins.AsReadOnly()), new(losses.AsReadOnly()), new(breakEvens.AsReadOnly()));
    }

    private static decimal? Gross(DailyReviewTradeEvidence trade) =>
        (trade.Quality & (DailyReviewTradeQuality.MissingProjection |
                         DailyReviewTradeQuality.UnsupportedProjectionVersion |
                         DailyReviewTradeQuality.UnknownGrossPnL)) != 0 ? null : trade.Facts?.GrossPnL;

    private static bool IsClosed(DailyReviewTradeEvidence trade) =>
        trade.Inclusion == DailyReviewTradeInclusion.ClosedOnDate;

    private static bool UnknownCommission(DailyReviewTradeEvidence trade) =>
        trade.Quality.HasFlag(DailyReviewTradeQuality.UnknownCommission) ||
        trade.Executions.Count == 0 || trade.Executions.Any(e => e.Commission is null);

    private static bool UnknownFees(DailyReviewTradeEvidence trade) =>
        trade.Quality.HasFlag(DailyReviewTradeQuality.UnknownFees) ||
        trade.Executions.Count == 0 || trade.Executions.Any(e => e.Fees is null);

    private static bool KnownCosts(DailyReviewTradeEvidence trade) =>
        !UnknownCommission(trade) && !UnknownFees(trade) && trade.Facts?.TotalCosts is >= 0m;

    private static DailyReviewTradeSet Set(IEnumerable<DailyReviewTradeEvidence> trades) =>
        new(Array.AsReadOnly(trades.Select(t => t.TradeId).ToArray()));
}
