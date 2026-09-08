using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Domain.Tests.Trades;

public sealed class TradeTests
{
    private static readonly Guid TradeId =
        new("37d775fa-fad7-4628-88f3-9898b862a255");

    private static readonly Guid TradingAccountId =
        new("313ce8d4-9d55-48be-9b26-cf5d30259047");

    private static readonly Guid InstrumentId =
        new("5b285ecb-d20b-431b-9aca-c382596b9f4e");

    private static readonly Guid StrategyId =
        new("0ab40da1-c4c8-4e86-bf05-9935d7c141bb");

    private static readonly Guid TradingSetupId =
        new("a6f89e70-dd67-48f3-88b9-f1d18a218fb7");

    private static readonly DateTimeOffset FirstExecutionAtUtc =
        new(2026, 1, 10, 14, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 7, 20, 0, 0, TimeSpan.Zero);

    private static readonly TradePricingSnapshot Pricing = new(20m, "USD");

    [Fact]
    public void BuyFirstExecutionCreatesLongTrade()
    {
        TradeExecution openingExecution = CreateExecution(
            side: ExecutionSide.Buy,
            quantity: 2m,
            commission: 1.25m,
            fees: 0.75m);

        Trade trade = Trade.Start(
            TradingAccountId,
            InstrumentId,
            Pricing,
            openingExecution,
            CreatedAtUtc);

        Assert.Equal(TradeId, trade.Id);
        Assert.Equal(TradingAccountId, trade.TradingAccountId);
        Assert.Equal(InstrumentId, trade.InstrumentId);
        Assert.Equal(TradeDirection.Long, trade.Direction);
        Assert.Equal(TradeStatus.Open, trade.Status);
        Assert.Equal(2m, trade.OpenQuantity);
        Assert.Equal(FirstExecutionAtUtc, trade.OpenedAtUtc);
        Assert.Null(trade.ClosedAtUtc);
        Assert.Equal(CreatedAtUtc, trade.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, trade.UpdatedAtUtc);
        Assert.Same(openingExecution, Assert.Single(trade.Executions));
        Assert.Equal(2m, trade.TotalCosts);
    }

    [Fact]
    public void SellFirstExecutionCreatesShortTrade()
    {
        Trade trade = StartTrade(side: ExecutionSide.Sell, quantity: 2m);

        Assert.Equal(TradeDirection.Short, trade.Direction);
        Assert.Equal(TradeStatus.Open, trade.Status);
        Assert.Equal(2m, trade.OpenQuantity);
    }

    [Fact]
    public void RejectsEmptyTradingAccountIdentifier()
    {
        Assert.Throws<ArgumentException>(() => Trade.Start(
            Guid.Empty,
            InstrumentId,
            Pricing,
            CreateExecution(),
            CreatedAtUtc));
    }

    [Fact]
    public void RejectsEmptyInstrumentIdentifier()
    {
        Assert.Throws<ArgumentException>(() => Trade.Start(
            TradingAccountId,
            Guid.Empty,
            Pricing,
            CreateExecution(),
            CreatedAtUtc));
    }

    [Fact]
    public void RejectsNullOpeningExecution()
    {
        Assert.Throws<ArgumentNullException>(() => Trade.Start(
            TradingAccountId,
            InstrumentId,
            Pricing,
            null!,
            CreatedAtUtc));
    }

    [Fact]
    public void OpeningExecutionSequenceMustBeOne()
    {
        Assert.Throws<ArgumentException>(() => Trade.Start(
            TradingAccountId,
            InstrumentId,
            Pricing,
            CreateExecution(sequence: 2),
            CreatedAtUtc));
    }

    [Fact]
    public void AuditTimestampIsIndependentFromExecutionTimestamp()
    {
        Trade trade = StartTrade();

        Assert.Equal(FirstExecutionAtUtc, trade.OpenedAtUtc);
        Assert.Equal(CreatedAtUtc, trade.CreatedAtUtc);
    }

    [Fact]
    public void LongOpeningSideExecutionScalesIn()
    {
        Trade trade = StartTrade(side: ExecutionSide.Buy, quantity: 2m);

        trade.AddExecution(
            CreateExecution(2, ExecutionSide.Buy, 1m, FirstExecutionAtUtc.AddMinutes(1)),
            CreatedAtUtc.AddMinutes(1));

        Assert.Equal(TradeDirection.Long, trade.Direction);
        Assert.Equal(TradeStatus.Open, trade.Status);
        Assert.Equal(3m, trade.OpenQuantity);
    }

    [Fact]
    public void LongOppositeSideExecutionScalesOut()
    {
        Trade trade = StartTrade(side: ExecutionSide.Buy, quantity: 2m);

        trade.AddExecution(
            CreateExecution(2, ExecutionSide.Sell, 1m, FirstExecutionAtUtc.AddMinutes(1)),
            CreatedAtUtc.AddMinutes(1));

        Assert.Equal(TradeDirection.Long, trade.Direction);
        Assert.Equal(TradeStatus.Open, trade.Status);
        Assert.Equal(1m, trade.OpenQuantity);
        Assert.Null(trade.ClosedAtUtc);
    }

    [Fact]
    public void LongExactFlattenClosesTrade()
    {
        Trade trade = StartTrade(side: ExecutionSide.Buy, quantity: 2m);
        DateTimeOffset closedAtUtc = FirstExecutionAtUtc.AddMinutes(10);

        trade.AddExecution(
            CreateExecution(2, ExecutionSide.Sell, 2m, closedAtUtc),
            CreatedAtUtc.AddMinutes(1));

        Assert.Equal(TradeDirection.Long, trade.Direction);
        Assert.Equal(TradeStatus.Closed, trade.Status);
        Assert.Equal(0m, trade.OpenQuantity);
        Assert.Equal(closedAtUtc, trade.ClosedAtUtc);
    }

    [Fact]
    public void LongScaleInScaleOutLifecyclePreservesExecutionOrder()
    {
        Trade trade = StartTrade(side: ExecutionSide.Buy, quantity: 2m);
        AddExecution(trade, 2, ExecutionSide.Buy, 1m);
        AddExecution(trade, 3, ExecutionSide.Sell, 1m);
        AddExecution(trade, 4, ExecutionSide.Sell, 2m);

        Assert.Equal(TradeDirection.Long, trade.Direction);
        Assert.Equal(TradeStatus.Closed, trade.Status);
        Assert.Equal(0m, trade.OpenQuantity);
        Assert.Equal(FirstExecutionAtUtc.AddMinutes(4), trade.ClosedAtUtc);
        Assert.Equal([1, 2, 3, 4], trade.Executions.Select(x => x.Sequence));
    }

    [Fact]
    public void ShortOpeningSideExecutionScalesIn()
    {
        Trade trade = StartTrade(side: ExecutionSide.Sell, quantity: 2m);

        trade.AddExecution(
            CreateExecution(2, ExecutionSide.Sell, 1m, FirstExecutionAtUtc.AddMinutes(1)),
            CreatedAtUtc.AddMinutes(1));

        Assert.Equal(TradeDirection.Short, trade.Direction);
        Assert.Equal(TradeStatus.Open, trade.Status);
        Assert.Equal(3m, trade.OpenQuantity);
    }

