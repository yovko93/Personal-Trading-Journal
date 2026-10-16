using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersonalTradingJournal.Application.Analytics.Breakdown;
using PersonalTradingJournal.Desktop.Tests.CalendarPage;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Dashboard;
using PersonalTradingJournal.Desktop.Views.Breakdown;

namespace PersonalTradingJournal.Desktop.Tests.Breakdown;

public sealed class BreakdownViewTests
{
    private const string HostVariable = "PTJ_BREAKDOWN_FILTER_HOST";
    [Fact]
    public async Task CompiledFiltersWrapBindAndPublishOnDispatcherInBothThemes()
    {
        if (Environment.GetEnvironmentVariable(HostVariable) != "1")
        {
            await IsolatedTestProcess.RunSuiteAsync(typeof(BreakdownViewTests), "breakdown-filters", HostVariable,
                TimeSpan.FromSeconds(90), caseHangTimeout: TimeSpan.FromSeconds(30));
            return;
        }
        await CalendarStaTest.RunAsync(() =>
        {
            using var f = new BreakdownFixture();
            var app = System.Windows.Application.Current ?? new() { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources = CalendarViewLayoutTests.SharedThemeResources("Light");
            var view = new BreakdownView { DataContext = f.Vm };
            int owner = Environment.CurrentManagedThreadId;
            f.Vm.PropertyChanged += (_, _) => Assert.Equal(owner, Environment.CurrentManagedThreadId);
            var prior = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            try
            {
                Pump(f.Vm.ActivateAsync());
                Assert.NotEqual(owner, Assert.Single(f.Reader.Calls).Thread);
                foreach (string theme in new[] { "Light", "Dark" })
                foreach (var (width, dpi) in new[] { (980, 96), (700, 240) })
                {
                    app.Resources = CalendarViewLayoutTests.SharedThemeResources(theme);
                    view.Resources.MergedDictionaries.Clear();
                    view.Resources.MergedDictionaries.Add(CalendarViewLayoutTests.SharedThemeResources(theme));
                    VisualTreeHelper.SetRootDpi(view, new(dpi / 96d, dpi / 96d));
                    f.Vm.Period = DashboardPeriod.Custom;
                    Layout();
                    var from = (TextBox)view.FindName("FromDate");
                    var through = (TextBox)view.FindName("ThroughDate");
                    from.Text = "2026-03-08"; through.Text = "2026-03-08";
                    from.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                    through.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                    Assert.Equal("2026-03-08", f.Vm.StartDateText);
                    var account = (ComboBox)view.FindName("AccountFilter");
                    var instrument = (ComboBox)view.FindName("InstrumentFilter");
                    account.SelectedItem = f.Vm.Accounts.Single(a => a.Id == BreakdownFixture.AccountId);
                    instrument.SelectedItem = f.Vm.Instruments.Single(i => i.Id == BreakdownFixture.InstrumentId);
                    ((ListBox)view.FindName("GroupingFilter")).SelectedValue = BreakdownGrouping.Account;
                    ((ComboBox)view.FindName("ModeFilter")).SelectedValue = BreakdownCalculationMode.Gross;
                    var apply = Descendants<Button>(view).Single(b => Equals(b.Content, "Apply"));
                    Assert.Same(f.Vm.ApplyCommand, apply.Command);
                    Pump(f.Vm.ApplyCommand.ExecuteAsync(null)); Layout();
                    Assert.False(f.Vm.HasUnappliedChanges);
                    Assert.Equal(BreakdownFixture.AccountId, f.Vm.Result!.Query.TradeFilter.TradingAccountId);
                    Assert.Equal(BreakdownFixture.InstrumentId, f.Vm.Result.Query.TradeFilter.InstrumentId);
                    Assert.Equal(BreakdownGrouping.Account, f.Vm.Result.Query.Grouping);
                    Assert.Equal(BreakdownCalculationMode.Gross, f.Vm.Result.Query.Mode);
                    Assert.Equal(f.Vm.SelectedAccount, account.SelectedItem);
                    Assert.Equal(f.Vm.SelectedInstrument, instrument.SelectedItem);
                    Assert.Equal(f.Vm.SelectedAccount.Name, AutomationProperties.GetHelpText(account));
                    Assert.Equal(f.Vm.SelectedInstrument.Name, instrument.ToolTip);
                    Assert.Equal(f.Vm.AppliedContext, ((TextBlock)view.FindName("AppliedScope")).ToolTip);
                    foreach (var control in Descendants<Control>(view).Where(c => c is ComboBox or TextBox || c is Button b && b.Command is not null))
                    {
                        if (!control.IsVisible) continue;
                        Assert.True(control.Focusable);
                        Assert.InRange(control.TranslatePoint(new(0, 0), view).X, -1, width);
                        Assert.InRange(control.TranslatePoint(new(control.ActualWidth, 0), view).X, 0, width + 1);
                    }
                    Assert.Equal(f.Vm.Currency, ((TextBox)view.FindName("CurrencyFilter")).Text);
                    foreach (var control in new Control[] { from, through, (ComboBox)view.FindName("AccountFilter"), (TextBox)view.FindName("CurrencyFilter") })
                    {
                        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)));
                        Assert.Equal(((SolidColorBrush)view.FindResource("PtjTextPrimaryBrush")).Color, ((SolidColorBrush)control.Foreground).Color);
                    }
                    Assert.Equal(((SolidColorBrush)view.FindResource("PtjBackgroundBrush")).Color, ((SolidColorBrush)view.Background).Color);
                    Assert.Equal(Visibility.Collapsed, Descendants<Button>(view).Single(b => Equals(b.Content, "Cancel loading")).Visibility);
                    var scroll = (ScrollViewer)view.Content;
                    Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
                    SaveRender(view, theme, width, dpi);
                    scroll.ScrollToEnd(); Layout();
                    Assert.InRange(scroll.VerticalOffset - scroll.ScrollableHeight, -1, 1);
                    SaveRender(view, theme + "-bottom", width, dpi);
                    scroll.ScrollToTop(); Layout();
                    from.Text = "invalid"; from.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                    int count = f.Reader.Calls.Count; Pump(f.Vm.ApplyCommand.ExecuteAsync(null)); Layout();
                    Assert.Equal(count, f.Reader.Calls.Count); Assert.NotNull(f.Vm.DateValidation);
                    Assert.Contains(Descendants<TextBlock>(view), t => t.Text == f.Vm.DateValidation);

                    void Layout() { view.Measure(new(width, 570)); view.Arrange(new(0, 0, width, 570)); view.UpdateLayout(); }
                }
                f.Vm.StartDateText = "2026-03-08";
                f.Reader.Hold = true; f.Reader.Drain();
                Task loading = f.Vm.ApplyAsync();
                var next = f.Reader.Next(); Pump(next);
                view.UpdateLayout();
                var cancel = Descendants<Button>(view).Single(b => Equals(b.Content, "Cancel loading"));
                Assert.Equal(Visibility.Visible, cancel.Visibility); Assert.True(cancel.IsEnabled);
                cancel.Command.Execute(null);
                next.Result.Completion.SetResult(BreakdownFixture.Snapshot(next.Result.Query)); Pump(loading);
                Assert.True(next.Result.Token.IsCancellationRequested); Assert.False(f.Vm.IsLoading);
            }
            finally { view.DataContext = null; SynchronizationContext.SetSynchronizationContext(prior); }
        });
    }
    private static void Pump(Task work)
    {
        if (!work.IsCompleted)
        {
            var frame = new DispatcherFrame();
            _ = work.ContinueWith(_ => Dispatcher.CurrentDispatcher.BeginInvoke(() => frame.Continue = false),
                CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
            Dispatcher.PushFrame(frame);
        }
        work.GetAwaiter().GetResult();
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int n = 0; n < VisualTreeHelper.GetChildrenCount(root); n++)
        {
            var child = VisualTreeHelper.GetChild(root, n);
            if (child is T found) yield return found;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
    private static void SaveRender(FrameworkElement view, string theme, int width, int dpi)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "breakdown-renders"); Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap(width * dpi / 96, 570 * dpi / 96, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, $"{theme}-{width}-{dpi}.png")); encoder.Save(file);
    }
}
