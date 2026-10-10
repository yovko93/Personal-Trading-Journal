using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Accounts;

public interface ITradingAccountBalanceReader
{
    Task<IReadOnlyList<AccountListItem>> GetAllWithBalancesAsync(CancellationToken cancellationToken = default);
}

public sealed record AccountBalanceCost(decimal? Commission, decimal? Fees);
public sealed record AccountBalanceTrade(Guid Id, string Currency, TradeStatus? Status, decimal? Gross,
    IReadOnlyList<AccountBalanceCost> Executions);
public enum AccountBalanceUnavailable { None, StartingBalanceMissing, IncompleteTrade, Overflow }

/// <summary>Read-only Trade-based balance, not a broker balance or a replacement for strict Net P&amp;L.</summary>
public sealed record AccountCurrentBalance(decimal? Value, int Comparison, int ClosedTradeCount,
    int UnknownCommissionCount, int UnknownFeeCount, int TradesWithUnknownCosts,
    int OtherCurrencyTradeCount, int OpenTradeCount, AccountBalanceUnavailable Unavailable)
{
    public bool IsEstimated => TradesWithUnknownCosts > 0;
}

public static class AccountCurrentBalanceCalculator
{
    public static AccountCurrentBalance Calculate(decimal? startingBalance, string currency,
        IEnumerable<AccountBalanceTrade> source, CancellationToken cancellationToken = default)
    {
        var trades = source.OrderBy(t => t.Id).ToArray();
        var included = trades.Where(t => t.Status == TradeStatus.Closed && t.Currency == currency).ToArray();
        int commissions = 0, fees = 0, unknown = 0;
        foreach (var trade in included)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int c = trade.Executions.Count(e => !e.Commission.HasValue);
            int f = trade.Executions.Count(e => !e.Fees.HasValue);
            commissions += c; fees += f;
            if (c + f > 0) unknown++;
        }
        var result = new AccountCurrentBalance(null, 0, included.Length, commissions, fees, unknown,
            trades.Count(t => t.Status == TradeStatus.Closed && t.Currency != currency),
            trades.Count(t => t.Status == TradeStatus.Open), AccountBalanceUnavailable.None);
        cancellationToken.ThrowIfCancellationRequested();
        if (!startingBalance.HasValue) return result with { Unavailable = AccountBalanceUnavailable.StartingBalanceMissing };
        if (trades.Any(t => t.Status is null) || included.Any(t => !t.Gross.HasValue || t.Executions.Count == 0))
            return result with { Unavailable = AccountBalanceUnavailable.IncompleteTrade };
        try
        {
            decimal balance = startingBalance.Value;
            foreach (var trade in included)
            {
                cancellationToken.ThrowIfCancellationRequested();
                balance = checked(balance + trade.Gross!.Value);
                foreach (var execution in trade.Executions)
                {
                    // Deduct individually recorded components, including entry costs. A null
                    // remains unknown in coverage; never write/coerce it to a known zero.
                    if (execution.Commission is { } commission) balance = checked(balance - commission);
                    if (execution.Fees is { } fee) balance = checked(balance - fee);
                }
            }
            return result with { Value = balance, Comparison = decimal.Compare(balance, startingBalance.Value) };
        }
        catch (OverflowException) { return result with { Unavailable = AccountBalanceUnavailable.Overflow }; }
    }
}
