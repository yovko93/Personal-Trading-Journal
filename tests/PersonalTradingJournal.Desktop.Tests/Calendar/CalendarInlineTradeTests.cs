using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Tests.Trades;
using PersonalTradingJournal.Desktop.ViewModels.Calendar;

namespace PersonalTradingJournal.Desktop.Tests.CalendarPage;

public sealed class CalendarInlineTradeTests
{
    [Fact]
    public void ClassificationPreservesInactiveMissingAndEmptyReferences()
    {
        var row = CalendarDayDetailsTests.Row(new(2026, 9, 5), 5, 5);
        var empty = new CalendarTradePresentation(row);
        Assert.Equal("None assigned", empty.SetupText);
        Assert.Equal("None assigned", empty.MistakesText);
        var assigned = new CalendarTradePresentation(row, new(Guid.NewGuid(), "Breakout", false,
            [new(Guid.NewGuid(), "Chasing", false), new(Guid.NewGuid(), null, null)]));
        Assert.Equal("Breakout (inactive)", assigned.SetupText);
        Assert.Equal("Chasing (inactive), Unavailable Mistake", assigned.MistakesText);
        Assert.Contains("Setup: Breakout", assigned.AccessibleName);
        Assert.Equal("Unavailable Setup", new CalendarTradePresentation(row, new(Guid.NewGuid(), null, null, [])).SetupText);
    }

    [Theory]
    [InlineData(2026, 3, 8, 6, 59, "01:59:00", "UTC-5")]
    [InlineData(2026, 3, 8, 7, 0, "03:00:00", "UTC-4")]
    [InlineData(2026, 9, 5, 8, 0, "04:00:00", "UTC-4")]
    [InlineData(2026, 9, 5, 16, 35, "12:35:00", "UTC-4")]
    public void ClockTimeIsAuthoritativeAndOffsetIsClearlyLabelled(int year, int month, int day, int hour, int minute, string expected, string offset)
    {
        var trade = CalendarDayDetailsTests.Row(new(year, month, day), 5, 5) with { ClosedAtUtc = new(year, month, day, hour, minute, 0, TimeSpan.Zero) };
        var row = new CalendarTradePresentation(trade);
        Assert.Equal(expected, row.ClosingTime);
        Assert.Contains(offset, row.ClosingTimeDescription);
        var point = Assert.Single(Assert.Single(CalendarDayPerformance.From([trade])).Points);
        Assert.StartsWith(expected, point.TimeText);
        var missing = new CalendarTradePresentation(trade with { ClosedAtUtc = null });
        Assert.Equal("—", missing.ClosingTime);
        Assert.Equal("Closing time unavailable", missing.ClosingTimeDescription);
        Assert.Empty(CalendarDayPerformance.From([missing.Trade]));
    }

