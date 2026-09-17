using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Tests.Trades;

public sealed class TradeLifecycleUseCaseTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 17, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset UpdatedAt = CreatedAt.AddHours(2);

    [Fact]
    public async Task UpdateCorrectsAggregateRebuildsChangedPricingAndPreservesMetadata()
    {
        Trade trade = CreateTrade();
        TradingAccount account = CreateAccount(Guid.NewGuid(), isActive: true);
        Instrument instrument = CreateInstrument(
            Guid.NewGuid(),
            AssetClass.Futures,
            isActive: true,
            tickSize: 0.25m,
            tickValue: 12.50m);
        var tradeStore = new FakeTradeMutationStore(trade);
        var useCase = CreateUpdateUseCase(tradeStore, account, instrument);
        using var cts = new CancellationTokenSource();
        UpdateTradeCommand command = CreateCommand(
            trade,
            account.Id,
            instrument.Id,
            TradeDirection.Short,
            quantity: 2m,
            entryPrice: 101m,
            exitPrice: 99m);

        UpdateTradeResult result = await useCase.ExecuteAsync(command, cts.Token);

        Assert.True(result.WasChanged);
        Assert.Equal(1, tradeStore.SaveCount);
        Assert.Equal(cts.Token, tradeStore.GetToken);
        Assert.Equal(cts.Token, tradeStore.SaveToken);
        Assert.Equal(account.Id, trade.TradingAccountId);
        Assert.Equal(instrument.Id, trade.InstrumentId);
        Assert.Equal(50m, trade.Pricing.PointValue);
        Assert.Equal(TradeDirection.Short, trade.Direction);
        Assert.Equal(UpdatedAt, trade.UpdatedAtUtc);
        Assert.Equal("external-execution", trade.Executions[0].ExternalExecutionId);
        Assert.Equal("external-order", trade.Executions[0].ExternalOrderId);
        Assert.Equal("ESU6", trade.Executions[0].BrokerSymbol);
    }

    [Fact]
    public async Task UpdateNoOpDoesNotSaveOrChangeTimestamp()
    {
        Trade trade = CreateTrade();
        TradingAccount account = CreateAccount(trade.TradingAccountId, isActive: false);
        Instrument instrument = CreateInstrument(
            trade.InstrumentId,
            AssetClass.Equity,
            isActive: false,
            tickSize: 0.01m,
            tickValue: 0.10m);
        TradePricingSnapshot originalPricing = trade.Pricing;
        var tradeStore = new FakeTradeMutationStore(trade);
        var useCase = CreateUpdateUseCase(tradeStore, account, instrument);

        UpdateTradeResult result = await useCase.ExecuteAsync(CreateCommand(
            trade,
            account.Id,
            instrument.Id,
            TradeDirection.Long,
            1m,
            100m,
            105m));

        Assert.False(result.WasChanged);
        Assert.Equal(0, tradeStore.SaveCount);
        Assert.Equal(CreatedAt, trade.UpdatedAtUtc);
        Assert.Same(originalPricing, trade.Pricing);
    }

    [Fact]
    public async Task NewlySelectedInactiveReferencesAreRejected()
    {
        Trade trade = CreateTrade();
        TradingAccount inactiveAccount = CreateAccount(Guid.NewGuid(), isActive: false);
        Instrument instrument = CreateInstrument(
            trade.InstrumentId,
            AssetClass.Equity,
            isActive: true,
            tickSize: 0.01m,
            tickValue: 0.10m);
        var tradeStore = new FakeTradeMutationStore(trade);
        UpdateTradeUseCase useCase = CreateUpdateUseCase(
            tradeStore,
            inactiveAccount,
            instrument);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(CreateCommand(
                trade,
                inactiveAccount.Id,
                instrument.Id,
                TradeDirection.Long,
                1m,
                100m,
                105m)));

        Assert.Equal(UpdateTradeUseCase.InactiveAccountMessage, exception.Message);
        Assert.Equal(0, tradeStore.SaveCount);
    }

    [Fact]
    public async Task NewlySelectedInactiveInstrumentIsRejected()
    {
        Trade trade = CreateTrade();
        TradingAccount account = CreateAccount(trade.TradingAccountId, isActive: true);
        Instrument inactiveInstrument = CreateInstrument(
            Guid.NewGuid(),
            AssetClass.Equity,
            isActive: false,
            tickSize: 0.01m,
            tickValue: 0.10m);
        var tradeStore = new FakeTradeMutationStore(trade);
        UpdateTradeUseCase useCase = CreateUpdateUseCase(
            tradeStore,
            account,
            inactiveInstrument);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(CreateCommand(
                trade,
                account.Id,
                inactiveInstrument.Id,
                TradeDirection.Long,
                1m,
                100m,
                105m)));

        Assert.Equal(UpdateTradeUseCase.InactiveInstrumentMessage, exception.Message);
        Assert.Equal(0, tradeStore.SaveCount);
    }

    [Fact]
    public async Task CurrentInactiveSetupMayBePreservedButNewInactiveSetupIsRejected()
    {
        Guid currentSetupId = Guid.NewGuid();
        Trade trade = CreateTrade(currentSetupId);
        TradingAccount account = CreateAccount(trade.TradingAccountId, true);
        Instrument instrument = CreateInstrument(
            trade.InstrumentId,
            AssetClass.Equity,
            true,
            0.01m,
            0.10m);
        TradingSetup currentInactive = CreateSetup(currentSetupId, false);
        var tradeStore = new FakeTradeMutationStore(trade);
        UpdateTradeUseCase preserveUseCase = CreateUpdateUseCase(
            tradeStore,
            account,
            instrument,
            currentInactive);

        UpdateTradeResult preserved = await preserveUseCase.ExecuteAsync(
            CreateCommand(trade, account.Id, instrument.Id, TradeDirection.Long, 1m, 100m, 105m));

        Assert.False(preserved.WasChanged);

        TradingSetup anotherInactive = CreateSetup(Guid.NewGuid(), false);
        UpdateTradeUseCase rejectUseCase = CreateUpdateUseCase(
            tradeStore,
            account,
            instrument,
            anotherInactive);
        UpdateTradeCommand changedSetup = CreateCommand(
            trade,
            account.Id,
            instrument.Id,
            TradeDirection.Long,
            1m,
            100m,
            105m) with { TradingSetupId = anotherInactive.Id };

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => rejectUseCase.ExecuteAsync(changedSetup));
        Assert.Equal(UpdateTradeUseCase.InactiveSetupMessage, exception.Message);
    }

    [Fact]
    public async Task FuturesFractionalQuantityIsRejectedBeforeSave()
    {
        Trade trade = CreateTrade();
        TradingAccount account = CreateAccount(trade.TradingAccountId, true);
        Instrument futures = CreateInstrument(
            Guid.NewGuid(),
            AssetClass.Futures,
            true,
            0.25m,
            12.50m);
        var tradeStore = new FakeTradeMutationStore(trade);
        UpdateTradeUseCase useCase = CreateUpdateUseCase(
            tradeStore,
            account,
            futures);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ExecuteAsync(CreateCommand(
                trade,
                account.Id,
                futures.Id,
                TradeDirection.Long,
                1.5m,
                100m,
                105m)));

        Assert.Contains(
            TradeQuantityPolicy.FuturesWholeContractsMessage,
            exception.Message,
            StringComparison.Ordinal);
        Assert.Equal(0, tradeStore.SaveCount);
    }

    [Theory]
    [InlineData("account")]
    [InlineData("instrument")]
    [InlineData("setup")]
    public async Task MissingReferenceIsReportedBeforeSave(string missingReference)
    {
        Trade trade = CreateTrade();
        TradingAccount account = CreateAccount(trade.TradingAccountId, true);
        Instrument instrument = CreateInstrument(
            trade.InstrumentId,
            AssetClass.Equity,
            true,
            0.01m,
            0.10m);
        Guid missingSetupId = Guid.NewGuid();
        var tradeStore = new FakeTradeMutationStore(trade);
        UpdateTradeUseCase useCase = CreateUpdateUseCase(
            tradeStore,
            missingReference == "account" ? null : account,
            missingReference == "instrument" ? null : instrument,
            setup: null);
        UpdateTradeCommand command = CreateCommand(
            trade,
            account.Id,
            instrument.Id,
            TradeDirection.Long,
            1m,
            100m,
            105m);
        if (missingReference == "setup")
        {
            command = command with { TradingSetupId = missingSetupId };
        }

        KeyNotFoundException exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => useCase.ExecuteAsync(command));

        Assert.Contains(missingReference, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, tradeStore.SaveCount);
    }

    [Fact]
    public async Task ActiveSetupCanBeAssignedAndClearedThroughCorrection()
    {
        Trade trade = CreateTrade();
        TradingAccount account = CreateAccount(trade.TradingAccountId, true);
        Instrument instrument = CreateInstrument(
            trade.InstrumentId,
            AssetClass.Equity,
            true,
            0.01m,
            0.10m);
        TradingSetup setup = CreateSetup(Guid.NewGuid(), true);
        var tradeStore = new FakeTradeMutationStore(trade);
        UpdateTradeUseCase useCase = CreateUpdateUseCase(
            tradeStore,
            account,
            instrument,
            setup);

        UpdateTradeResult assigned = await useCase.ExecuteAsync(
            CreateCommand(trade, account.Id, instrument.Id, TradeDirection.Long, 1m, 100m, 105m)
                with { TradingSetupId = setup.Id });

        Assert.True(assigned.WasChanged);
        Assert.Equal(setup.Id, trade.TradingSetupId);
        Assert.Equal(1, tradeStore.SaveCount);

        UpdateTradeResult cleared = await useCase.ExecuteAsync(
            CreateCommand(trade, account.Id, instrument.Id, TradeDirection.Long, 1m, 100m, 105m)
                with { TradingSetupId = null });

        Assert.True(cleared.WasChanged);
        Assert.Null(trade.TradingSetupId);
        Assert.Equal(2, tradeStore.SaveCount);
    }

    [Fact]
    public async Task MissingTradeFailsBeforeReferenceLookups()
    {
        var tradeStore = new FakeTradeMutationStore(null);
        TradingAccount account = CreateAccount(Guid.NewGuid(), true);
        Instrument instrument = CreateInstrument(Guid.NewGuid(), AssetClass.Equity, true, 0.01m, 0.10m);
        UpdateTradeUseCase useCase = CreateUpdateUseCase(tradeStore, account, instrument);
        Trade detached = CreateTrade();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            useCase.ExecuteAsync(CreateCommand(
                detached,
                account.Id,
                instrument.Id,
                TradeDirection.Long,
                1m,
                100m,
                105m)));

        Assert.Equal(0, tradeStore.SaveCount);
    }

    [Fact]
    public async Task DeleteCleansEveryScreenshotAfterDatabaseDelete()
    {
        Guid tradeId = Guid.NewGuid();
        var deletionStore = new FakeTradeDeletionStore(
            new TradeDeletionInfo(tradeId, ["one.png", "two.jpg"]));
        var storage = new FakeScreenshotStorage();
        var useCase = new DeleteTradeUseCase(deletionStore, storage);
        using var cts = new CancellationTokenSource();

        DeleteTradeResult result = await useCase.ExecuteAsync(tradeId, cts.Token);

        Assert.Equal(DeleteTradeResult.Deleted, result);
        Assert.Equal(cts.Token, deletionStore.Token);
        Assert.Equal(["one.png", "two.jpg"], storage.DeletedKeys);
        Assert.All(storage.DeleteTokens, token => Assert.Equal(CancellationToken.None, token));
    }

    [Fact]
    public async Task DeleteReportsCleanupWarningAndContinuesRemainingFiles()
    {
        Guid tradeId = Guid.NewGuid();
        var deletionStore = new FakeTradeDeletionStore(
            new TradeDeletionInfo(tradeId, ["broken.png", "good.png"]));
        var storage = new FakeScreenshotStorage { FailingKey = "broken.png" };
        var useCase = new DeleteTradeUseCase(deletionStore, storage);

        DeleteTradeResult result = await useCase.ExecuteAsync(tradeId);

        Assert.Equal(DeleteTradeResult.DeletedWithFileCleanupWarning, result);
        Assert.Equal(["broken.png", "good.png"], storage.DeletedKeys);
    }

    [Fact]
    public async Task DeleteMissingTradeThrowsAndDoesNotTouchStorage()
    {
        var deletionStore = new FakeTradeDeletionStore(null);
        var storage = new FakeScreenshotStorage();
        var useCase = new DeleteTradeUseCase(deletionStore, storage);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            useCase.ExecuteAsync(Guid.NewGuid()));

        Assert.Empty(storage.DeletedKeys);
    }

    [Fact]
    public async Task DeleteWithoutScreenshotsCommitsWithoutFileOperations()
    {
        Guid tradeId = Guid.NewGuid();
        var deletionStore = new FakeTradeDeletionStore(
            new TradeDeletionInfo(tradeId, []));
        var storage = new FakeScreenshotStorage();
        var useCase = new DeleteTradeUseCase(deletionStore, storage);

        DeleteTradeResult result = await useCase.ExecuteAsync(tradeId);

        Assert.Equal(DeleteTradeResult.Deleted, result);
        Assert.Empty(storage.DeletedKeys);
    }

    private static UpdateTradeUseCase CreateUpdateUseCase(
        FakeTradeMutationStore tradeStore,
        TradingAccount? account,
        Instrument? instrument,
        TradingSetup? setup = null) =>
        new(
            tradeStore,
            new FakeAccountStore(account),
            new FakeInstrumentStore(instrument),
            new FakeSetupStore(setup),
            new FixedTimeProvider(UpdatedAt));

    private static UpdateTradeCommand CreateCommand(
        Trade trade,
        Guid accountId,
        Guid instrumentId,
        TradeDirection direction,
        decimal quantity,
        decimal entryPrice,
        decimal exitPrice) =>
        new(
            trade.Id,
            accountId,
            instrumentId,
            trade.TradingSetupId,
            direction,
            quantity,
            new UpdateTradeExecutionInput(
                trade.Executions[0].Id,
                trade.Executions[0].ExecutedAtUtc,
                entryPrice,
                trade.Executions[0].Commission,
                trade.Executions[0].Fees),
            new UpdateTradeExecutionInput(
                trade.Executions[1].Id,
                trade.Executions[1].ExecutedAtUtc,
                exitPrice,
                trade.Executions[1].Commission,
                trade.Executions[1].Fees));

    private static Trade CreateTrade(Guid? setupId = null)
    {
        Guid tradeId = Guid.NewGuid();
        return Trade.Rehydrate(
            tradeId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new TradePricingSnapshot(10m, "USD"),
            setupId,
            [
                TradeExecution.Rehydrate(
                    Guid.NewGuid(), tradeId, 1, CreatedAt, ExecutionSide.Buy,
                    1m, 100m, 1m, 0m, "external-execution", "external-order", "ESU6"),
                TradeExecution.Rehydrate(
                    Guid.NewGuid(), tradeId, 2, CreatedAt.AddHours(1), ExecutionSide.Sell,
                    1m, 105m, 1m, 0m, null, null, null),
            ],
            CreatedAt,
            CreatedAt);
    }

    private static TradingAccount CreateAccount(Guid id, bool isActive) =>
        TradingAccount.Rehydrate(
            id, "Account", TradingAccountType.Personal, null, null, "USD", null,
            isActive, CreatedAt, CreatedAt);

    private static Instrument CreateInstrument(
        Guid id,
        AssetClass assetClass,
        bool isActive,
        decimal tickSize,
        decimal tickValue) =>
        Instrument.Rehydrate(
            id, "ES", "Instrument", assetClass, null, "USD", tickSize, tickValue,
            isActive, CreatedAt, CreatedAt);

    private static TradingSetup CreateSetup(Guid id, bool isActive) =>
        TradingSetup.Rehydrate(id, "Setup", null, isActive, CreatedAt, CreatedAt);

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class FakeTradeMutationStore(Trade? trade) : ITradeMutationStore
    {
        public int SaveCount { get; private set; }
        public CancellationToken GetToken { get; private set; }
        public CancellationToken SaveToken { get; private set; }

        public Task<Trade?> GetByIdAsync(Guid tradeId, CancellationToken cancellationToken = default)
        {
            GetToken = cancellationToken;
            return Task.FromResult(trade);
        }

        public Task SaveAsync(Trade value, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            SaveToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAccountStore(TradingAccount? account) : ITradingAccountStore
    {
        public Task AddAsync(TradingAccount value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TradingAccount?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(account?.Id == accountId ? account : null);
        public Task UpdateAsync(TradingAccount value, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeInstrumentStore(Instrument? instrument) : IInstrumentStore
    {
        public Task AddAsync(Instrument value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Instrument?> GetByIdAsync(Guid instrumentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(instrument?.Id == instrumentId ? instrument : null);
        public Task UpdateAsync(Instrument value, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeSetupStore(TradingSetup? setup) : ITradingSetupStore
    {
        public Task AddAsync(TradingSetup value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TradingSetup?> GetByIdAsync(Guid setupId, CancellationToken cancellationToken = default) =>
            Task.FromResult(setup?.Id == setupId ? setup : null);
        public Task UpdateAsync(TradingSetup value, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeTradeDeletionStore(TradeDeletionInfo? result) : ITradeDeletionStore
    {
        public CancellationToken Token { get; private set; }

        public Task<TradeDeletionInfo?> DeleteAsync(Guid tradeId, CancellationToken cancellationToken = default)
        {
            Token = cancellationToken;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeScreenshotStorage : ITradeScreenshotFileStorage
    {
        public string? FailingKey { get; init; }
        public List<string> DeletedKeys { get; } = [];
        public List<CancellationToken> DeleteTokens { get; } = [];

        public Task<string> StoreAsync(Stream content, string fileExtension, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteIfExistsAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            DeletedKeys.Add(storageKey);
            DeleteTokens.Add(cancellationToken);
            return storageKey == FailingKey
                ? Task.FromException(new IOException("Cleanup failed."))
                : Task.CompletedTask;
        }
    }
}
