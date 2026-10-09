using System.Globalization;
using PersonalTradingJournal.Application.Common.Time;

namespace PersonalTradingJournal.Desktop.Formatting;

/// <summary>Presentation only: retain clock precision and explicitly label the instant's UTC offset.</summary>
public static class TradingTimestampFormatter
{
    public const string DefaultClockFormat = "yyyy-MM-dd HH:mm:ss.FFFFFFF";

    public static string Format(DateTimeOffset timestamp, string clockFormat, CultureInfo culture)
    {
        int minutes = (int)(timestamp.Offset.Ticks / TimeSpan.TicksPerMinute);
        int magnitude = Math.Abs(minutes);
        string offset = $"UTC{(minutes < 0 ? "-" : "+")}{(magnitude / 60).ToString(CultureInfo.InvariantCulture)}";
        if (magnitude % 60 != 0)
            offset += ":" + (magnitude % 60).ToString("D2", CultureInfo.InvariantCulture);
        return timestamp.ToString(clockFormat, culture) + " " + offset;
    }

    public static string FormatNewYork(DateTimeOffset timestamp, string clockFormat, CultureInfo culture) =>
        // Display bindings accept both canonical UTC and already-projected offsets (e.g. Import).
        // Normalize the instant, not its wall clock; the Application UTC-only contract stays strict.
        Format(TradingTimePolicy.ConvertUtcToTradingTime(timestamp.ToUniversalTime()), clockFormat, culture);
}