    [Fact]
    public async Task InlineEditorTargetsRowRetainsInvalidDraftAndRefreshesAfterSuccessfulSave()
    {
        var row = CalendarDayDetailsTests.Row(new(2026, 9, 5), 200, 197);
        var original = TradesViewModelTests.CreateEditableTradeDetail(row, null);
        var updated = original with { AverageEntryPrice = 101, GrossPnL = 160, NetPnL = 157 };
        var details = new FakeTradeDetailReader();
        details.EnqueueResult(original); // View
        details.EnqueueResult(original); // Edit
        details.EnqueueResult(updated); // Save reload
        details.EnqueueResult(updated); // Calendar refresh of expanded details
        var store = new FakeTradeMutationStore { TradeToReturn = TradesViewModelTests.CreateEditableDomainTrade(original) };
        var editor = TradesViewModelTests.CreateViewModel(tradeDetailReader: details,
            reader: TradesViewModelTests.CreateEditReferenceReader(original), tradeMutationStore: store,
            accountStore: new() { AccountToReturn = TradesViewModelTests.CreateTradingAccount(row.TradingAccountId) },
            instrumentStore: new() { InstrumentToReturn = TradesViewModelTests.CreateInstrument(row.InstrumentId) });
        var dayReader = new FakeTradingCalendarDayReader { Handler = (q, ct) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date,
            [store.SaveCallCount == 0 ? row : row with { GrossPnL = 160, NetPnL = 157 }])) };
        var vm = await CalendarSummaryFixture.CreateAsync(dayReader, editor);
        await vm.SelectDayCommand.ExecuteAsync(CalendarDayDetailsTests.Cell(vm, new(2026, 9, 5)));
        await vm.ViewTradeCommand.ExecuteAsync(row);
        Assert.Equal(row.Id, editor.SelectedTradeDetail!.Id);
        Assert.True(vm.DayTrades[0].IsExpanded);
        await editor.ShowSelectedTradeEditCommand.ExecuteAsync(null);
        editor.QuantityText = "invalid";
        await editor.SaveTradeEditCommand.ExecuteAsync(null);
        Assert.Equal(0, store.SaveCallCount);
        Assert.True(editor.IsTradeEditVisible);
        Assert.False(vm.TryCloseDayDialog());
        Assert.False(vm.ViewTradeCommand.CanExecute(row));
        vm.OnDataCommitted(); // External refresh must not discard this draft.
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal("invalid", editor.QuantityText);
        Assert.Single(dayReader.Calls);
        editor.QuantityText = "2";
        editor.EntryPriceText = "101";
        store.SaveException = new IOException("synthetic failure");
        await editor.SaveTradeEditCommand.ExecuteAsync(null);
        Assert.True(editor.IsTradeEditVisible);
        Assert.Equal("101", editor.EntryPriceText);
        Assert.NotNull(editor.TradeUpdateErrorMessage);
        store.SaveException = null;
        int commits = 0; vm.TradeDataCommitted += (_, _) => commits++;
        // The fake mutation store has already returned the same mutable aggregate once.
        store.TradeToReturn = TradesViewModelTests.CreateEditableDomainTrade(original);
        await editor.SaveTradeEditCommand.ExecuteAsync(null);
        await vm.LoadTask;
        Assert.Equal(1, commits);
        Assert.Equal(row.Id, store.RequestedTradeId);
        Assert.Equal(101, store.SavedTrade!.AverageEntryPrice);
        Assert.Equal(157, Assert.Single(vm.DaySummaries).Amount);
        Assert.Equal(157, Assert.Single(Assert.Single(vm.DayPerformance).Points).Value);
        Assert.True(Assert.Single(vm.DayTrades).IsExpanded);
        Assert.False(editor.IsTradeEditVisible);
        Assert.Equal(row.Id, editor.SelectedTradeDetail!.Id);
        Assert.Equal(new DateOnly(2026, 9, 5), vm.SelectedDate);
        Assert.True(vm.TryCloseDayDialog());
    }

    [Fact]
    public async Task CancelDoesNotSaveAndSwitchingRowsUsesCurrentIdentity()
    {
        var a = CalendarDayDetailsTests.Row(new(2026, 9, 5), 5, 5);
        var b = a with { Id = Guid.NewGuid(), InstrumentSymbol = "OTHER" };
        var initial = TradesViewModelTests.CreateEditableTradeDetail(a, null);
        var reader = new FakeTradeDetailReader();
        reader.EnqueueResult(initial); reader.EnqueueResult(initial);
        reader.EnqueueResult(TradesViewModelTests.CreateEditableTradeDetail(b, null));
        var store = new FakeTradeMutationStore();
        var editor = TradesViewModelTests.CreateViewModel(tradeDetailReader: reader,
            reader: TradesViewModelTests.CreateEditReferenceReader(initial), tradeMutationStore: store);
        var dayReader = new FakeTradingCalendarDayReader { Handler = (q, ct) => Task.FromResult(TradingCalendarDayDetails.Create(q.Date, [b, a])) };
        var vm = await CalendarSummaryFixture.CreateAsync(dayReader, editor);
        await vm.SelectDayCommand.ExecuteAsync(CalendarDayDetailsTests.Cell(vm, new(2026, 9, 5)));
        await vm.ViewTradeCommand.ExecuteAsync(a);
        await editor.ShowSelectedTradeEditCommand.ExecuteAsync(null);
        editor.EntryPriceText = "999";
        await vm.ViewTradeCommand.ExecuteAsync(b);
        Assert.Equal(a.Id, editor.SelectedTradeDetail!.Id);
        editor.CancelTradeEditCommand.Execute(null);
        Assert.Equal(0, store.SaveCallCount);
        await vm.ViewTradeCommand.ExecuteAsync(b);
        Assert.Equal(b.Id, editor.SelectedTradeDetail!.Id);
        Assert.Equal(b.Id, Assert.Single(vm.DayTrades, r => r.IsExpanded).Trade.Id);
    }
}