    [Fact]
    public void ShortOppositeSideExecutionScalesOut()
    {
        Trade trade = StartTrade(side: ExecutionSide.Sell, quantity: 2m);

        trade.AddExecution(
            CreateExecution(2, ExecutionSide.Buy, 1m, FirstExecutionAtUtc.AddMinutes(1)),
            CreatedAtUtc.AddMinutes(1));

        Assert.Equal(TradeDirection.Short, trade.Direction);
        Assert.Equal(TradeStatus.Open, trade.Status);
        Assert.Equal(1m, trade.OpenQuantity);
        Assert.Null(trade.ClosedAtUtc);
    }

    [Fact]
    public void ShortExactFlattenClosesTrade()
    {
        Trade trade = StartTrade(side: ExecutionSide.Sell, quantity: 2m);
        DateTimeOffset closedAtUtc = FirstExecutionAtUtc.AddMinutes(10);

        trade.AddExecution(
            CreateExecution(2, ExecutionSide.Buy, 2m, closedAtUtc),
            CreatedAtUtc.AddMinutes(1));

        Assert.Equal(TradeDirection.Short, trade.Direction);
        Assert.Equal(TradeStatus.Closed, trade.Status);
        Assert.Equal(0m, trade.OpenQuantity);
        Assert.Equal(closedAtUtc, trade.ClosedAtUtc);
    }

    [Fact]
    public void ShortScaleInScaleOutLifecyclePreservesDirection()
    {
        Trade trade = StartTrade(side: ExecutionSide.Sell, quantity: 2m);
        AddExecution(trade, 2, ExecutionSide.Sell, 1m);
        AddExecution(trade, 3, ExecutionSide.Buy, 1m);
        AddExecution(trade, 4, ExecutionSide.Buy, 2m);

        Assert.Equal(TradeDirection.Short, trade.Direction);
        Assert.Equal(TradeStatus.Closed, trade.Status);
        Assert.Equal(0m, trade.OpenQuantity);
        Assert.Equal(FirstExecutionAtUtc.AddMinutes(4), trade.ClosedAtUtc);
        Assert.Equal([1, 2, 3, 4], trade.Executions.Select(x => x.Sequence));
    }

    [Fact]
    public void FractionalQuantitiesCloseExactly()
    {
        Trade trade = StartTrade(quantity: 1.5m);
        AddExecution(trade, 2, ExecutionSide.Sell, 0.25m);
        AddExecution(trade, 3, ExecutionSide.Sell, 1.25m);

        Assert.Equal(TradeStatus.Closed, trade.Status);
        Assert.Equal(0m, trade.OpenQuantity);
    }

    [Fact]
    public void AcceptsNextContiguousSequence()
    {
        Trade trade = StartTrade();
        TradeExecution execution = CreateExecution(
            2,
            ExecutionSide.Buy,
            1m,
            FirstExecutionAtUtc.AddMinutes(1));

        trade.AddExecution(execution, CreatedAtUtc.AddMinutes(1));

        Assert.Same(execution, trade.Executions[1]);
    }

    [Fact]
    public void RejectsDuplicateSequenceWithoutChangingTrade()
    {
        Trade trade = StartTrade();

        Assert.Throws<ArgumentException>(() => trade.AddExecution(
            CreateExecution(1, ExecutionSide.Buy, 1m),
            CreatedAtUtc.AddMinutes(1)));

        AssertUnchanged(trade, 1, 2m, TradeStatus.Open, CreatedAtUtc);
    }

    [Fact]
    public void RejectsSequenceGapWithoutChangingTrade()
    {
        Trade trade = StartTrade();

        Assert.Throws<ArgumentException>(() => trade.AddExecution(
            CreateExecution(3, ExecutionSide.Buy, 1m),
            CreatedAtUtc.AddMinutes(1)));

        AssertUnchanged(trade, 1, 2m, TradeStatus.Open, CreatedAtUtc);
    }

    [Fact]
    public void RejectsSequenceLowerThanExpectedWithoutChangingTrade()
    {
        Trade trade = StartTrade();
        AddExecution(trade, 2, ExecutionSide.Buy, 1m);
        DateTimeOffset updatedAtUtc = trade.UpdatedAtUtc;

        Assert.Throws<ArgumentException>(() => trade.AddExecution(
            CreateExecution(1, ExecutionSide.Buy, 1m, FirstExecutionAtUtc.AddMinutes(3)),
            CreatedAtUtc.AddMinutes(3)));

        AssertUnchanged(trade, 2, 3m, TradeStatus.Open, updatedAtUtc);
    }

    [Fact]
    public void RejectsExecutionOwnedByDifferentTradeWithoutChangingTrade()
    {
        Trade trade = StartTrade();
        Guid differentTradeId = new("30198db9-fee9-4429-b57b-844f85d0e84b");

        Assert.Throws<ArgumentException>(() => trade.AddExecution(
            CreateExecution(2, ExecutionSide.Buy, 1m, tradeId: differentTradeId),
            CreatedAtUtc.AddMinutes(1)));

        AssertUnchanged(trade, 1, 2m, TradeStatus.Open, CreatedAtUtc);
    }

    [Fact]
    public void RejectsDuplicateExecutionIdentifierWithoutChangingTrade()
    {
        Guid executionId = new("1795a60e-f77c-4abe-9951-68000c4ca722");
        TradeExecution openingExecution = CreateExecution(executionId: executionId);
        Trade trade = Trade.Start(
            TradingAccountId,
            InstrumentId,
            Pricing,
            openingExecution,
            CreatedAtUtc);

        Assert.Throws<ArgumentException>(() => trade.AddExecution(
            CreateExecution(
                2,
                ExecutionSide.Buy,
                1m,
                FirstExecutionAtUtc.AddMinutes(1),
                executionId: executionId),
            CreatedAtUtc.AddMinutes(1)));

        AssertUnchanged(trade, 1, 2m, TradeStatus.Open, CreatedAtUtc);
    }

    [Fact]
    public void AcceptsLaterExecutionTimestamp()
    {
        Trade trade = StartTrade();
        DateTimeOffset laterTimestamp = FirstExecutionAtUtc.AddMinutes(1);

        trade.AddExecution(
            CreateExecution(2, ExecutionSide.Buy, 1m, laterTimestamp),
            CreatedAtUtc.AddMinutes(1));

        Assert.Equal(laterTimestamp, trade.Executions[1].ExecutedAtUtc);
    }

    [Fact]
    public void AcceptsEqualExecutionTimestamp()
    {
        Trade trade = StartTrade();

        trade.AddExecution(
            CreateExecution(2, ExecutionSide.Buy, 1m, FirstExecutionAtUtc),
            CreatedAtUtc.AddMinutes(1));

        Assert.Equal(FirstExecutionAtUtc, trade.Executions[1].ExecutedAtUtc);
    }

