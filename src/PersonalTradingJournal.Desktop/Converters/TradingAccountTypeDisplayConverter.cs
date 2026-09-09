using PersonalTradingJournal.Domain.Accounts;
using System.Globalization;
using System.Windows.Data;

namespace PersonalTradingJournal.Desktop.Converters;

public sealed class TradingAccountTypeDisplayConverter : IValueConverter
{
    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        return value is TradingAccountType accountType
            ? accountType switch
            {
                TradingAccountType.Personal => "Personal",
                TradingAccountType.PropEvaluation => "Prop Evaluation",
                TradingAccountType.PropFunded => "Prop Funded",
                TradingAccountType.Demo => "Demo",
                TradingAccountType.Other => "Other",
                _ => accountType.ToString(),
            }
            : string.Empty;
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
