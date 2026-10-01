using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PersonalTradingJournal.Desktop.Views.Dashboard;

public enum RangeDay { None, Interior, Start, End, Single, Partial }

/// <summary>Inherited display-only range; native single-date selection still edits each endpoint.</summary>
public static class CalendarRange
{
    public static readonly DependencyProperty StartProperty = Register("Start", typeof(DateTime?), null);
    public static readonly DependencyProperty EndProperty = Register("End", typeof(DateTime?), null);
    public static readonly DependencyProperty IsDraftProperty = Register("IsDraft", typeof(bool), false);
    private static DependencyProperty Register(string name, Type type, object? value) =>
        DependencyProperty.RegisterAttached(name, type, typeof(CalendarRange), new FrameworkPropertyMetadata(value, FrameworkPropertyMetadataOptions.Inherits));
    public static DateTime? GetStart(DependencyObject target) => (DateTime?)target.GetValue(StartProperty);
    public static void SetStart(DependencyObject target, DateTime? value) => target.SetValue(StartProperty, value);
    public static DateTime? GetEnd(DependencyObject target) => (DateTime?)target.GetValue(EndProperty);
    public static void SetEnd(DependencyObject target, DateTime? value) => target.SetValue(EndProperty, value);
    public static bool GetIsDraft(DependencyObject target) => (bool)target.GetValue(IsDraftProperty);
    public static void SetIsDraft(DependencyObject target, bool value) => target.SetValue(IsDraftProperty, value);

    public static RangeDay Classify(DateTime day, DateTime? start, DateTime? end)
    {
        DateTime date = day.Date;
        if (start is not { } first || end is not { } last || first.Date > last.Date)
            return date == start?.Date || date == end?.Date ? RangeDay.Partial : RangeDay.None;
        if (date < first.Date || date > last.Date) return RangeDay.None;
        if (first.Date == last.Date) return RangeDay.Single;
        return date == first.Date ? RangeDay.Start : date == last.Date ? RangeDay.End : RangeDay.Interior;
    }
}

public sealed class CalendarRangeDayConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length == 3 && values[0] is DateTime date
            ? CalendarRange.Classify(date, values[1] as DateTime?, values[2] as DateTime?) : RangeDay.None;
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