    [Fact]
    public void RejectsEarlierExecutionTimestampWithoutChangingTrade()
    {
        Trade trade = StartTrade();

        Assert.Throws<ArgumentException>(() => trade.AddExecution(
            CreateExecution(
                2,
                ExecutionSide.Buy,
                1m,
                FirstExecutionAtUtc.AddTicks(-1)),
            CreatedAtUtc.AddMinutes(1)));

        AssertUnchanged(trade, 1, 2m, TradeStatus.Open, CreatedAtUtc);
    }

    [Fact]
    public void RejectsLongReversalWithoutChangingTrade()
    {
        Trade trade = StartTrade(side: ExecutionSide.Buy, quantity: 2m);

        Assert.Throws<InvalidOperationException>(() => trade.AddExecution(
            CreateExecution(2, ExecutionSide.Sell, 3m, FirstExecutionAtUtc.AddMinutes(1)),
            CreatedAtUtc.AddMinutes(1)));

        Assert.Equal(TradeDirection.Long, trade.Direction);
        AssertUnchanged(trade, 1, 2m, TradeStatus.Open, CreatedAtUtc);
    }

    [Fact]
    public void RejectsShortReversalWithoutChangingTrade()
    {
        Trade trade = StartTrade(side: ExecutionSide.Sell, quantity: 2m);

        Assert.Throws<InvalidOperationException>(() => trade.AddExecution(
            CreateExecution(2, ExecutionSide.Buy, 3m, FirstExecutionAtUtc.AddMinutes(1)),
            CreatedAtUtc.AddMinutes(1)));

        Assert.Equal(TradeDirection.Short, trade.Direction);
        AssertUnchanged(trade, 1, 2m, TradeStatus.Open, CreatedAtUtc);
    }

    [Fact]
    public void RejectsExecutionAfterCloseWithoutChangingTrade()
    {
        Trade trade = StartTrade(quantity: 2m);
        AddExecution(trade, 2, ExecutionSide.Sell, 2m);
        DateTimeOffset updatedAtUtc = trade.UpdatedAtUtc;
        DateTimeOffset? closedAtUtc = trade.ClosedAtUtc;

        Assert.Throws<InvalidOperationException>(() => trade.AddExecution(
            CreateExecution(3, ExecutionSide.Buy, 1m, FirstExecutionAtUtc.AddMinutes(3)),
            CreatedAtUtc.AddMinutes(3)));

        AssertUnchanged(trade, 2, 0m, TradeStatus.Closed, updatedAtUtc);
        Assert.Equal(closedAtUtc, trade.ClosedAtUtc);
    }

    [Fact]
    public void ValidExecutionAdvancesAuditTimestamp()
    {
        Trade trade = StartTrade();
        DateTimeOffset updatedAtUtc = CreatedAtUtc.AddMinutes(10);

        trade.AddExecution(
            CreateExecution(2, ExecutionSide.Buy, 1m),
            updatedAtUtc);

        Assert.Equal(updatedAtUtc, trade.UpdatedAtUtc);
    }

    [Fact]
    public void RejectsNonUtcAuditTimestampWithoutAppendingExecution()
    {
        Trade trade = StartTrade();
        var nonUtcTimestamp = new DateTimeOffset(
            2026,
            9,
            7,
            23,
            0,
            0,
            TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(() => trade.AddExecution(
            CreateExecution(2, ExecutionSide.Buy, 1m),
            nonUtcTimestamp));

        AssertUnchanged(trade, 1, 2m, TradeStatus.Open, CreatedAtUtc);
    }

    [Fact]
    public void RejectsBackwardsAuditTimestampWithoutAppendingExecution()
    {
        Trade trade = StartTrade();

        Assert.Throws<ArgumentOutOfRangeException>(() => trade.AddExecution(
            CreateExecution(2, ExecutionSide.Buy, 1m),
            CreatedAtUtc.AddTicks(-1)));

        AssertUnchanged(trade, 1, 2m, TradeStatus.Open, CreatedAtUtc);
    }

    [Fact]
    public void TotalCostsSumsAllExecutionCosts()
    {
        Trade trade = StartTrade(commission: 1.25m, fees: 0.50m);
        trade.AddExecution(
            CreateExecution(
                2,
                ExecutionSide.Buy,
                1m,
                commission: 0.75m,
                fees: 0.25m),
            CreatedAtUtc.AddMinutes(1));

        Assert.Equal(2.75m, trade.TotalCosts);
    }

    [Fact]
    public void ExecutionCollectionCannotBeMutatedByCallers()
    {
        Trade trade = StartTrade();

        Assert.IsNotType<List<TradeExecution>>(trade.Executions);
        ICollection<TradeExecution> collection =
            Assert.IsAssignableFrom<ICollection<TradeExecution>>(trade.Executions);
        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => collection.Add(
            CreateExecution(2, ExecutionSide.Buy, 1m)));
        Assert.Single(trade.Executions);
    }

    [Fact]
    public void RehydratesValidLongOpenTrade()
    {
        Trade trade = RehydrateTrade(
            CreateExecution(1, ExecutionSide.Buy, 2m),
            CreateExecution(2, ExecutionSide.Sell, 1m));

        Assert.Equal(TradeDirection.Long, trade.Direction);
        Assert.Equal(TradeStatus.Open, trade.Status);
        Assert.Equal(1m, trade.OpenQuantity);
        Assert.Null(trade.ClosedAtUtc);
    }

    [Fact]
    public void RehydratesValidShortOpenTrade()
    {
        Trade trade = RehydrateTrade(
            CreateExecution(1, ExecutionSide.Sell, 2m),
            CreateExecution(2, ExecutionSide.Buy, 1m));

        Assert.Equal(TradeDirection.Short, trade.Direction);
        Assert.Equal(TradeStatus.Open, trade.Status);
        Assert.Equal(1m, trade.OpenQuantity);
    }

    [Fact]
    public void RehydratesValidLongClosedTrade()
    {
        Trade trade = RehydrateTrade(
            CreateExecution(1, ExecutionSide.Buy, 2m),
            CreateExecution(2, ExecutionSide.Sell, 1m),
            CreateExecution(3, ExecutionSide.Sell, 1m));

        Assert.Equal(TradeDirection.Long, trade.Direction);
        Assert.Equal(TradeStatus.Closed, trade.Status);
        Assert.Equal(0m, trade.OpenQuantity);
        Assert.Equal(FirstExecutionAtUtc.AddMinutes(2), trade.ClosedAtUtc);
    }

