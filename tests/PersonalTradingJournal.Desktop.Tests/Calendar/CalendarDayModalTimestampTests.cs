using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using PersonalTradingJournal.Desktop.Converters;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Tests.Trades;
using PersonalTradingJournal.Desktop.Views.Calendar;

namespace PersonalTradingJournal.Desktop.Tests.CalendarPage;

public sealed partial class CalendarDayModalTests
{
    [Theory]
    [InlineData("Light", 9, 9, "UTC-4", 1100, 96)]
    [InlineData("Dark", 9, 9, "UTC-4", 1100, 96)]
    [InlineData("Light", 12, 10, "UTC-5", 1100, 96)]
    [InlineData("Dark", 12, 10, "UTC-5", 1100, 96)]
    [InlineData("Light", 9, 9, "UTC-4", 480, 240)]
    [InlineData("Dark", 9, 9, "UTC-4", 480, 240)]
    [InlineData("Light", 12, 10, "UTC-5", 480, 240)]
    [InlineData("Dark", 12, 10, "UTC-5", 480, 240)]
    public async Task InlineExecutionBindingRendersExplicitOffsetInBothThemes(string theme, int month,
        int utcHour, string offset, int width, int dpi)
    {
        var instant = new DateTimeOffset(2026, month, 28, utcHour, 10, 3, TimeSpan.Zero).AddTicks(1234567);
        var row = CalendarDayDetailsTests.Row(new(2026, month, 28), 200, 197) with { OpenedAtUtc = instant, ClosedAtUtc = instant.AddHours(1) };
        var detail = TradesViewModelTests.CreateEditableTradeDetail(row, null);
        var reader = new FakeTradeDetailReader(); reader.EnqueueResult(detail);
        var editor = TradesViewModelTests.CreateViewModel(tradeDetailReader: reader);
        await editor.ShowTradeDetailCommand.ExecuteAsync(row);
        await OnSta(() =>
        {
            var inline = new CalendarInlineTradeView { DataContext = editor, Resources = CalendarViewLayoutTests.SharedThemeResources(theme),
                Language = System.Windows.Markup.XmlLanguage.GetLanguage("en-US") };
            inline.SetResourceReference(Control.BackgroundProperty, "PtjSurfaceBrush");
            var window = new Window { Content = inline, Width = width, Height = 700, ShowInTaskbar = false };
            try
            {
                window.Show(); Pump(); window.UpdateLayout();
                var timestamps = Descendants(inline).OfType<TextBlock>()
                    .Where(t => t.GetBindingExpression(TextBlock.TextProperty)?.ParentBinding.Path.Path == "ExecutedAtUtc").ToArray();
                Assert.Equal(2, timestamps.Length);
                for (int i = 0; i < timestamps.Length; i++)
                {
                    Assert.IsType<TradingTimestampConverter>(timestamps[i].GetBindingExpression(TextBlock.TextProperty)!.ParentBinding.Converter);
                    Assert.Equal($"2026-{month:00}-28 {5 + i:00}:10:03.1234567 {offset}", timestamps[i].Text);
                    Assert.Equal(timestamps[i].Text, AutomationProperties.GetName(timestamps[i]));
                    Assert.True(timestamps[i].IsVisible);
                    Assert.True(timestamps[i].DesiredSize.Width <= width - 32);
                }
                Assert.Equal(instant, detail.Executions[0].ExecutedAtUtc);
                Assert.Equal(TimeSpan.Zero, detail.Executions[0].ExecutedAtUtc.Offset);
                Render(inline, $"offset-{month}-{theme}", width, dpi);
            }
            finally { window.Close(); }
        });
    }
}
