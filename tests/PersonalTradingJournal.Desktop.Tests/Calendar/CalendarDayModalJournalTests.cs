using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Tests.Trades;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;
using PersonalTradingJournal.Desktop.Views.Calendar;

namespace PersonalTradingJournal.Desktop.Tests.CalendarPage;

public sealed partial class CalendarDayModalTests
{
    [Theory]
    [InlineData("Light", 1100, 96, true)]
    [InlineData("Dark", 1100, 96, false)]
    [InlineData("Light", 480, 240, false)]
    [InlineData("Dark", 480, 240, true)]
    public async Task JournalMarkersAndModalActionsStayCompactAcrossThemesAndSizes(string theme, int width, int dpi, bool draft)
    {
        var date = new DateOnly(2026, 9, 5); // Saturday remains weekly-only.
        var statuses = new ModalJournalStatuses([
            new(Guid.NewGuid(), date, draft, 2),
            new(Guid.NewGuid(), date, false, 1, Guid.NewGuid()),
            new(Guid.NewGuid(), new(2026, 8, 31), false, 4)]);
        CalendarViewModel vm = await CalendarSummaryFixture.CreateAsync(journalStatusReader: statuses,
            journalRepository: new FakeDailyJournalRepository(), journalDialogs: new FakeDialogService());
        vm.OpenJournalAsync = (_, _) => Task.CompletedTask;
        await vm.SelectDayCommand.ExecuteAsync(CalendarDayDetailsTests.Cell(vm, date));
        await OnSta(() =>
        {
            var resources = CalendarViewLayoutTests.SharedThemeResources(theme);
            var grid = new CalendarView { DataContext = vm, Resources = resources };
            grid.SetResourceReference(Control.BackgroundProperty, "PtjBackgroundBrush");
            grid.Measure(new Size(width, 800)); grid.Arrange(new Rect(0, 0, width, 800)); grid.UpdateLayout();
            foreach (var cell in Descendants(grid).OfType<CalendarDayHost>())
            {
                var day = (CalendarDayCell)cell.DataContext;
                var indicator = Descendants(cell).OfType<TextBlock>().Single(t => t.Name == "DayJournalIndicator");
                bool expected = day.Date == date || day.Date == new DateOnly(2026, 8, 31);
                Assert.Equal(expected ? Visibility.Visible : Visibility.Collapsed, indicator.Visibility);
                Assert.Null(indicator.ToolTip); // Date-marker-only hover behavior is unchanged.
                if (!expected) continue;
                bool isDraft = day.Date == date && draft;
                Assert.Equal(isDraft ? "Draft" : "✓", indicator.Text);
                Assert.Contains(isDraft ? "Draft" : "Completed", AutomationProperties.GetName(indicator), StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Completed", indicator.Text);
                if (day.Date == date)
                {
                    Assert.Equal(2, day.JournalCount);
                    Assert.Contains(draft ? "1 Draft, 1 Completed" : "0 Draft, 2 Completed", AutomationProperties.GetName(indicator));
                }
                Assert.True(indicator.ActualWidth <= cell.ActualWidth);
                Assert.True(indicator.TransformToAncestor(cell).Transform(new Point()).Y + indicator.ActualHeight <= cell.ActualHeight);
                Assert.Same(resources["PtjTextSecondaryBrush"], indicator.Foreground);
                Assert.Null(cell.ToolTip);
                if (day.IsSaturday)
                {
                    Assert.False(day.ShowsDailySummary);
                    Assert.Contains(Descendants(cell).OfType<TextBlock>(), t => t.Text == "Week 1");
                }
            }
            Render(grid, $"journal-grid-{theme}-{draft}", width, dpi);
            var content = new CalendarDayDetailsView { DataContext = vm, Resources = resources };
            content.Measure(new Size(width, 760)); content.Arrange(new Rect(0, 0, width, 760)); content.UpdateLayout();
            var button = (Button)content.FindName("DayJournalAction");
            Assert.True(button.IsEnabled);
            Assert.True(button.Focusable);
            Assert.Equal("Add Journal", button.Content);
            Assert.Equal(button.Content, AutomationProperties.GetName(button));
            Assert.Contains("Choose its Account inside the form", AutomationProperties.GetHelpText(button));
            Assert.DoesNotContain(Descendants(content).OfType<TextBlock>(), t => t.Text.Contains("Coming later", StringComparison.Ordinal));
            var scroll = (ScrollViewer)content.FindName("DayContentScroller");
            button.BringIntoView(); content.UpdateLayout();
            var top = button.TransformToAncestor(scroll).Transform(new Point());
            Assert.InRange(top.Y, -1, scroll.ViewportHeight);
            Assert.InRange(top.Y + button.ActualHeight, 0, scroll.ViewportHeight + 1);
            Render(content, $"journal-action-{theme}-{draft}", width, dpi);
        });
    }

    [Theory]
    [InlineData("Light", false)]
    [InlineData("Dark", true)]
    public async Task JournalActionStaysInlineAndProtectedModalCloseKeepsTradeAndJournalDrafts(string theme, bool veto)
    {
        var date = new DateOnly(2026, 9, 5);
        var row = CalendarDayDetailsTests.Row(date, 10, 9);
        var detail = TradesViewModelTests.CreateEditableTradeDetail(row, null);
        var details = new FakeTradeDetailReader(); details.EnqueueResult(detail); details.EnqueueResult(detail);
        var editor = TradesViewModelTests.CreateViewModel(tradeDetailReader: details,
            reader: TradesViewModelTests.CreateEditReferenceReader(detail));
        var dayReader = new FakeTradingCalendarDayReader { Handler = (query, ct) => Task.FromResult(
            TradingCalendarDayDetails.Create(query.Date, [row], ct)) };
        var journalRepository = new FakeDailyJournalRepository();
        var journalDialogs = new FakeDialogService();
        var vm = await CalendarSummaryFixture.CreateAsync(dayReader, editor, journalRepository, journalRepository, journalDialogs);
        await OnSta(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            var view = new CalendarView { DataContext = vm, Resources = CalendarViewLayoutTests.SharedThemeResources(theme) };
            var owner = new Window { Content = view, Width = 1100, Height = 820, ShowInTaskbar = false };
            int navigations = 0;
            vm.OpenJournalAsync = (requestedDate, account) =>
            {
                navigations++;
                return Task.CompletedTask;
            };
            Exception? failure = null;
            try
            {
                owner.Show(); Pump(); owner.UpdateLayout();
                var cell = Descendants(view).OfType<CalendarDayHost>().Single(c => ((CalendarDayCell)c.DataContext).Date == date);
                dispatcher.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        var dialog = Assert.IsType<CalendarDayDialogWindow>(view.DayDialog);
                        await vm.DayLoadTask; dialog.UpdateLayout();
                        var content = (CalendarDayDetailsView)dialog.FindName("DayContent");
                        var button = (Button)content.FindName("DayJournalAction");
                        Assert.True(button.IsEnabled);
                        Assert.Equal("Add Journal", button.Content);
                        if (veto)
                        {
                            await vm.ViewTradeCommand.ExecuteAsync(row);
                            await editor.ShowSelectedTradeEditCommand.ExecuteAsync(null);
                            editor.EntryPriceText = "local unsaved edit";
                        }
                        Assert.Same(vm.AddDayJournalCommand, button.Command);
                        button.Command.Execute(null);
                        await vm.AddDayJournalCommand.ExecutionTask!;
                        dialog.UpdateLayout(); // Materialize the lazy editor under the real modal's scoped resources.
                        var inlineView = Assert.Single(Descendants(content).OfType<PersonalTradingJournal.Desktop.Views.Journals.InlineJournalView>());
                        Assert.True(inlineView.IsVisible);
                        Assert.Same(dialog, view.DayDialog);
                        Assert.True(dialog.IsVisible);
                        Assert.Equal(0, navigations);
                        Assert.Equal(Visibility.Visible, ((Border)view.FindName("ModalShade")).Visibility);
                        var journal = Assert.IsType<PersonalTradingJournal.Desktop.ViewModels.Journals.JournalViewModel>(vm.InlineJournal);
                        Assert.True(journal.IsEditorOpen && journal.CanEdit);
                        Assert.Equal(date.ToDateTime(TimeOnly.MinValue), journal.SelectedDate);
                        Assert.Null(journal.SelectedAccount.Id);
                        journal.Text = "Keep modal draft";
                        dialog.Close(); // Trade edit or Journal unsaved-change veto.
                        Assert.Same(dialog, view.DayDialog);
                        Assert.Equal("Keep modal draft", journal.Text);
                        if (veto)
                        {
                            Assert.Equal("local unsaved edit", editor.EntryPriceText);
                            Assert.True(editor.IsTradeEditVisible);
                            editor.CancelTradeEditCommand.Execute(null);
                            dialog.Close();
                            Assert.Same(dialog, view.DayDialog); // Journal now independently vetoes.
                        }
                        journalDialogs.ConfirmationResult = true;
                        dialog.Close();
                    }
                    catch (Exception error) { failure = error; journalDialogs.ConfirmationResult = true; editor.CancelTradeEditCommand.Execute(null); view.DayDialog?.Close(); }
                }));
                cell.RaiseEvent(new RoutedEventArgs(CalendarDayHost.InvokedEvent));
                Pump();
                if (failure is not null) throw failure;
                Assert.Null(view.DayDialog);
                Assert.Equal(0, navigations);
                Assert.Null(vm.InlineJournal);
                Assert.Equal(date, vm.SelectedDate);
                Assert.Equal(new DateOnly(2026, 9, 1), vm.SelectedMonth);
                Assert.Equal("All currencies", vm.SelectedCurrency);
                Assert.Null(vm.SelectedAccount.Id);
            }
            finally { editor.CancelTradeEditCommand.Execute(null); owner.Close(); }
        });
    }

    private sealed class ModalJournalStatuses(IReadOnlyList<DailyJournalStatus> statuses) : IDailyJournalStatusReader
    {
        public Task<IReadOnlyList<DailyJournalStatus>> GetAsync(DateOnly from, DateOnly through,
            Guid? tradingAccountId = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<DailyJournalStatus>>(statuses.Where(s => s.TradingDate >= from && s.TradingDate <= through).ToArray());
        }
    }
}