    [Fact]
    public void RehydratesValidShortClosedTrade()
    {
        Trade trade = RehydrateTrade(
            CreateExecution(1, ExecutionSide.Sell, 2m),
            CreateExecution(2, ExecutionSide.Buy, 1m),
            CreateExecution(3, ExecutionSide.Buy, 1m));

        Assert.Equal(TradeDirection.Short, trade.Direction);
        Assert.Equal(TradeStatus.Closed, trade.Status);
        Assert.Equal(0m, trade.OpenQuantity);
        Assert.Equal(FirstExecutionAtUtc.AddMinutes(2), trade.ClosedAtUtc);
    }

    [Fact]
    public void RehydrationPreservesIdentityAuditTimestampsAndOrdersBySequence()
    {
        DateTimeOffset updatedAtUtc = CreatedAtUtc.AddDays(1);
        TradeExecution first = CreateExecution(1, ExecutionSide.Buy, 2m);
        TradeExecution second = CreateExecution(2, ExecutionSide.Sell, 1m);

        Trade trade = Trade.Rehydrate(
            TradeId,
            TradingAccountId,
            InstrumentId,
            Pricing,
            null,
            null,
            [second, first],
            CreatedAtUtc,
            updatedAtUtc);

        Assert.Equal(TradeId, trade.Id);
        Assert.Equal(TradingAccountId, trade.TradingAccountId);
        Assert.Equal(InstrumentId, trade.InstrumentId);
        Assert.Equal(CreatedAtUtc, trade.CreatedAtUtc);
        Assert.Equal(updatedAtUtc, trade.UpdatedAtUtc);
        Assert.Equal([1, 2], trade.Executions.Select(x => x.Sequence));
    }

    [Fact]
    public void RehydrationRejectsNullExecutionCollection()
    {
        Assert.Throws<ArgumentNullException>(() => Trade.Rehydrate(
            TradeId,
            TradingAccountId,
            InstrumentId,
            Pricing,
            null,
            null,
            null!,
            CreatedAtUtc,
            CreatedAtUtc));
    }

    [Fact]
    public void RehydrationRejectsEmptyExecutionCollection()
    {
        Assert.Throws<ArgumentException>(() => RehydrateTrade([]));
    }

    [Fact]
    public void RehydrationRejectsMismatchedTradeIdentifier()
    {
        Guid differentTradeId = new("791e23f7-629e-487a-8f57-97bddba9ea4d");

        Assert.Throws<ArgumentException>(() => RehydrateTrade(
            CreateExecution(tradeId: differentTradeId)));
    }

    [Fact]
    public void RehydrationRejectsDuplicateExecutionIdentifier()
    {
        Guid executionId = new("64f1f12c-f607-46a5-9158-6d1c22b308fa");

        Assert.Throws<ArgumentException>(() => RehydrateTrade(
            CreateExecution(1, ExecutionSide.Buy, 2m, executionId: executionId),
            CreateExecution(2, ExecutionSide.Buy, 1m, executionId: executionId)));
    }

    [Fact]
    public void RehydrationRejectsDuplicateSequence()
    {
        Assert.Throws<ArgumentException>(() => RehydrateTrade(
            CreateExecution(1, ExecutionSide.Buy, 2m),
            CreateExecution(1, ExecutionSide.Sell, 1m)));
    }

    [Fact]
    public void RehydrationRejectsSequenceGap()
    {
        Assert.Throws<ArgumentException>(() => RehydrateTrade(
            CreateExecution(1, ExecutionSide.Buy, 2m),
            CreateExecution(3, ExecutionSide.Sell, 1m)));
    }

    [Fact]
    public void RehydrationRejectsSequenceNotStartingAtOne()
    {
        Assert.Throws<ArgumentException>(() => RehydrateTrade(
            CreateExecution(2, ExecutionSide.Buy, 2m)));
    }

    [Fact]
    public void RehydrationRejectsBackwardsExecutionTimestamp()
    {
        Assert.Throws<ArgumentException>(() => RehydrateTrade(
            CreateExecution(
                1,
                ExecutionSide.Buy,
                2m,
                FirstExecutionAtUtc.AddMinutes(2)),
            CreateExecution(
                2,
                ExecutionSide.Sell,
                1m,
                FirstExecutionAtUtc.AddMinutes(1))));
    }

    [Fact]
    public void RehydrationRejectsLifecycleCrossingZero()
    {
        Assert.Throws<ArgumentException>(() => RehydrateTrade(
            CreateExecution(1, ExecutionSide.Buy, 2m),
            CreateExecution(2, ExecutionSide.Sell, 3m)));
    }

