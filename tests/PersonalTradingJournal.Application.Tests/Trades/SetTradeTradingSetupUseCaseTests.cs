using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Tests.Trades;

public sealed class SetTradeTradingSetupUseCaseTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 15, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CurrentUtc =
        new(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteAsyncRejectsNullCommandBeforeStoreAccess()
    {
        var tradeStore = new RecordingTradeMutationStore();
        var setupStore = new RecordingTradingSetupStore();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => CreateUseCase(tradeStore, setupStore).ExecuteAsync(null!));

        Assert.Equal(0, tradeStore.GetCallCount);
        Assert.Equal(0, setupStore.GetCallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteAsyncRejectsEmptyIdentifiersBeforeStoreAccess(
        bool emptyTradeId)
    {
        var tradeStore = new RecordingTradeMutationStore();
        var setupStore = new RecordingTradingSetupStore();
        var command = new SetTradeTradingSetupCommand(
            emptyTradeId ? Guid.Empty : Guid.NewGuid(),
            emptyTradeId ? Guid.NewGuid() : Guid.Empty);

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateUseCase(tradeStore, setupStore).ExecuteAsync(command));

        Assert.Equal(0, tradeStore.GetCallCount);
        Assert.Equal(0, setupStore.GetCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncThrowsForMissingTradeWithoutSaving()
    {
        Guid tradeId = Guid.NewGuid();
        var tradeStore = new RecordingTradeMutationStore();

        KeyNotFoundException exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => CreateUseCase(tradeStore, new RecordingTradingSetupStore())
                .ExecuteAsync(new(tradeId, null)));

        Assert.Contains(tradeId.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, tradeStore.SaveCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncAssignsActiveSetupAndUsesTimeProvider()
    {
        Trade trade = CreateTrade();
        TradingSetup setup = CreateSetup();
        var tradeStore = new RecordingTradeMutationStore { TradeToReturn = trade };
        var setupStore = new RecordingTradingSetupStore { SetupToReturn = setup };

        await CreateUseCase(tradeStore, setupStore)
            .ExecuteAsync(new(trade.Id, setup.Id));

        Assert.Equal(setup.Id, trade.TradingSetupId);
        Assert.Equal(CurrentUtc, trade.UpdatedAtUtc);
        Assert.Same(trade, tradeStore.SavedTrade);
        Assert.Equal(1, tradeStore.SaveCallCount);
        Assert.Equal(setup.Id, setupStore.RequestedSetupId);
    }

    [Fact]
    public async Task ExecuteAsyncClearsAssignmentWithoutLoadingSetup()
    {
        Trade trade = CreateTrade();
        TradingSetup setup = CreateSetup();
        trade.SetTradingSetup(setup.Id, CreatedAtUtc.AddMinutes(1));
        var tradeStore = new RecordingTradeMutationStore { TradeToReturn = trade };
        var setupStore = new RecordingTradingSetupStore();

        await CreateUseCase(tradeStore, setupStore)
            .ExecuteAsync(new(trade.Id, null));

        Assert.Null(trade.TradingSetupId);
        Assert.Equal(CurrentUtc, trade.UpdatedAtUtc);
        Assert.Equal(0, setupStore.GetCallCount);
        Assert.Equal(1, tradeStore.SaveCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncAllowsInactiveHistoricalSetupToBeCleared()
    {
        Trade trade = CreateTrade();
        TradingSetup inactiveSetup = CreateSetup(active: false);
        trade.SetTradingSetup(inactiveSetup.Id, CreatedAtUtc.AddMinutes(1));
        var tradeStore = new RecordingTradeMutationStore { TradeToReturn = trade };

        await CreateUseCase(tradeStore, new RecordingTradingSetupStore())
            .ExecuteAsync(new(trade.Id, null));

        Assert.Null(trade.TradingSetupId);
        Assert.Equal(1, tradeStore.SaveCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncReplacesInactiveHistoricalSetupWithActiveSetup()
    {
        Trade trade = CreateTrade();
        TradingSetup inactiveSetup = CreateSetup(active: false);
        TradingSetup activeSetup = CreateSetup();
        trade.SetTradingSetup(inactiveSetup.Id, CreatedAtUtc.AddMinutes(1));
        var tradeStore = new RecordingTradeMutationStore { TradeToReturn = trade };

        await CreateUseCase(
                tradeStore,
                new RecordingTradingSetupStore { SetupToReturn = activeSetup })
            .ExecuteAsync(new(trade.Id, activeSetup.Id));

        Assert.Equal(activeSetup.Id, trade.TradingSetupId);
        Assert.Equal(1, tradeStore.SaveCallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsyncRejectsMissingOrInactiveSetupWithoutMutation(
        bool inactive)
    {
        Trade trade = CreateTrade();
        Guid requestedSetupId = Guid.NewGuid();
        TradingSetup? setup = inactive ? CreateSetup(active: false) : null;
        if (setup is not null)
        {
            requestedSetupId = setup.Id;
        }

        var tradeStore = new RecordingTradeMutationStore { TradeToReturn = trade };
        var setupStore = new RecordingTradingSetupStore { SetupToReturn = setup };

        Exception exception = inactive
            ? await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateUseCase(tradeStore, setupStore)
                    .ExecuteAsync(new(trade.Id, requestedSetupId)))
            : await Assert.ThrowsAsync<KeyNotFoundException>(
                () => CreateUseCase(tradeStore, setupStore)
                    .ExecuteAsync(new(trade.Id, requestedSetupId)));

        Assert.Equal(
            inactive
                ? "The selected trading setup is inactive."
                : "The selected trading setup was not found.",
            exception.Message);
        Assert.Null(trade.TradingSetupId);
        Assert.Equal(CreatedAtUtc, trade.UpdatedAtUtc);
        Assert.Equal(0, tradeStore.SaveCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncSameActiveAssignmentIsNoOpWithoutSave()
    {
        Trade trade = CreateTrade();
        TradingSetup setup = CreateSetup();
        trade.SetTradingSetup(setup.Id, CreatedAtUtc.AddMinutes(1));
        DateTimeOffset unchangedUpdatedAtUtc = trade.UpdatedAtUtc;
        var tradeStore = new RecordingTradeMutationStore { TradeToReturn = trade };
        var setupStore = new RecordingTradingSetupStore { SetupToReturn = setup };

        await CreateUseCase(tradeStore, setupStore)
            .ExecuteAsync(new(trade.Id, setup.Id));

        Assert.Equal(unchangedUpdatedAtUtc, trade.UpdatedAtUtc);
        Assert.Equal(1, setupStore.GetCallCount);
        Assert.Equal(0, tradeStore.SaveCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncPropagatesPersistenceFailure()
    {
        Trade trade = CreateTrade();
        TradingSetup setup = CreateSetup();
        var expected = new InvalidOperationException("save failed");
        var tradeStore = new RecordingTradeMutationStore
        {
            TradeToReturn = trade,
            SaveException = expected,
        };

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateUseCase(
                    tradeStore,
                    new RecordingTradingSetupStore { SetupToReturn = setup })
                .ExecuteAsync(new(trade.Id, setup.Id)));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task ExecuteAsyncForwardsCancellationTokenToEveryRequiredStore()
    {
        Trade trade = CreateTrade();
        TradingSetup setup = CreateSetup();
        var tradeStore = new RecordingTradeMutationStore { TradeToReturn = trade };
        var setupStore = new RecordingTradingSetupStore { SetupToReturn = setup };
        using var cancellationSource = new CancellationTokenSource();

        await CreateUseCase(tradeStore, setupStore).ExecuteAsync(
            new(trade.Id, setup.Id),
            cancellationSource.Token);

        Assert.Equal(cancellationSource.Token, tradeStore.GetCancellationToken);
        Assert.Equal(cancellationSource.Token, setupStore.GetCancellationToken);
        Assert.Equal(cancellationSource.Token, tradeStore.SaveCancellationToken);
    }

    [Fact]
    public async Task ExecuteAsyncPropagatesCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var expected = new OperationCanceledException(cancellationSource.Token);
        var tradeStore = new RecordingTradeMutationStore { GetException = expected };

        OperationCanceledException actual =
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => CreateUseCase(tradeStore, new RecordingTradingSetupStore())
                    .ExecuteAsync(
                        new(Guid.NewGuid(), null),
                        cancellationSource.Token));

        Assert.Same(expected, actual);
        Assert.Equal(0, tradeStore.SaveCallCount);
    }

    private static SetTradeTradingSetupUseCase CreateUseCase(
        RecordingTradeMutationStore tradeStore,
        RecordingTradingSetupStore setupStore) =>
        new(tradeStore, setupStore, new FixedTimeProvider(CurrentUtc));

    private static Trade CreateTrade()
    {
        Guid tradeId = Guid.NewGuid();
        var execution = new TradeExecution(
            tradeId,
            1,
            CreatedAtUtc.AddMinutes(-1),
            ExecutionSide.Buy,
            1m,
            100m,
            0m,
            0m,
            null,
            null,
            null);

        return Trade.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new TradePricingSnapshot(20m, "USD"),
            execution,
            CreatedAtUtc);
    }

    private static TradingSetup CreateSetup(bool active = true)
    {
        var setup = new TradingSetup("Silver Bullet", null, CreatedAtUtc);
        if (!active)
        {
            setup.Deactivate(CreatedAtUtc.AddMinutes(1));
        }

        return setup;
    }

    private sealed class RecordingTradeMutationStore : ITradeMutationStore
    {
        public Trade? TradeToReturn { get; set; }
        public Trade? SavedTrade { get; private set; }
        public Exception? GetException { get; set; }
        public Exception? SaveException { get; set; }
        public int GetCallCount { get; private set; }
        public int SaveCallCount { get; private set; }
        public CancellationToken GetCancellationToken { get; private set; }
        public CancellationToken SaveCancellationToken { get; private set; }

        public Task<Trade?> GetByIdAsync(
            Guid tradeId,
            CancellationToken cancellationToken = default)
        {
            GetCallCount++;
            GetCancellationToken = cancellationToken;
            return GetException is null
                ? Task.FromResult(TradeToReturn)
                : Task.FromException<Trade?>(GetException);
        }

        public Task SaveAsync(
            Trade trade,
            CancellationToken cancellationToken = default)
        {
            SaveCallCount++;
            SavedTrade = trade;
            SaveCancellationToken = cancellationToken;
            return SaveException is null
                ? Task.CompletedTask
                : Task.FromException(SaveException);
        }
    }

    private sealed class RecordingTradingSetupStore : ITradingSetupStore
    {
        public TradingSetup? SetupToReturn { get; set; }
        public int GetCallCount { get; private set; }
        public Guid? RequestedSetupId { get; private set; }
        public CancellationToken GetCancellationToken { get; private set; }

        public Task AddAsync(
            TradingSetup setup,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TradingSetup?> GetByIdAsync(
            Guid setupId,
            CancellationToken cancellationToken = default)
        {
            GetCallCount++;
            RequestedSetupId = setupId;
            GetCancellationToken = cancellationToken;
            return Task.FromResult(SetupToReturn);
        }

        public Task UpdateAsync(
            TradingSetup setup,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
