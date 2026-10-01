using System.Globalization;

namespace PersonalTradingJournal.Desktop.Views.Dashboard;

/// <summary>Shared display coordinates and monetary ticks; never rounds the source decimals.</summary>
internal readonly record struct ChartValueScale(double Minimum, double Maximum, double Step)
{
    public int TickCount => Math.Min(12, (int)Math.Round((Maximum - Minimum) / Step) + 1);

    public static ChartValueScale For(double[] values)
    {
        double min = Math.Min(0, values.Min()), max = Math.Max(0, values.Max());
        if (min == max) return new(-1, 1, 1);
        double raw = (max - min) / 4;
        double power = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double fraction = raw / power;
        double step = (fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10) * power;
        double floor = Math.Floor(min / step) * step, ceiling = Math.Ceiling(max / step) * step;
        if (floor == ceiling) ceiling = floor + step;
        return new(floor, ceiling, step);
    }

    public static string TickText(double value, string currency, int decimals) =>
        $"{value.ToString($"N{decimals}", CultureInfo.CurrentCulture)} {currency}".TrimEnd();
}