    [Fact]
    public void RehydrationRejectsExecutionAfterPositionBecomesFlat()
    {
        Assert.Throws<ArgumentException>(() => RehydrateTrade(
            CreateExecution(1, ExecutionSide.Buy, 2m),
            CreateExecution(2, ExecutionSide.Sell, 2m),
            CreateExecution(3, ExecutionSide.Buy, 1m)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void RehydrationRejectsEmptyAggregateIdentifier(int identifierIndex)
    {
        Guid id = identifierIndex == 0 ? Guid.Empty : TradeId;
        Guid tradingAccountId = identifierIndex == 1 ? Guid.Empty : TradingAccountId;
        Guid instrumentId = identifierIndex == 2 ? Guid.Empty : InstrumentId;

        Assert.Throws<ArgumentException>(() => Trade.Rehydrate(
            id,
            tradingAccountId,
            instrumentId,
            Pricing,
            null,
            null,
            [CreateExecution()],
            CreatedAtUtc,
            CreatedAtUtc));
    }

    [Fact]
    public void RehydrationRejectsInvalidAuditTimestamps()
    {
        var nonUtcTimestamp = new DateTimeOffset(
            2026,
            9,
            7,
            23,
            0,
            0,
            TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(() => Trade.Rehydrate(
            TradeId,
            TradingAccountId,
            InstrumentId,
            Pricing,
            null,
            null,
            [CreateExecution()],
            nonUtcTimestamp,
            nonUtcTimestamp));

        Assert.Throws<ArgumentOutOfRangeException>(() => Trade.Rehydrate(
            TradeId,
            TradingAccountId,
            InstrumentId,
            Pricing,
            null,
            null,
            [CreateExecution()],
            CreatedAtUtc,
            CreatedAtUtc.AddTicks(-1)));
    }

    [Fact]
    public void ExposesSuppliedPricingSnapshot()
    {
        var pricing = new TradePricingSnapshot(50m, "EUR");

        Trade trade = StartTrade(pricing: pricing);

        Assert.Same(pricing, trade.Pricing);
    }

    [Fact]
    public void RejectsNullPricingWhenStartingTrade()
    {
        Assert.Throws<ArgumentNullException>(() => Trade.Start(
            TradingAccountId,
            InstrumentId,
            null!,
            CreateExecution(),
            CreatedAtUtc));
    }

    [Fact]
    public void RejectsNullPricingWhenRehydratingTrade()
    {
        Assert.Throws<ArgumentNullException>(() => Trade.Rehydrate(
            TradeId,
            TradingAccountId,
            InstrumentId,
            null!,
            null,
            null,
            [CreateExecution()],
            CreatedAtUtc,
            CreatedAtUtc));
    }

    [Fact]
    public void RehydrationPreservesPricingSnapshot()
    {
        var pricing = new TradePricingSnapshot(5m, "EUR");

        Trade trade = Trade.Rehydrate(
            TradeId,
            TradingAccountId,
            InstrumentId,
            pricing,
            null,
            null,
            [CreateExecution()],
            CreatedAtUtc,
            CreatedAtUtc);

        Assert.Same(pricing, trade.Pricing);
    }

    [Fact]
    public void CalculatesLongAverageEntryPrice()
    {
        Trade trade = StartTrade(
            side: ExecutionSide.Buy,
            quantity: 2m,
            price: 100.125m);

        Assert.Equal(100.125m, trade.AverageEntryPrice);
    }

    [Fact]
    public void CalculatesShortAverageEntryPrice()
    {
        Trade trade = StartTrade(
            side: ExecutionSide.Sell,
            quantity: 2m,
            price: 100.125m);

        Assert.Equal(100.125m, trade.AverageEntryPrice);
    }

    [Fact]
    public void CalculatesWeightedScaleInAverageEntryPrice()
    {
        Trade trade = StartTrade(quantity: 1m, price: 100m);
        AddExecution(trade, 2, ExecutionSide.Buy, 3m, price: 110m);

        Assert.Equal(107.5m, trade.AverageEntryPrice);
    }

    [Fact]
    public void AverageExitPriceIsNullBeforeScaleOut()
    {
        Trade trade = StartTrade();

        Assert.Null(trade.AverageExitPrice);
    }

    [Fact]
    public void CalculatesPartialScaleOutAverageExitPrice()
    {
        Trade trade = StartTrade(quantity: 2m, price: 100m);
        AddExecution(trade, 2, ExecutionSide.Sell, 1m, price: 110.25m);

        Assert.Equal(110.25m, trade.AverageExitPrice);
        Assert.Equal(TradeStatus.Open, trade.Status);
    }

    [Fact]
    public void OpenTradeDoesNotExposeFinalPnlAfterPartialScaleOut()
    {
        Trade trade = StartTrade(quantity: 2m, price: 100m);
        AddExecution(trade, 2, ExecutionSide.Sell, 1m, price: 110m);

        Assert.Null(trade.GrossPnL);
        Assert.Null(trade.NetPnL);
    }

    [Fact]
    public void CalculatesProfitableLongFinalPnlAndCosts()
    {
        Trade trade = StartTrade(
            quantity: 1m,
            commission: 1m,
            fees: 1m,
            price: 20_000m);
        AddExecution(
            trade,
            2,
            ExecutionSide.Sell,
            1m,
            price: 20_010m,
            commission: 1m,
            fees: 1m);

        Assert.Equal(200m, trade.GrossPnL);
        Assert.Equal(4m, trade.TotalCosts);
        Assert.Equal(196m, trade.NetPnL);
    }

    [Fact]
    public void CalculatesLosingLongFinalPnl()
    {
        Trade trade = StartTrade(quantity: 1m, price: 20_000m);
        AddExecution(trade, 2, ExecutionSide.Sell, 1m, price: 19_990m);

        Assert.Equal(-200m, trade.GrossPnL);
        Assert.Equal(-200m, trade.NetPnL);
    }

    [Fact]
    public void CalculatesProfitableShortFinalPnl()
    {
        Trade trade = StartTrade(
            side: ExecutionSide.Sell,
            quantity: 1m,
            price: 20_000m);
        AddExecution(trade, 2, ExecutionSide.Buy, 1m, price: 19_990m);

        Assert.Equal(200m, trade.GrossPnL);
        Assert.Equal(200m, trade.NetPnL);
    }

    [Fact]
    public void CalculatesLosingShortFinalPnl()
    {
        Trade trade = StartTrade(
            side: ExecutionSide.Sell,
            quantity: 1m,
            price: 20_000m);
        AddExecution(trade, 2, ExecutionSide.Buy, 1m, price: 20_010m);

        Assert.Equal(-200m, trade.GrossPnL);
        Assert.Equal(-200m, trade.NetPnL);
    }

    [Fact]
    public void CalculatesLongScaleInScaleOutEconomics()
    {
        Trade trade = StartTrade(quantity: 1m, price: 100m);
        AddExecution(trade, 2, ExecutionSide.Buy, 3m, price: 110m);
        AddExecution(trade, 3, ExecutionSide.Sell, 2m, price: 120m);
        AddExecution(trade, 4, ExecutionSide.Sell, 2m, price: 130m);

        Assert.Equal(107.5m, trade.AverageEntryPrice);
        Assert.Equal(125m, trade.AverageExitPrice);
        Assert.Equal(1_400m, trade.GrossPnL);
        Assert.Equal(1_400m, trade.NetPnL);
    }

    [Fact]
    public void CalculatesShortScaleInScaleOutEconomics()
    {
        Trade trade = StartTrade(
            side: ExecutionSide.Sell,
            quantity: 1m,
            price: 130m);
        AddExecution(trade, 2, ExecutionSide.Sell, 3m, price: 120m);
        AddExecution(trade, 3, ExecutionSide.Buy, 2m, price: 110m);
        AddExecution(trade, 4, ExecutionSide.Buy, 2m, price: 100m);

        Assert.Equal(122.5m, trade.AverageEntryPrice);
        Assert.Equal(105m, trade.AverageExitPrice);
        Assert.Equal(1_400m, trade.GrossPnL);
        Assert.Equal(1_400m, trade.NetPnL);
    }

    [Fact]
    public void CalculatesInterleavedLifecyclePnlFromCompleteCashFlows()
    {
        Trade trade = StartTrade(quantity: 2m, price: 100m);
        AddExecution(trade, 2, ExecutionSide.Sell, 1m, price: 110m);
        AddExecution(trade, 3, ExecutionSide.Buy, 1m, price: 90m);
        AddExecution(trade, 4, ExecutionSide.Sell, 2m, price: 105m);

        Assert.Equal(600m, trade.GrossPnL);
    }

    [Fact]
    public void CalculatesFractionalQuantityPnlWithoutRoundingInputs()
    {
        var pricing = new TradePricingSnapshot(2m, "USD");
        Trade trade = StartTrade(
            quantity: 1.5m,
            price: 100m,
            pricing: pricing);
        AddExecution(trade, 2, ExecutionSide.Sell, 0.5m, price: 110m);
        AddExecution(trade, 3, ExecutionSide.Sell, 1m, price: 120m);

        Assert.Equal(50m, trade.GrossPnL);
        Assert.Equal(50m, trade.NetPnL);
    }

    [Fact]
    public void CalculatesProfitableLongPnlWithNegativePrices()
    {
        var pricing = new TradePricingSnapshot(1m, "USD");
        Trade trade = StartTrade(quantity: 1m, price: -40m, pricing: pricing);
        AddExecution(trade, 2, ExecutionSide.Sell, 1m, price: -30m);

        Assert.Equal(10m, trade.GrossPnL);
    }

    [Fact]
    public void CalculatesProfitableShortPnlWithNegativePrices()
    {
        var pricing = new TradePricingSnapshot(1m, "USD");
        Trade trade = StartTrade(
            side: ExecutionSide.Sell,
            quantity: 1m,
            price: -30m,
            pricing: pricing);
        AddExecution(trade, 2, ExecutionSide.Buy, 1m, price: -40m);

        Assert.Equal(10m, trade.GrossPnL);
    }

    [Fact]
    public void PriceBreakevenTradeCanHaveNegativeNetPnl()
    {
        Trade trade = StartTrade(
            quantity: 1m,
            commission: 1m,
            fees: 1m,
            price: 100m);
        AddExecution(
            trade,
            2,
            ExecutionSide.Sell,
            1m,
            price: 100m,
            commission: 2m,
            fees: 1m);

        Assert.Equal(0m, trade.GrossPnL);
        Assert.Equal(5m, trade.TotalCosts);
        Assert.Equal(-5m, trade.NetPnL);
    }

    [Fact]
    public void PnlRemainsDerivedAsExecutionsAreAdded()
    {
        Trade trade = StartTrade(quantity: 1m, price: 100m);

        Assert.Null(trade.GrossPnL);
        Assert.Null(trade.NetPnL);

        AddExecution(trade, 2, ExecutionSide.Sell, 1m, price: 110m);

        Assert.Equal(200m, trade.GrossPnL);
        Assert.Equal(200m, trade.NetPnL);
    }

    [Fact]
    public void HistoricalPnlUsesCapturedPricingSnapshotWithoutInstrumentLookup()
    {
        var historicalPricing = new TradePricingSnapshot(20m, "USD");
        Trade trade = StartTrade(
            quantity: 1m,
            price: 20_000m,
            pricing: historicalPricing);
        AddExecution(trade, 2, ExecutionSide.Sell, 1m, price: 20_010m);

        Assert.Same(historicalPricing, trade.Pricing);
        Assert.Equal(200m, trade.GrossPnL);
        Assert.Equal("USD", trade.Pricing.Currency);
    }

    [Fact]
    public void RehydratedTradeDerivesAllEconomicsFromExecutionsAndPricing()
    {
        var pricing = new TradePricingSnapshot(5m, "EUR");
        TradeExecution opening = CreateExecution(
            1,
            ExecutionSide.Buy,
            1m,
            commission: 0.75m,
            fees: 0.25m,
            price: 100m);
        TradeExecution closing = CreateExecution(
            2,
            ExecutionSide.Sell,
            1m,
            commission: 1.50m,
            fees: 0.50m,
            price: 110m);

        Trade trade = Trade.Rehydrate(
            TradeId,
            TradingAccountId,
            InstrumentId,
            pricing,
            null,
            null,
            [closing, opening],
            CreatedAtUtc,
            CreatedAtUtc.AddMinutes(1));

        Assert.Same(pricing, trade.Pricing);
        Assert.Equal(100m, trade.AverageEntryPrice);
        Assert.Equal(110m, trade.AverageExitPrice);
        Assert.Equal(50m, trade.GrossPnL);
        Assert.Equal(3m, trade.TotalCosts);
        Assert.Equal(47m, trade.NetPnL);
    }

    [Fact]
    public void NewTradeIsUnclassified()
    {
        Trade trade = StartTrade();

        Assert.Null(trade.StrategyId);
        Assert.Null(trade.TradingSetupId);
    }

    [Fact]
    public void AssignsStrategyOnlyAndAdvancesTimestamp()
    {
        Trade trade = StartTrade();
        DateTimeOffset classifiedAtUtc = CreatedAtUtc.AddMinutes(1);

        trade.SetClassification(StrategyId, null, classifiedAtUtc);

        Assert.Equal(StrategyId, trade.StrategyId);
        Assert.Null(trade.TradingSetupId);
        Assert.Equal(classifiedAtUtc, trade.UpdatedAtUtc);
    }

    [Fact]
    public void AssignsTradingSetupOnlyAndAdvancesTimestamp()
    {
        Trade trade = StartTrade();
        DateTimeOffset classifiedAtUtc = CreatedAtUtc.AddMinutes(1);

        trade.SetClassification(null, TradingSetupId, classifiedAtUtc);

        Assert.Null(trade.StrategyId);
        Assert.Equal(TradingSetupId, trade.TradingSetupId);
        Assert.Equal(classifiedAtUtc, trade.UpdatedAtUtc);
    }

    [Fact]
    public void AssignsBothIndependentClassificationDimensions()
    {
        Trade trade = StartTrade();

        trade.SetClassification(
            StrategyId,
            TradingSetupId,
            CreatedAtUtc.AddMinutes(1));

        Assert.Equal(StrategyId, trade.StrategyId);
        Assert.Equal(TradingSetupId, trade.TradingSetupId);
    }

    [Fact]
    public void RejectsEmptyStrategyIdentifierWithoutChangingTrade()
    {
        Trade trade = StartTrade();
        trade.SetClassification(
            StrategyId,
            TradingSetupId,
            CreatedAtUtc.AddMinutes(1));
        DateTimeOffset updatedAtUtc = trade.UpdatedAtUtc;

        Assert.Throws<ArgumentException>(() => trade.SetClassification(
            Guid.Empty,
            null,
            CreatedAtUtc.AddMinutes(2)));

        Assert.Equal(StrategyId, trade.StrategyId);
        Assert.Equal(TradingSetupId, trade.TradingSetupId);
        Assert.Equal(updatedAtUtc, trade.UpdatedAtUtc);
    }

    [Fact]
    public void RejectsEmptyTradingSetupIdentifierWithoutPartialReclassification()
    {
        Trade trade = StartTrade();
        trade.SetClassification(
            StrategyId,
            TradingSetupId,
            CreatedAtUtc.AddMinutes(1));
        Guid newStrategyId = new("2bc51114-cb51-4b92-871a-a9afe24f6de8");
        DateTimeOffset updatedAtUtc = trade.UpdatedAtUtc;

        Assert.Throws<ArgumentException>(() => trade.SetClassification(
            newStrategyId,
            Guid.Empty,
            CreatedAtUtc.AddMinutes(2)));

        Assert.Equal(StrategyId, trade.StrategyId);
        Assert.Equal(TradingSetupId, trade.TradingSetupId);
        Assert.Equal(updatedAtUtc, trade.UpdatedAtUtc);
    }

    [Fact]
    public void IdenticalClassificationIsNoOp()
    {
        Trade trade = StartTrade();
        DateTimeOffset firstClassifiedAtUtc = CreatedAtUtc.AddMinutes(1);
        trade.SetClassification(StrategyId, TradingSetupId, firstClassifiedAtUtc);

        trade.SetClassification(
            StrategyId,
            TradingSetupId,
            CreatedAtUtc.AddMinutes(2));

        Assert.Equal(StrategyId, trade.StrategyId);
        Assert.Equal(TradingSetupId, trade.TradingSetupId);
        Assert.Equal(firstClassifiedAtUtc, trade.UpdatedAtUtc);
    }

    [Fact]
    public void ClearsStrategyOnly()
    {
        Trade trade = StartClassifiedTrade();
        DateTimeOffset clearedAtUtc = CreatedAtUtc.AddMinutes(2);

        trade.SetClassification(null, TradingSetupId, clearedAtUtc);

        Assert.Null(trade.StrategyId);
        Assert.Equal(TradingSetupId, trade.TradingSetupId);
        Assert.Equal(clearedAtUtc, trade.UpdatedAtUtc);
    }

    [Fact]
    public void ClearsTradingSetupOnly()
    {
        Trade trade = StartClassifiedTrade();
        DateTimeOffset clearedAtUtc = CreatedAtUtc.AddMinutes(2);

        trade.SetClassification(StrategyId, null, clearedAtUtc);

        Assert.Equal(StrategyId, trade.StrategyId);
        Assert.Null(trade.TradingSetupId);
        Assert.Equal(clearedAtUtc, trade.UpdatedAtUtc);
    }

    [Fact]
    public void ClearsAllClassificationAndAdvancesTimestamp()
    {
        Trade trade = StartClassifiedTrade();
        DateTimeOffset clearedAtUtc = CreatedAtUtc.AddMinutes(2);

        trade.SetClassification(null, null, clearedAtUtc);

        Assert.Null(trade.StrategyId);
        Assert.Null(trade.TradingSetupId);
        Assert.Equal(clearedAtUtc, trade.UpdatedAtUtc);
    }

    [Fact]
    public void ReclassifiesStrategyIndependently()
    {
        Trade trade = StartClassifiedTrade();
        Guid replacementStrategyId = new("6d5316d9-6abc-4571-b791-e77c88ea3295");

        trade.SetClassification(
            replacementStrategyId,
            TradingSetupId,
            CreatedAtUtc.AddMinutes(2));

        Assert.Equal(replacementStrategyId, trade.StrategyId);
        Assert.Equal(TradingSetupId, trade.TradingSetupId);
    }

    [Fact]
    public void ReclassifiesTradingSetupIndependently()
    {
        Trade trade = StartClassifiedTrade();
        Guid replacementSetupId = new("74b131fb-fd22-426d-99da-b87629952c0c");

        trade.SetClassification(
            StrategyId,
            replacementSetupId,
            CreatedAtUtc.AddMinutes(2));

        Assert.Equal(StrategyId, trade.StrategyId);
        Assert.Equal(replacementSetupId, trade.TradingSetupId);
    }

    [Fact]
    public void ReclassifiesBothDimensionsAtomically()
    {
        Trade trade = StartClassifiedTrade();
        Guid replacementStrategyId = new("c89dfa4e-61f0-49b2-986f-3172830dc044");
        Guid replacementSetupId = new("65b84936-0f69-4adf-808c-df2731c8aaee");

        trade.SetClassification(
            replacementStrategyId,
            replacementSetupId,
            CreatedAtUtc.AddMinutes(2));

        Assert.Equal(replacementStrategyId, trade.StrategyId);
        Assert.Equal(replacementSetupId, trade.TradingSetupId);
    }

    [Fact]
    public void ClassificationWhileOpenDoesNotChangeExecutionDerivedState()
    {
        Trade trade = StartTrade(quantity: 2m, price: 100m);
        TradeDirection direction = trade.Direction;
        decimal openQuantity = trade.OpenQuantity;
        DateTimeOffset openedAtUtc = trade.OpenedAtUtc;
        decimal averageEntryPrice = trade.AverageEntryPrice;
        TradeExecution openingExecution = trade.Executions[0];

        trade.SetClassification(
            StrategyId,
            TradingSetupId,
            CreatedAtUtc.AddMinutes(1));

        Assert.Equal(TradeStatus.Open, trade.Status);
        Assert.Equal(direction, trade.Direction);
        Assert.Equal(openQuantity, trade.OpenQuantity);
        Assert.Equal(openedAtUtc, trade.OpenedAtUtc);
        Assert.Equal(averageEntryPrice, trade.AverageEntryPrice);
        Assert.Same(openingExecution, Assert.Single(trade.Executions));
        Assert.Null(trade.GrossPnL);
        Assert.Null(trade.NetPnL);
    }

    [Fact]
    public void ClassificationAfterCloseDoesNotChangeLifecycleOrEconomics()
    {
        Trade trade = StartTrade(
            quantity: 1m,
            commission: 1m,
            price: 100m);
        AddExecution(
            trade,
            2,
            ExecutionSide.Sell,
            1m,
            price: 110m,
            fees: 2m);
        DateTimeOffset? closedAtUtc = trade.ClosedAtUtc;
        decimal? grossPnL = trade.GrossPnL;
        decimal? netPnL = trade.NetPnL;
        decimal totalCosts = trade.TotalCosts;
        TradeExecution[] executions = trade.Executions.ToArray();

        trade.SetClassification(
            StrategyId,
            TradingSetupId,
            CreatedAtUtc.AddMinutes(10));

        Assert.Equal(TradeStatus.Closed, trade.Status);
        Assert.Equal(closedAtUtc, trade.ClosedAtUtc);
        Assert.Equal(grossPnL, trade.GrossPnL);
        Assert.Equal(netPnL, trade.NetPnL);
        Assert.Equal(totalCosts, trade.TotalCosts);
        Assert.Equal(executions, trade.Executions);
    }

    [Fact]
    public void RejectsNonUtcClassificationTimestampWithoutChangingTrade()
    {
        Trade trade = StartClassifiedTrade();
        var nonUtcTimestamp = new DateTimeOffset(
            2026,
            9,
            8,
            12,
            0,
            0,
            TimeSpan.FromHours(2));
        DateTimeOffset updatedAtUtc = trade.UpdatedAtUtc;

        Assert.Throws<ArgumentException>(() => trade.SetClassification(
            null,
            null,
            nonUtcTimestamp));

        Assert.Equal(StrategyId, trade.StrategyId);
        Assert.Equal(TradingSetupId, trade.TradingSetupId);
        Assert.Equal(updatedAtUtc, trade.UpdatedAtUtc);
    }

    [Fact]
    public void RejectsBackwardsClassificationTimestampWithoutChangingTrade()
    {
        Trade trade = StartClassifiedTrade();
        DateTimeOffset updatedAtUtc = trade.UpdatedAtUtc;
        Guid replacementStrategyId = new("79994487-d763-496d-ae25-0d9ad8c9f8bf");
        Guid replacementSetupId = new("bf365078-7afd-46ef-8942-50dac73001e2");

        Assert.Throws<ArgumentOutOfRangeException>(() => trade.SetClassification(
            replacementStrategyId,
            replacementSetupId,
            CreatedAtUtc));

        Assert.Equal(StrategyId, trade.StrategyId);
        Assert.Equal(TradingSetupId, trade.TradingSetupId);
        Assert.Equal(updatedAtUtc, trade.UpdatedAtUtc);
    }

    [Fact]
    public void ClassificationSurvivesAdditionalExecution()
    {
        Trade trade = StartClassifiedTrade();

        AddExecution(trade, 2, ExecutionSide.Buy, 1m);

        Assert.Equal(StrategyId, trade.StrategyId);
        Assert.Equal(TradingSetupId, trade.TradingSetupId);
        Assert.Equal(3m, trade.OpenQuantity);
    }

    [Fact]
    public void ClosingClassifiedTradePreservesClassification()
    {
        Trade trade = StartClassifiedTrade();

        AddExecution(trade, 2, ExecutionSide.Sell, 2m);

        Assert.Equal(TradeStatus.Closed, trade.Status);
        Assert.Equal(StrategyId, trade.StrategyId);
        Assert.Equal(TradingSetupId, trade.TradingSetupId);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void RehydratesIndependentClassificationDimensions(
        bool includeStrategy,
        bool includeTradingSetup)
    {
        Guid? strategyId = includeStrategy ? StrategyId : null;
        Guid? tradingSetupId = includeTradingSetup ? TradingSetupId : null;

        Trade trade = Trade.Rehydrate(
            TradeId,
            TradingAccountId,
            InstrumentId,
            Pricing,
            strategyId,
            tradingSetupId,
            [CreateExecution()],
            CreatedAtUtc,
            CreatedAtUtc.AddMinutes(1));

        Assert.Equal(strategyId, trade.StrategyId);
        Assert.Equal(tradingSetupId, trade.TradingSetupId);
    }

    [Fact]
    public void RehydrationRejectsEmptyStrategyIdentifier()
    {
        Assert.Throws<ArgumentException>(() => Trade.Rehydrate(
            TradeId,
            TradingAccountId,
            InstrumentId,
            Pricing,
            Guid.Empty,
            null,
            [CreateExecution()],
            CreatedAtUtc,
            CreatedAtUtc));
    }

    [Fact]
    public void RehydrationRejectsEmptyTradingSetupIdentifier()
    {
        Assert.Throws<ArgumentException>(() => Trade.Rehydrate(
            TradeId,
            TradingAccountId,
            InstrumentId,
            Pricing,
            null,
            Guid.Empty,
            [CreateExecution()],
            CreatedAtUtc,
            CreatedAtUtc));
    }

    [Fact]
    public void RehydratedClassifiedTradeRetainsLifecycleAndEconomics()
    {
        TradeExecution opening = CreateExecution(
            1,
            ExecutionSide.Buy,
            1m,
            commission: 1m,
            price: 100m);
        TradeExecution closing = CreateExecution(
            2,
            ExecutionSide.Sell,
            1m,
            fees: 2m,
            price: 110m);

        Trade trade = Trade.Rehydrate(
            TradeId,
            TradingAccountId,
            InstrumentId,
            Pricing,
            StrategyId,
            TradingSetupId,
            [closing, opening],
            CreatedAtUtc,
            CreatedAtUtc.AddMinutes(1));

        Assert.Equal(StrategyId, trade.StrategyId);
        Assert.Equal(TradingSetupId, trade.TradingSetupId);
        Assert.Equal(TradeDirection.Long, trade.Direction);
        Assert.Equal(TradeStatus.Closed, trade.Status);
        Assert.Equal(0m, trade.OpenQuantity);
        Assert.Equal(200m, trade.GrossPnL);
        Assert.Equal(3m, trade.TotalCosts);
        Assert.Equal(197m, trade.NetPnL);
    }

    private static Trade StartTrade(
        ExecutionSide side = ExecutionSide.Buy,
        decimal quantity = 2m,
        decimal commission = 0m,
        decimal fees = 0m,
        decimal price = 20_000m,
        TradePricingSnapshot? pricing = null)
    {
        return Trade.Start(
            TradingAccountId,
            InstrumentId,
            pricing ?? Pricing,
            CreateExecution(
                side: side,
                quantity: quantity,
                commission: commission,
                fees: fees,
                price: price),
            CreatedAtUtc);
    }

    private static Trade StartClassifiedTrade()
    {
        Trade trade = StartTrade();
        trade.SetClassification(
            StrategyId,
            TradingSetupId,
            CreatedAtUtc.AddMinutes(1));
        return trade;
    }

    private static void AddExecution(
        Trade trade,
        int sequence,
        ExecutionSide side,
        decimal quantity,
        decimal price = 20_000m,
        decimal commission = 0m,
        decimal fees = 0m)
    {
        trade.AddExecution(
            CreateExecution(
                sequence,
                side,
                quantity,
                FirstExecutionAtUtc.AddMinutes(sequence),
                commission: commission,
                fees: fees,
                price: price),
            CreatedAtUtc.AddMinutes(sequence));
    }

    private static TradeExecution CreateExecution(
        int sequence = 1,
        ExecutionSide side = ExecutionSide.Buy,
        decimal quantity = 2m,
        DateTimeOffset? executedAtUtc = null,
        Guid? tradeId = null,
        Guid? executionId = null,
        decimal commission = 0m,
        decimal fees = 0m,
        decimal price = 20_000m)
    {
        Guid resolvedTradeId = tradeId ?? TradeId;
        DateTimeOffset resolvedExecutedAtUtc =
            executedAtUtc ?? FirstExecutionAtUtc.AddMinutes(sequence - 1);

        if (executionId.HasValue)
        {
            return TradeExecution.Rehydrate(
                executionId.Value,
                resolvedTradeId,
                sequence,
                resolvedExecutedAtUtc,
                side,
                quantity,
                price,
                commission,
                fees,
                null,
                null,
                null);
        }

        return new TradeExecution(
            resolvedTradeId,
            sequence,
            resolvedExecutedAtUtc,
            side,
            quantity,
            price,
            commission,
            fees,
            null,
            null,
            null);
    }

    private static Trade RehydrateTrade(params TradeExecution[] executions)
    {
        return RehydrateTrade((IEnumerable<TradeExecution>)executions);
    }

    private static Trade RehydrateTrade(IEnumerable<TradeExecution> executions)
    {
        return Trade.Rehydrate(
            TradeId,
            TradingAccountId,
            InstrumentId,
            Pricing,
            null,
            null,
            executions,
            CreatedAtUtc,
            CreatedAtUtc.AddMinutes(1));
    }

    private static void AssertUnchanged(
        Trade trade,
        int executionCount,
        decimal openQuantity,
        TradeStatus status,
        DateTimeOffset updatedAtUtc)
    {
        Assert.Equal(executionCount, trade.Executions.Count);
        Assert.Equal(openQuantity, trade.OpenQuantity);
        Assert.Equal(status, trade.Status);
        Assert.Equal(updatedAtUtc, trade.UpdatedAtUtc);
    }
}
