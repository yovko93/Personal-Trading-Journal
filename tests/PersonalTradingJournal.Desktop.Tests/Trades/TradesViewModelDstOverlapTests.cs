using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Trades;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Desktop.Tests.Trades;

public sealed partial class TradesViewModelTests
{
    private static readonly DateTimeOffset EdtOverlapInstant =
        new(2026, 11, 1, 5, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset EstOverlapInstant =
        new(2026, 11, 1, 6, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task EditPreservesUnchangedEdtOverlapEntryInstant()
    {
        DstEditFixture fixture = await OpenDstEditAsync(
            EdtOverlapInstant,
            exitAtUtc: null);

        Assert.Equal(
            "2026-11-01 01:30:00",
            fixture.ViewModel.EntryExecutedAtNewYorkText);

        fixture.ViewModel.EntryPriceText = "101";
        await fixture.ViewModel.SaveTradeEditCommand.ExecuteAsync(null);

        Assert.Equal(1, fixture.MutationStore.SaveCallCount);
        Assert.Equal(
            EdtOverlapInstant,
            Assert.Single(fixture.MutationStore.SavedTrade!.Executions).ExecutedAtUtc);
        Assert.Null(fixture.ViewModel.ValidationErrorMessage);
    }

    [Fact]
    public async Task EditPreservesUnchangedEstOverlapEntryInstant()
    {
        DstEditFixture fixture = await OpenDstEditAsync(
            EstOverlapInstant,
            exitAtUtc: null);

        Assert.Equal(
            "2026-11-01 01:30:00",
            fixture.ViewModel.EntryExecutedAtNewYorkText);

        fixture.ViewModel.EntryPriceText = "101";
        await fixture.ViewModel.SaveTradeEditCommand.ExecuteAsync(null);

        Assert.Equal(1, fixture.MutationStore.SaveCallCount);
        Assert.Equal(
            EstOverlapInstant,
            Assert.Single(fixture.MutationStore.SavedTrade!.Executions).ExecutedAtUtc);
        Assert.Null(fixture.ViewModel.ValidationErrorMessage);
    }

    [Fact]
    public async Task EditPreservesUnchangedOverlapExitInstantIndependently()
    {
        DateTimeOffset entryAtUtc =
            new(2026, 11, 1, 4, 30, 0, TimeSpan.Zero);
        DstEditFixture fixture = await OpenDstEditAsync(
            entryAtUtc,
            EstOverlapInstant);

        Assert.Equal(
            "2026-11-01 00:30:00",
            fixture.ViewModel.EntryExecutedAtNewYorkText);
        Assert.Equal(
            "2026-11-01 01:30:00",
            fixture.ViewModel.ExitExecutedAtNewYorkText);

        fixture.ViewModel.ExitPriceText = "106";
        await fixture.ViewModel.SaveTradeEditCommand.ExecuteAsync(null);

        Assert.Equal(1, fixture.MutationStore.SaveCallCount);
        Assert.Equal(
            [entryAtUtc, EstOverlapInstant],
            fixture.MutationStore.SavedTrade!.Executions
                .Select(execution => execution.ExecutedAtUtc));
    }

    [Fact]
    public async Task EditRejectsChangedAmbiguousTimestampWithoutPersistence()
    {
        DstEditFixture fixture = await OpenDstEditAsync(
            new DateTimeOffset(2026, 9, 10, 13, 30, 0, TimeSpan.Zero),
            exitAtUtc: null);

        fixture.ViewModel.EntryExecutedAtNewYorkText =
            "2026-11-01 01:30:00";
        await fixture.ViewModel.SaveTradeEditCommand.ExecuteAsync(null);

        Assert.Equal(0, fixture.MutationStore.GetCallCount);
        Assert.Equal(0, fixture.MutationStore.SaveCallCount);
        Assert.True(fixture.ViewModel.IsEntryExecutedAtNewYorkInvalid);
        Assert.Equal(
            "Entry New York time is ambiguous because of the daylight-saving transition. Enter an unambiguous time.",
            fixture.ViewModel.ValidationErrorMessage);
    }

    [Fact]
    public async Task CloseTradeStillRejectsAmbiguousNewExecutionTime()
    {
        CloseTradeFixture fixture = await CreateCloseTradeFixtureAsync();
        ShowValidCloseForm(fixture.ViewModel);
        fixture.ViewModel.CloseTradeExecutedAtNewYorkText =
            "2026-11-01 01:30:00";

        bool succeeded = fixture.ViewModel.TryBuildCloseManualTradeCommand(
            out CloseManualTradeCommand? command);

        Assert.False(succeeded);
        Assert.Null(command);
        Assert.Equal(
            "Exit New York time is ambiguous because of the daylight-saving transition. Enter an unambiguous time.",
            fixture.ViewModel.CloseTradeValidationErrorMessage);
        Assert.Equal(0, fixture.MutationStore.SaveCallCount);
    }

    [Fact]
    public async Task CancelledEditSnapshotCannotDisambiguateLaterCreateInput()
    {
        DstEditFixture fixture = await OpenDstEditAsync(
            EdtOverlapInstant,
            exitAtUtc: null);
        ManualTradeAccountOption account = Assert.Single(
            fixture.ViewModel.AccountOptions);
        ManualTradeInstrumentOption instrument = Assert.Single(
            fixture.ViewModel.InstrumentOptions);

        fixture.ViewModel.CancelTradeEditCommand.Execute(null);
        fixture.ViewModel.ShowManualEntryCommand.Execute(null);
        fixture.ViewModel.SelectedAccount = account;
        fixture.ViewModel.SelectedInstrument = instrument;
        fixture.ViewModel.SelectedDirection = TradeDirection.Long;
        fixture.ViewModel.QuantityText = "1";
        fixture.ViewModel.EntryExecutedAtNewYorkText =
            "2026-11-01 01:30:00";
        fixture.ViewModel.EntryPriceText = "100";

        bool succeeded = fixture.ViewModel.TryBuildManualTradeCommand(
            out CreateManualTradeCommand? command);

        Assert.False(succeeded);
        Assert.Null(command);
        Assert.Equal(
            "Entry New York time is ambiguous because of the daylight-saving transition. Enter an unambiguous time.",
            fixture.ViewModel.ValidationErrorMessage);
    }

    private static async Task<DstEditFixture> OpenDstEditAsync(
        DateTimeOffset entryAtUtc,
        DateTimeOffset? exitAtUtc)
    {
        TradeListItem item = CreateTradeListItem() with
        {
            Status = exitAtUtc.HasValue ? TradeStatus.Closed : TradeStatus.Open,
            OpenedAtUtc = entryAtUtc,
            ClosedAtUtc = exitAtUtc,
            OpenQuantity = exitAtUtc.HasValue ? 0m : 2m,
            AverageEntryPrice = 100m,
            AverageExitPrice = exitAtUtc.HasValue ? 105m : null,
            GrossPnL = exitAtUtc.HasValue ? 200m : null,
            NetPnL = exitAtUtc.HasValue ? 197m : null,
        };
        TradeDetail detail = CreateEditableTradeDetailAt(
            item,
            entryAtUtc,
            exitAtUtc);
        var detailReader = new FakeTradeDetailReader();
        detailReader.EnqueueResult(detail);
        detailReader.EnqueueResult(detail);
        var setupReader = new FakeTradingSetupReader();
        setupReader.EnqueueResult([]);
        var mutationStore = new FakeTradeMutationStore
        {
            TradeToReturn = CreateEditableDomainTrade(detail),
        };
        var accountStore = new FakeTradingAccountStore
        {
            AccountToReturn = CreateTradingAccount(detail.TradingAccountId),
        };
        var instrumentStore = new FakeInstrumentStore
        {
            InstrumentToReturn = CreateInstrument(detail.InstrumentId),
        };
        var listReader = new FakeTradeListReader();
        listReader.EnqueueResult([item]);
        TradesViewModel viewModel = CreateViewModel(
            reader: CreateEditReferenceReader(detail),
            tradeListReader: listReader,
            tradeDetailReader: detailReader,
            accountStore: accountStore,
            instrumentStore: instrumentStore,
            tradeMutationStore: mutationStore,
            tradingSetupReader: setupReader);

        await viewModel.ShowTradeEditCommand.ExecuteAsync(item);
        return new DstEditFixture(viewModel, mutationStore);
    }

    private static TradeDetail CreateEditableTradeDetailAt(
        TradeListItem item,
        DateTimeOffset entryAtUtc,
        DateTimeOffset? exitAtUtc)
    {
        var executions = new List<TradeExecutionDetailItem>
        {
            new(
                Guid.NewGuid(), 1, entryAtUtc, ExecutionSide.Buy,
                2m, 100m, 1m, 0.5m, 1.5m,
                "ESX6", "ENTRY-1", "ORDER-1"),
        };
        if (exitAtUtc.HasValue)
        {
            executions.Add(new TradeExecutionDetailItem(
                Guid.NewGuid(), 2, exitAtUtc.Value, ExecutionSide.Sell,
                2m, 105m, 1m, 0.5m, 1.5m,
                "ESX6", "EXIT-1", "ORDER-2"));
        }

        return new TradeDetail(
            item.Id,
            item.TradingAccountId,
            item.TradingAccountName,
            item.InstrumentId,
            item.InstrumentSymbol,
            $"{item.InstrumentSymbol} display name",
            null,
            null,
            null,
            TradeDirection.Long,
            exitAtUtc.HasValue ? TradeStatus.Closed : TradeStatus.Open,
            entryAtUtc,
            exitAtUtc,
            exitAtUtc.HasValue ? 0m : 2m,
            100m,
            exitAtUtc.HasValue ? 105m : null,
            exitAtUtc.HasValue ? 3m : 1.5m,
            exitAtUtc.HasValue ? 200m : null,
            exitAtUtc.HasValue ? 197m : null,
            20m,
            "USD",
            executions);
    }

    private sealed record DstEditFixture(
        TradesViewModel ViewModel,
        FakeTradeMutationStore MutationStore);
}
