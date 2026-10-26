using System.Globalization;
using PersonalTradingJournal.Application.Common.Time;

namespace PersonalTradingJournal.Desktop.ViewModels.Notebook;

internal static class NotebookDatePresentation
{
    // Notebook dates retain the trading-day boundary, regardless of machine culture/timezone.
    public static string Format(DateTimeOffset utcTimestamp) =>
        TradingTimePolicy.ConvertUtcToTradingTime(utcTimestamp)
            .ToString("MMM dd, yyyy", CultureInfo.GetCultureInfo("en-US"));
}
