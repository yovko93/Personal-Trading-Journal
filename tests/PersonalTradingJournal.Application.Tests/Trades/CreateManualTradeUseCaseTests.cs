using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Tests.Trades;

public sealed class CreateManualTradeUseCaseTests
{
    private static readonly DateTimeOffset ReferenceCreatedAtUtc =
        new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset EntryExecutedAtUtc =
        new(2026, 9, 9, 14, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ExitExecutedAtUtc =
        new(2026, 9, 9, 15, 45, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset CurrentUtc =
        new(2026, 9, 10, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteAsyncCreatesOpenLongTrade()
    {
        TradingAccount account = CreateTradingAccount();
        Instrument instrument = CreateInstrument();
        var tradeStore = new RecordingTradeStore();
        CreateManualTradeUseCase useCase = CreateUseCase(account, instrument, tradeStore);
        var command = CreateCommand(account.Id, instrument.Id, TradeDirection.Long);

        Guid tradeId = await useCase.ExecuteAsync(command);

        Trade trade = Assert.IsType<Trade>(tradeStore.AddedTrade);
        TradeExecution execution = Assert.Single(trade.Executions);
        Assert.NotEqual(Guid.Empty, tradeId);
        Assert.Equal(trade.Id, tradeId);
        Assert.Equal(account.Id, trade.TradingAccountId);
        Assert.Equal(instrument.Id, trade.InstrumentId);
        Assert.Equal(TradeDirection.Long, trade.Direction);
        Assert.Equal(TradeStatus.Open, trade.Status);
        Assert.Equal(command.Quantity, trade.OpenQuantity);
        Assert.Equal(1, execution.Sequence);
        Assert.Equal(ExecutionSide.Buy, execution.Side);
        Assert.Equal(command.Entry.ExecutedAtUtc, execution.ExecutedAtUtc);
        Assert.Equal(command.Entry.Price, execution.Price);
        Assert.Equal(command.Entry.Commission, execution.Commission);
        Assert.Equal(command.Entry.Fees, execution.Fees);
        Assert.Null(execution.ExternalExecutionId);
        Assert.Null(execution.ExternalOrderId);
        Assert.Null(execution.BrokerSymbol);
        Assert.Null(trade.StrategyId);
        Assert.Null(trade.TradingSetupId);
    }

    [Fact]
    public async Task ExecuteAsyncCreatesOpenShortTrade()
    {
        TradingAccount account = CreateTradingAccount();
        Instrument instrument = CreateInstrument();
        var tradeStore = new RecordingTradeStore();
        CreateManualTradeUseCase useCase = CreateUseCase(account, instrument, tradeStore);

        await useCase.ExecuteAsync(
            CreateCommand(account.Id, instrument.Id, TradeDirection.Short));

        Trade trade = Assert.IsType<Trade>(tradeStore.AddedTrade);
        Assert.Equal(TradeDirection.Short, trade.Direction);
        Assert.Equal(TradeStatus.Open, trade.Status);
        Assert.Equal(ExecutionSide.Sell, Assert.Single(trade.Executions).Side);
    }

    [Fact]
    public async Task ExecuteAsyncCreatesClosedLongTrade()
    {
        TradingAccount account = CreateTradingAccount();
        Instrument instrument = CreateInstrument();
        var tradeStore = new RecordingTradeStore();
        CreateManualTradeUseCase useCase = CreateUseCase(account, instrument, tradeStore);
        CreateManualTradeCommand command = CreateCommand(
            account.Id,
            instrument.Id,
            TradeDirection.Long,
            new ManualTradeExecutionInput(ExitExecutedAtUtc, 21950.75m, 1.75m, 0.35m));

        await useCase.ExecuteAsync(command);

        Trade trade = Assert.IsType<Trade>(tradeStore.AddedTrade);
        Assert.Equal(TradeStatus.Closed, trade.Status);
        Assert.Equal(0m, trade.OpenQuantity);
        Assert.Equal(2, trade.Executions.Count);
        TradeExecution entry = trade.Executions[0];
        TradeExecution exit = trade.Executions[1];
        Assert.Equal(1, entry.Sequence);
        Assert.Equal(2, exit.Sequence);
        Assert.Equal(ExecutionSide.Buy, entry.Side);
        Assert.Equal(ExecutionSide.Sell, exit.Side);
        Assert.Equal(trade.Id, entry.TradeId);
        Assert.Equal(trade.Id, exit.TradeId);
        Assert.Equal(command.Quantity, entry.Quantity);
        Assert.Equal(command.Quantity, exit.Quantity);
        Assert.Equal(command.Entry.ExecutedAtUtc, entry.ExecutedAtUtc);
        Assert.Equal(command.Exit!.ExecutedAtUtc, exit.ExecutedAtUtc);
        Assert.Equal(command.Entry.Price, entry.Price);
        Assert.Equal(command.Exit.Price, exit.Price);
        Assert.Equal(command.Entry.Commission, entry.Commission);
        Assert.Equal(command.Entry.Fees, entry.Fees);
        Assert.Equal(command.Exit.Commission, exit.Commission);
        Assert.Equal(command.Exit.Fees, exit.Fees);
    }

    [Fact]
    public async Task ExecuteAsyncCreatesClosedShortTrade()
    {
        TradingAccount account = CreateTradingAccount();
        Instrument instrument = CreateInstrument();
        var tradeStore = new RecordingTradeStore();
        CreateManualTradeUseCase useCase = CreateUseCase(account, instrument, tradeStore);

        await useCase.ExecuteAsync(
            CreateCommand(
                account.Id,
                instrument.Id,
                TradeDirection.Short,
                new ManualTradeExecutionInput(ExitExecutedAtUtc, 21850m, 1m, 0.25m)));

        Trade trade = Assert.IsType<Trade>(tradeStore.AddedTrade);
        Assert.Equal(TradeStatus.Closed, trade.Status);
        Assert.Equal(ExecutionSide.Sell, trade.Executions[0].Side);
        Assert.Equal(ExecutionSide.Buy, trade.Executions[1].Side);
    }

    [Fact]
    public async Task ExecuteAsyncSnapshotsCurrentInstrumentEconomics()
    {
        TradingAccount account = CreateTradingAccount();
        Instrument instrument = CreateInstrument();
        var tradeStore = new RecordingTradeStore();
        CreateManualTradeUseCase useCase = CreateUseCase(account, instrument, tradeStore);

        await useCase.ExecuteAsync(CreateCommand(account.Id, instrument.Id));

        Trade trade = Assert.IsType<Trade>(tradeStore.AddedTrade);
        Assert.Equal(20m, trade.Pricing.PointValue);
        Assert.Equal("USD", trade.Pricing.Currency);
    }

    [Fact]
    public async Task ExecuteAsyncThrowsWhenTradingAccountIsMissing()
    {
        var accountStore = new StubTradingAccountStore(null);
        var instrumentStore = new StubInstrumentStore(CreateInstrument());
        var tradeStore = new RecordingTradeStore();
        var useCase = new CreateManualTradeUseCase(
            accountStore,
            instrumentStore,
            tradeStore,
            new FixedTimeProvider(CurrentUtc));
        CreateManualTradeCommand command = CreateCommand(Guid.NewGuid(), Guid.NewGuid());

        KeyNotFoundException exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => useCase.ExecuteAsync(command));

        Assert.Contains(command.TradingAccountId.ToString(), exception.Message);
        Assert.Contains("Trading Account", exception.Message);
        Assert.Equal(1, accountStore.GetByIdCallCount);
        Assert.Equal(0, instrumentStore.GetByIdCallCount);
        Assert.Equal(0, tradeStore.AddCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncThrowsWhenInstrumentIsMissing()
    {
        TradingAccount account = CreateTradingAccount();
        var accountStore = new StubTradingAccountStore(account);
        var instrumentStore = new StubInstrumentStore(null);
        var tradeStore = new RecordingTradeStore();
        var useCase = new CreateManualTradeUseCase(
            accountStore,
            instrumentStore,
            tradeStore,
            new FixedTimeProvider(CurrentUtc));
        CreateManualTradeCommand command = CreateCommand(account.Id, Guid.NewGuid());

        KeyNotFoundException exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => useCase.ExecuteAsync(command));

        Assert.Contains(command.InstrumentId.ToString(), exception.Message);
        Assert.Equal(1, accountStore.GetByIdCallCount);
        Assert.Equal(1, instrumentStore.GetByIdCallCount);
        Assert.Equal(0, tradeStore.AddCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncRejectsEmptyTradingAccountIdBeforeLoadingReferences()
    {
        var accountStore = new StubTradingAccountStore(CreateTradingAccount());
        var instrumentStore = new StubInstrumentStore(CreateInstrument());
        var tradeStore = new RecordingTradeStore();
        var useCase = new CreateManualTradeUseCase(
            accountStore,
            instrumentStore,
            tradeStore,
            new FixedTimeProvider(CurrentUtc));

        await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ExecuteAsync(CreateCommand(Guid.Empty, Guid.NewGuid())));

        Assert.Equal(0, accountStore.GetByIdCallCount);
        Assert.Equal(0, instrumentStore.GetByIdCallCount);
        Assert.Equal(0, tradeStore.AddCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncRejectsEmptyInstrumentIdBeforeLoadingReferences()
    {
        var accountStore = new StubTradingAccountStore(CreateTradingAccount());
        var instrumentStore = new StubInstrumentStore(CreateInstrument());
        var tradeStore = new RecordingTradeStore();
        var useCase = new CreateManualTradeUseCase(
            accountStore,
            instrumentStore,
            tradeStore,
            new FixedTimeProvider(CurrentUtc));

        await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ExecuteAsync(CreateCommand(Guid.NewGuid(), Guid.Empty)));

        Assert.Equal(0, accountStore.GetByIdCallCount);
        Assert.Equal(0, instrumentStore.GetByIdCallCount);
        Assert.Equal(0, tradeStore.AddCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncPropagatesInvalidQuantityWithoutPersisting()
    {
        TradingAccount account = CreateTradingAccount();
        Instrument instrument = CreateInstrument();
        var tradeStore = new RecordingTradeStore();
        CreateManualTradeUseCase useCase = CreateUseCase(account, instrument, tradeStore);
        CreateManualTradeCommand command = CreateCommand(account.Id, instrument.Id) with
        {
            Quantity = 0m
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => useCase.ExecuteAsync(command));

        Assert.Equal(0, tradeStore.AddCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncDoesNotNormalizeNonUtcExecutionTimestamp()
    {
        TradingAccount account = CreateTradingAccount();
        Instrument instrument = CreateInstrument();
        var tradeStore = new RecordingTradeStore();
        CreateManualTradeUseCase useCase = CreateUseCase(account, instrument, tradeStore);
        var nonUtcEntry = new ManualTradeExecutionInput(
            new DateTimeOffset(2026, 9, 9, 17, 30, 0, TimeSpan.FromHours(3)),
            21900.25m,
            1.50m,
            0.25m);
        CreateManualTradeCommand command = CreateCommand(account.Id, instrument.Id) with
        {
            Entry = nonUtcEntry
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ExecuteAsync(command));

        Assert.Equal(0, tradeStore.AddCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncPropagatesBackwardExitTimestampWithoutPersisting()
    {
        TradingAccount account = CreateTradingAccount();
        Instrument instrument = CreateInstrument();
        var tradeStore = new RecordingTradeStore();
        CreateManualTradeUseCase useCase = CreateUseCase(account, instrument, tradeStore);
        var earlierExit = new ManualTradeExecutionInput(
            EntryExecutedAtUtc.AddMinutes(-1),
            21800m,
            1m,
            0.25m);

        await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ExecuteAsync(
                CreateCommand(
                    account.Id,
                    instrument.Id,
                    TradeDirection.Long,
                    earlierExit)));

        Assert.Equal(0, tradeStore.AddCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncPropagatesPersistenceFailure()
    {
        TradingAccount account = CreateTradingAccount();
        Instrument instrument = CreateInstrument();
        var expectedException = new InvalidOperationException("Persistence failed.");
        var tradeStore = new RecordingTradeStore(expectedException);
        CreateManualTradeUseCase useCase = CreateUseCase(account, instrument, tradeStore);

        InvalidOperationException actualException =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => useCase.ExecuteAsync(CreateCommand(account.Id, instrument.Id)));

        Assert.Same(expectedException, actualException);
        Assert.Equal(1, tradeStore.AddCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncUsesTimeProviderOnlyForAggregateAuditTimestamps()
    {
        TradingAccount account = CreateTradingAccount();
        Instrument instrument = CreateInstrument();
        var tradeStore = new RecordingTradeStore();
        CreateManualTradeUseCase useCase = CreateUseCase(account, instrument, tradeStore);
        CreateManualTradeCommand command = CreateCommand(
            account.Id,
            instrument.Id,
            TradeDirection.Long,
            new ManualTradeExecutionInput(ExitExecutedAtUtc, 21950m, 1m, 0.25m));

        await useCase.ExecuteAsync(command);

        Trade trade = Assert.IsType<Trade>(tradeStore.AddedTrade);
        Assert.Equal(CurrentUtc, trade.CreatedAtUtc);
        Assert.Equal(CurrentUtc, trade.UpdatedAtUtc);
        Assert.Equal(EntryExecutedAtUtc, trade.Executions[0].ExecutedAtUtc);
        Assert.Equal(ExitExecutedAtUtc, trade.Executions[1].ExecutedAtUtc);
    }

    [Fact]
    public async Task ExecuteAsyncForwardsCancellationTokenToEveryStore()
    {
        TradingAccount account = CreateTradingAccount();
        Instrument instrument = CreateInstrument();
        var accountStore = new StubTradingAccountStore(account);
        var instrumentStore = new StubInstrumentStore(instrument);
        var tradeStore = new RecordingTradeStore();
        var useCase = new CreateManualTradeUseCase(
            accountStore,
            instrumentStore,
            tradeStore,
            new FixedTimeProvider(CurrentUtc));
        using var cancellationSource = new CancellationTokenSource();

        await useCase.ExecuteAsync(
            CreateCommand(account.Id, instrument.Id),
            cancellationSource.Token);

        Assert.Equal(cancellationSource.Token, accountStore.CancellationToken);
        Assert.Equal(cancellationSource.Token, instrumentStore.CancellationToken);
        Assert.Equal(cancellationSource.Token, tradeStore.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsyncAllowsInactiveReferenceDataForHistoricalEntry()
    {
        TradingAccount account = CreateTradingAccount();
        account.Deactivate(ReferenceCreatedAtUtc.AddDays(1));
        Instrument instrument = CreateInstrument();
        instrument.Deactivate(ReferenceCreatedAtUtc.AddDays(1));
        var tradeStore = new RecordingTradeStore();
        CreateManualTradeUseCase useCase = CreateUseCase(account, instrument, tradeStore);

        await useCase.ExecuteAsync(CreateCommand(account.Id, instrument.Id));

        Assert.NotNull(tradeStore.AddedTrade);
    }

    private static CreateManualTradeUseCase CreateUseCase(
        TradingAccount account,
        Instrument instrument,
        RecordingTradeStore tradeStore)
    {
        return new CreateManualTradeUseCase(
            new StubTradingAccountStore(account),
            new StubInstrumentStore(instrument),
            tradeStore,
            new FixedTimeProvider(CurrentUtc));
    }

    private static CreateManualTradeCommand CreateCommand(
        Guid tradingAccountId,
        Guid instrumentId,
        TradeDirection direction = TradeDirection.Long,
        ManualTradeExecutionInput? exit = null)
    {
        return new CreateManualTradeCommand(
            tradingAccountId,
            instrumentId,
            direction,
            2.5m,
            new ManualTradeExecutionInput(
                EntryExecutedAtUtc,
                21900.25m,
                1.50m,
                0.25m),
            exit);
    }

    private static TradingAccount CreateTradingAccount()
    {
        return new TradingAccount(
            "Primary Account",
            TradingAccountType.Personal,
            null,
            null,
            "USD",
            100000m,
            ReferenceCreatedAtUtc);
    }

    private static Instrument CreateInstrument()
    {
        return new Instrument(
            "NQ",
            "Nasdaq-100 E-mini",
            AssetClass.Futures,
            "CME",
            "USD",
            0.25m,
            5m,
            ReferenceCreatedAtUtc);
    }

    private sealed class StubTradingAccountStore : ITradingAccountStore
    {
        private readonly TradingAccount? _account;

        public StubTradingAccountStore(TradingAccount? account)
        {
            _account = account;
        }

        public int GetByIdCallCount { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task AddAsync(
            TradingAccount account,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<TradingAccount?> GetByIdAsync(
            Guid accountId,
            CancellationToken cancellationToken = default)
        {
            GetByIdCallCount++;
            CancellationToken = cancellationToken;
            return Task.FromResult(_account);
        }

        public Task UpdateAsync(
            TradingAccount account,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubInstrumentStore : IInstrumentStore
    {
        private readonly Instrument? _instrument;

        public StubInstrumentStore(Instrument? instrument)
        {
            _instrument = instrument;
        }

        public int GetByIdCallCount { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task AddAsync(
            Instrument instrument,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Instrument?> GetByIdAsync(
            Guid instrumentId,
            CancellationToken cancellationToken = default)
        {
            GetByIdCallCount++;
            CancellationToken = cancellationToken;
            return Task.FromResult(_instrument);
        }

        public Task UpdateAsync(
            Instrument instrument,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingTradeStore : ITradeStore
    {
        private readonly Exception? _exception;

        public RecordingTradeStore(Exception? exception = null)
        {
            _exception = exception;
        }

        public Trade? AddedTrade { get; private set; }

        public int AddCallCount { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task AddAsync(
            Trade trade,
            CancellationToken cancellationToken = default)
        {
            AddedTrade = trade;
            AddCallCount++;
            CancellationToken = cancellationToken;

            return _exception is null
                ? Task.CompletedTask
                : Task.FromException(_exception);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
