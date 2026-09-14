using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Tests.Trades;

public sealed class CloseManualTradeUseCaseTests
{
    private static readonly DateTimeOffset EntryAtUtc =
        new(2026, 9, 14, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ExitAtUtc =
        new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CurrentUtc =
        new(2026, 9, 14, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteAsyncRejectsNullCommandBeforeStoreAccess()
    {
        var store = new RecordingTradeMutationStore();
        CloseManualTradeUseCase useCase = CreateUseCase(store);

        _ = await Assert.ThrowsAsync<ArgumentNullException>(
            () => useCase.ExecuteAsync(null!));

        Assert.Equal(0, store.GetCallCount);
        Assert.Equal(0, store.SaveCallCount);
    }

    [Theory]
    [InlineData("empty-id")]
    [InlineData("non-utc")]
    [InlineData("negative-commission")]
    [InlineData("negative-fees")]
    public async Task ExecuteAsyncRejectsInvalidCommandBeforeStoreAccess(
        string invalidField)
    {
        var store = new RecordingTradeMutationStore();
        CloseManualTradeUseCase useCase = CreateUseCase(store);
        CloseManualTradeCommand valid = CreateCommand(Guid.NewGuid());
        CloseManualTradeCommand command = invalidField switch
        {
            "empty-id" => valid with { TradeId = Guid.Empty },
            "non-utc" => valid with
            {
                ExecutedAtUtc = valid.ExecutedAtUtc.ToOffset(TimeSpan.FromHours(2)),
            },
            "negative-commission" => valid with { Commission = -0.01m },
            "negative-fees" => valid with { Fees = -0.01m },
            _ => throw new InvalidOperationException(),
        };

        _ = await Assert.ThrowsAnyAsync<ArgumentException>(
            () => useCase.ExecuteAsync(command));

        Assert.Equal(0, store.GetCallCount);
        Assert.Equal(0, store.SaveCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncThrowsForMissingTradeWithoutSaving()
    {
        Guid tradeId = Guid.NewGuid();
        var store = new RecordingTradeMutationStore { TradeToReturn = null };
        CloseManualTradeUseCase useCase = CreateUseCase(store);

        KeyNotFoundException exception = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => useCase.ExecuteAsync(CreateCommand(tradeId)));

        Assert.Contains(tradeId.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, store.GetCallCount);
        Assert.Equal(0, store.SaveCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncRejectsAlreadyClosedTradeWithoutSaving()
    {
        Trade trade = CreateOpenTrade(TradeDirection.Long, 2m);
        trade.AddExecution(
            CreateExecution(
                trade.Id,
                sequence: 2,
                ExitAtUtc,
                ExecutionSide.Sell,
                2m,
                110m,
                1m,
                0.25m),
            CurrentUtc);
        var store = new RecordingTradeMutationStore { TradeToReturn = trade };

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateUseCase(store).ExecuteAsync(CreateCommand(trade.Id)));

        Assert.Equal(0, store.SaveCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncClosesLongTradeWithExactAuthoritativeState()
    {
        Trade trade = CreateOpenTrade(TradeDirection.Long, 2m);
        var store = new RecordingTradeMutationStore { TradeToReturn = trade };
        using var cancellationSource = new CancellationTokenSource();
        CloseManualTradeCommand command = CreateCommand(trade.Id);

        await CreateUseCase(store).ExecuteAsync(
            command,
            cancellationSource.Token);

        Trade savedTrade = Assert.IsType<Trade>(store.SavedTrade);
        TradeExecution closingExecution = savedTrade.Executions[^1];
        Assert.Same(trade, savedTrade);
        Assert.Equal(TradeStatus.Closed, savedTrade.Status);
        Assert.Equal(0m, savedTrade.OpenQuantity);
        Assert.Equal(ExitAtUtc, savedTrade.ClosedAtUtc);
        Assert.Equal(110m, savedTrade.AverageExitPrice);
        Assert.Equal(400m, savedTrade.GrossPnL);
        Assert.Equal(395.75m, savedTrade.NetPnL);
        Assert.Equal(CurrentUtc, savedTrade.UpdatedAtUtc);
        Assert.Equal(2, closingExecution.Sequence);
        Assert.Equal(ExecutionSide.Sell, closingExecution.Side);
        Assert.Equal(2m, closingExecution.Quantity);
        Assert.Equal(command.ExecutedAtUtc, closingExecution.ExecutedAtUtc);
        Assert.Equal(command.Price, closingExecution.Price);
        Assert.Equal(command.Commission, closingExecution.Commission);
        Assert.Equal(command.Fees, closingExecution.Fees);
        Assert.Null(closingExecution.ExternalExecutionId);
        Assert.Null(closingExecution.ExternalOrderId);
        Assert.Null(closingExecution.BrokerSymbol);
        Assert.Equal(1, store.SaveCallCount);
        Assert.Equal(cancellationSource.Token, store.GetCancellationToken);
        Assert.Equal(cancellationSource.Token, store.SaveCancellationToken);
    }

    [Fact]
    public async Task ExecuteAsyncClosesShortTradeWithBuyExecution()
    {
        Trade trade = CreateOpenTrade(TradeDirection.Short, 2m);
        var store = new RecordingTradeMutationStore { TradeToReturn = trade };
        CloseManualTradeCommand command = CreateCommand(trade.Id) with { Price = 90m };

        await CreateUseCase(store).ExecuteAsync(command);

        Assert.Equal(ExecutionSide.Buy, trade.Executions[^1].Side);
        Assert.Equal(TradeStatus.Closed, trade.Status);
        Assert.Equal(400m, trade.GrossPnL);
    }

    [Fact]
    public async Task ExecuteAsyncClosesOnlyRemainingPartialExitQuantityWithNextSequence()
    {
        Trade trade = CreateOpenTrade(TradeDirection.Long, 3m);
        trade.AddExecution(
            CreateExecution(
                trade.Id,
                sequence: 2,
                EntryAtUtc.AddMinutes(30),
                ExecutionSide.Sell,
                1m,
                105m,
                0.5m,
                0.1m),
            CurrentUtc);
        var store = new RecordingTradeMutationStore { TradeToReturn = trade };

        await CreateUseCase(store).ExecuteAsync(CreateCommand(trade.Id));

        TradeExecution closingExecution = trade.Executions[^1];
        Assert.Equal(3, closingExecution.Sequence);
        Assert.Equal(2m, closingExecution.Quantity);
        Assert.Equal(0m, trade.OpenQuantity);
        Assert.Equal(325m / 3m, trade.AverageExitPrice);
        Assert.Equal(500m, trade.GrossPnL);
    }

    [Fact]
    public async Task ExecuteAsyncLetsDomainRejectTimestampBeforeLatestExecution()
    {
        Trade trade = CreateOpenTrade(TradeDirection.Long, 2m);
        var store = new RecordingTradeMutationStore { TradeToReturn = trade };
        CloseManualTradeCommand command = CreateCommand(trade.Id) with
        {
            ExecutedAtUtc = EntryAtUtc.AddMinutes(-1),
        };

        _ = await Assert.ThrowsAsync<ArgumentException>(
            () => CreateUseCase(store).ExecuteAsync(command));

        Assert.Equal(0, store.SaveCallCount);
        Assert.Equal(TradeStatus.Open, trade.Status);
    }

    [Fact]
    public async Task ExecuteAsyncPropagatesSaveFailure()
    {
        Trade trade = CreateOpenTrade(TradeDirection.Long, 2m);
        var expected = new InvalidOperationException("save failed");
        var store = new RecordingTradeMutationStore
        {
            TradeToReturn = trade,
            SaveException = expected,
        };

        InvalidOperationException actual =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateUseCase(store).ExecuteAsync(CreateCommand(trade.Id)));

        Assert.Same(expected, actual);
        Assert.Equal(1, store.SaveCallCount);
    }

    [Fact]
    public async Task ExecuteAsyncPropagatesCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var expected = new OperationCanceledException(cancellationSource.Token);
        var store = new RecordingTradeMutationStore { GetException = expected };

        OperationCanceledException actual =
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => CreateUseCase(store).ExecuteAsync(
                    CreateCommand(Guid.NewGuid()),
                    cancellationSource.Token));

        Assert.Same(expected, actual);
        Assert.Equal(0, store.SaveCallCount);
    }

    [Fact]
    public void CommandDoesNotAcceptClosingQuantity()
    {
        Assert.DoesNotContain(
            typeof(CloseManualTradeCommand).GetProperties(),
            property => property.Name.Contains(
                "Quantity",
                StringComparison.OrdinalIgnoreCase));
    }

    private static CloseManualTradeUseCase CreateUseCase(
        RecordingTradeMutationStore store) =>
        new(store, new FixedTimeProvider(CurrentUtc));

    private static CloseManualTradeCommand CreateCommand(Guid tradeId) =>
        new(tradeId, ExitAtUtc, 110m, 2m, 0.75m);

    private static Trade CreateOpenTrade(
        TradeDirection direction,
        decimal quantity)
    {
        Guid tradeId = Guid.NewGuid();
        ExecutionSide openingSide = direction == TradeDirection.Long
            ? ExecutionSide.Buy
            : ExecutionSide.Sell;
        TradeExecution openingExecution = CreateExecution(
            tradeId,
            sequence: 1,
            EntryAtUtc,
            openingSide,
            quantity,
            100m,
            1m,
            0.5m);

        return Trade.Start(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new TradePricingSnapshot(20m, "USD"),
            openingExecution,
            EntryAtUtc.AddHours(1));
    }

    private static TradeExecution CreateExecution(
        Guid tradeId,
        int sequence,
        DateTimeOffset executedAtUtc,
        ExecutionSide side,
        decimal quantity,
        decimal price,
        decimal commission,
        decimal fees) =>
        new(
            tradeId,
            sequence,
            executedAtUtc,
            side,
            quantity,
            price,
            commission,
            fees,
            externalExecutionId: null,
            externalOrderId: null,
            brokerSymbol: null);

    private sealed class RecordingTradeMutationStore : ITradeMutationStore
    {
        public Trade? TradeToReturn { get; set; }

        public Exception? GetException { get; set; }

        public Exception? SaveException { get; set; }

        public int GetCallCount { get; private set; }

        public int SaveCallCount { get; private set; }

        public Trade? SavedTrade { get; private set; }

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

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
