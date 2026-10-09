using System.Globalization;
using System.Windows.Data;
using PersonalTradingJournal.Application.Accounts;

namespace PersonalTradingJournal.Desktop.Converters;

public sealed record AccountBalanceDisplay(string ValueText, string SupportingText, string Description, int Comparison);

public sealed class AccountBalanceDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not AccountListItem account) return new AccountBalanceDisplay("Unavailable", "", "Current Balance unavailable", 0);
        var balance = account.CurrentBalance;
        string reason = balance?.Unavailable switch
        {
            AccountBalanceUnavailable.StartingBalanceMissing => "Starting Balance is not set.",
            AccountBalanceUnavailable.IncompleteTrade => "Trade economics are incomplete.",
            AccountBalanceUnavailable.Overflow => "Calculation exceeds the supported numeric range.",
            _ => balance is null ? "Refresh to calculate balance." : ""
        };
        var supporting = new List<string>();
        if (reason.Length > 0) supporting.Add(reason);
        if (balance?.IsEstimated == true)
            supporting.Add($"Unknown costs: {balance.TradesWithUnknownCosts:N0} Trade(s); {balance.UnknownCommissionCount:N0} commission, {balance.UnknownFeeCount:N0} fee entries.");
        if (balance?.OtherCurrencyTradeCount > 0)
            supporting.Add($"{balance.OtherCurrencyTradeCount:N0} other-currency Trade(s) excluded.");
        string text = (balance?.IsEstimated == true ? "Estimated " : "") +
            (balance?.Value is { } amount ? amount.ToString("N2", culture) + " " + account.Currency : "Unavailable");
        string help = $"Current Balance for {account.Name}: {text}. {string.Join(" ", supporting)} " +
            $"Trade-based calculation: Starting Balance plus Gross from {balance?.ClosedTradeCount ?? 0:N0} fully closed Trades in {account.Currency}, minus individually recorded commissions and fees. " +
            $"Missing costs remain unknown and are omitted from arithmetic. {balance?.OpenTradeCount ?? 0:N0} open or partially closed Trades excluded, including their costs. " +
            "Not a broker balance; excludes unrecorded deposits, withdrawals, payouts and other broker adjustments.";
        return new AccountBalanceDisplay(text, string.Join(" ", supporting), help, balance?.Comparison ?? 0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
