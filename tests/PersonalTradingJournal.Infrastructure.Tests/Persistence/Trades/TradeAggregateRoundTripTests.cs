using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Trades;

public sealed class TradeAggregateRoundTripTests
{
    private static readonly Guid TradingAccountId =
        Guid.Parse("77fb4791-1dde-4316-98fe-cd3311fef183");

    private static readonly Guid InstrumentId =
        Guid.Parse("6f365ef7-7e9c-4479-83d3-3030bb3f10a2");

    private static readonly Guid StrategyId =
        Guid.Parse("81172b03-4625-4c93-9c16-6f96ec81dd3f");

    private static readonly Guid TradingSetupId =
        Guid.Parse("e50f36a0-7214-4677-84b0-404bb15ca1a9");

    private static readonly DateTimeOffset FirstExecutionAtUtc =
        new(2026, 9, 15, 13, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 15, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ClosedScaledLongTradeRoundTripsAndUsesHistoricalPricing()
    {
        Guid tradeId = Guid.Parse("a928b03c-5092-42aa-b618-0ed940432e50");
        Guid[] executionIds =
        [
            Guid.Parse("d2d54831-e7c5-4444-a8f6-ad0c13049435"),
            Guid.Parse("00b73e4d-0974-4935-85ec-f08c638f7772"),
            Guid.Parse("1befa6cb-a85a-4538-a69c-4551f5011e96"),
            Guid.Parse("28e01c08-f5f5-4772-9873-b340640c25d5"),
        ];
        Trade original = StartTrade(
            tradeId,
            executionIds[0],
            ExecutionSide.Buy,
            2m,
            20_000m,
            pointValue: 20m,
            commission: 1m,
            fees: 0.25m);
        AddExecution(
            original,
            executionIds[1],
            2,
            ExecutionSide.Buy,
            1m,
            20_010m,
            commission: 1m,
            fees: 0.25m);
        AddExecution(
            original,
            executionIds[2],
            3,
            ExecutionSide.Sell,
            1m,
            20_020m,
            commission: 1m,
            fees: 0.25m);
        AddExecution(
            original,
            executionIds[3],
            4,
            ExecutionSide.Sell,
            2m,
            20_030m,
            commission: 1m,
            fees: 0.25m);

        original.SetClassification(
            Guid.Parse("5573241e-a351-41d3-b36c-d04e8af36ea3"),
            null,
            CreatedAtUtc.AddMinutes(4));
        original.SetClassification(
            StrategyId,
            TradingSetupId,
            CreatedAtUtc.AddHours(1));

        (Trade rehydrated, decimal currentInstrumentPointValue) =
            RoundTrip(original, updatedInstrumentTickValue: 2.5m);

        Assert.Equal(tradeId, rehydrated.Id);
        Assert.Equal(TradingAccountId, rehydrated.TradingAccountId);
        Assert.Equal(InstrumentId, rehydrated.InstrumentId);
        Assert.Equal(20m, rehydrated.Pricing.PointValue);
        Assert.Equal("USD", rehydrated.Pricing.Currency);
        Assert.Equal(10m, currentInstrumentPointValue);
        Assert.Equal(StrategyId, rehydrated.StrategyId);
        Assert.Equal(TradingSetupId, rehydrated.TradingSetupId);
        Assert.Equal(CreatedAtUtc, rehydrated.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc.AddHours(1), rehydrated.UpdatedAtUtc);

        Assert.Collection(
            rehydrated.Executions,
            execution => AssertExecution(
                execution,
                executionIds[0],
                1,
                ExecutionSide.Buy,
                2m,
                20_000m,
                "EXEC-1",
                "ORDER-1"),
            execution => AssertExecution(
                execution,
                executionIds[1],
                2,
                ExecutionSide.Buy,
                1m,
                20_010m,
                "EXEC-2",
                "ORDER-2"),
            execution => AssertExecution(
                execution,
                executionIds[2],
                3,
                ExecutionSide.Sell,
                1m,
                20_020m,
                "EXEC-3",
                "ORDER-3"),
            execution => AssertExecution(
                execution,
                executionIds[3],
                4,
                ExecutionSide.Sell,
                2m,
                20_030m,
                "EXEC-4",
                "ORDER-4"));

        Assert.Equal(TradeDirection.Long, rehydrated.Direction);
        Assert.Equal(TradeStatus.Closed, rehydrated.Status);
        Assert.Equal(0m, rehydrated.OpenQuantity);
        Assert.Equal(FirstExecutionAtUtc, rehydrated.OpenedAtUtc);
        Assert.Equal(FirstExecutionAtUtc.AddMinutes(3), rehydrated.ClosedAtUtc);
        Assert.Equal(60_010m / 3m, rehydrated.AverageEntryPrice);
        Assert.Equal(60_080m / 3m, rehydrated.AverageExitPrice);
        Assert.Equal(5m, rehydrated.TotalCosts);
        Assert.Equal(1_400m, rehydrated.GrossPnL);
        Assert.Equal(1_395m, rehydrated.NetPnL);
        Assert.True(rehydrated.ClosedAtUtc.HasValue);
        Assert.True(rehydrated.UpdatedAtUtc > rehydrated.ClosedAtUtc.Value);
    }

    [Fact]
    public void ClosedShortTradeRoundTripsWithCorrectEconomics()
    {
        Guid tradeId = Guid.Parse("5af17019-628d-4518-89c9-7f0f652b01e7");
        Trade original = StartTrade(
            tradeId,
            Guid.Parse("c3fb236a-97a8-44b0-924c-75b7d2f36f72"),
            ExecutionSide.Sell,
            2m,
            100m,
            pointValue: 10m,
            commission: 0.5m,
            fees: 0.1m);
        AddExecution(
            original,
            Guid.Parse("e840737b-e73f-45a7-a6c0-252c3e0162f3"),
            2,
            ExecutionSide.Buy,
            1m,
            90m,
            commission: 0.5m,
            fees: 0.1m);
        AddExecution(
            original,
            Guid.Parse("a0166d68-d980-4174-ad97-a8cf76028688"),
            3,
            ExecutionSide.Buy,
            1m,
            80m,
            commission: 0.5m,
            fees: 0.1m);

        (Trade rehydrated, _) = RoundTrip(original);

        Assert.Equal(tradeId, rehydrated.Id);
        Assert.Null(rehydrated.StrategyId);
        Assert.Null(rehydrated.TradingSetupId);
        Assert.Equal(3, rehydrated.Executions.Count);
        Assert.Equal(TradeDirection.Short, rehydrated.Direction);
        Assert.Equal(TradeStatus.Closed, rehydrated.Status);
        Assert.Equal(0m, rehydrated.OpenQuantity);
        Assert.Equal(FirstExecutionAtUtc, rehydrated.OpenedAtUtc);
        Assert.Equal(FirstExecutionAtUtc.AddMinutes(2), rehydrated.ClosedAtUtc);
        Assert.Equal(100m, rehydrated.AverageEntryPrice);
        Assert.Equal(85m, rehydrated.AverageExitPrice);
        Assert.Equal(1.8m, rehydrated.TotalCosts);
        Assert.Equal(300m, rehydrated.GrossPnL);
        Assert.Equal(298.2m, rehydrated.NetPnL);
    }

    [Fact]
    public void PartiallyExitedOpenTradeRoundTripsWithNullFinalPnl()
    {
        Guid tradeId = Guid.Parse("cd89cf5f-cc97-451d-9c83-ac474306ac96");
        Trade original = StartTrade(
            tradeId,
            Guid.Parse("a662a349-0d01-447b-9d52-4d9171ba08e1"),
            ExecutionSide.Buy,
            2m,
            100m,
            pointValue: 20m,
            commission: 0.5m,
            fees: 0.1m);
        AddExecution(
            original,
            Guid.Parse("79861745-750a-4517-93c8-e86d8a5d0408"),
            2,
            ExecutionSide.Sell,
            1m,
            110m,
            commission: 0.5m,
            fees: 0.1m);

        (Trade rehydrated, _) = RoundTrip(original);

        Assert.Equal(tradeId, rehydrated.Id);
        Assert.Null(rehydrated.StrategyId);
        Assert.Null(rehydrated.TradingSetupId);
        Assert.Equal(2, rehydrated.Executions.Count);
        Assert.Equal(TradeDirection.Long, rehydrated.Direction);
        Assert.Equal(TradeStatus.Open, rehydrated.Status);
        Assert.Equal(1m, rehydrated.OpenQuantity);
        Assert.Equal(FirstExecutionAtUtc, rehydrated.OpenedAtUtc);
        Assert.Null(rehydrated.ClosedAtUtc);
        Assert.Equal(100m, rehydrated.AverageEntryPrice);
        Assert.Equal(110m, rehydrated.AverageExitPrice);
        Assert.Equal(1.2m, rehydrated.TotalCosts);
        Assert.Null(rehydrated.GrossPnL);
        Assert.Null(rehydrated.NetPnL);
    }

    private static (Trade Trade, decimal CurrentInstrumentPointValue) RoundTrip(
        Trade original,
        decimal? updatedInstrumentTickValue = null)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddReferenceRecords(writeContext, original);
            writeContext.Trades.Add(TradePersistenceMapper.ToRecord(original));
            writeContext.TradeExecutions.AddRange(
                original.Executions.Select(TradeExecutionPersistenceMapper.ToRecord));
            writeContext.SaveChanges();
        }

        if (updatedInstrumentTickValue.HasValue)
        {
            using var updateContext = new JournalDbContext(options);
            InstrumentRecord instrument = updateContext.Instruments
                .Single(record => record.Id == InstrumentId);
            instrument.TickValue = updatedInstrumentTickValue.Value;
            updateContext.SaveChanges();
        }

        using var readContext = new JournalDbContext(options);
        TradeRecord tradeRecord = readContext.Trades
            .AsNoTracking()
            .Single(record => record.Id == original.Id);
        List<TradeExecutionRecord> executionRecords = readContext.TradeExecutions
            .AsNoTracking()
            .Where(record => record.TradeId == original.Id)
            .OrderBy(record => record.Sequence)
            .ToList();
        InstrumentRecord currentInstrument = readContext.Instruments
            .AsNoTracking()
            .Single(record => record.Id == InstrumentId);

        Trade rehydrated = TradePersistenceMapper.ToDomain(
            tradeRecord,
            executionRecords);

        return (
            rehydrated,
            currentInstrument.TickValue / currentInstrument.TickSize);
    }

    private static Trade StartTrade(
        Guid tradeId,
        Guid executionId,
        ExecutionSide side,
        decimal quantity,
        decimal price,
        decimal pointValue,
        decimal commission,
        decimal fees)
    {
        TradeExecution openingExecution = CreateExecution(
            tradeId,
            executionId,
            1,
            side,
            quantity,
            price,
            commission,
            fees);

        return Trade.Start(
            TradingAccountId,
            InstrumentId,
            new TradePricingSnapshot(pointValue, "USD"),
            openingExecution,
            CreatedAtUtc);
    }

    private static void AddExecution(
        Trade trade,
        Guid executionId,
        int sequence,
        ExecutionSide side,
        decimal quantity,
        decimal price,
        decimal commission,
        decimal fees)
    {
        trade.AddExecution(
            CreateExecution(
                trade.Id,
                executionId,
                sequence,
                side,
                quantity,
                price,
                commission,
                fees),
            CreatedAtUtc.AddMinutes(sequence - 1));
    }

    private static TradeExecution CreateExecution(
        Guid tradeId,
        Guid executionId,
        int sequence,
        ExecutionSide side,
        decimal quantity,
        decimal price,
        decimal commission,
        decimal fees)
    {
        return TradeExecution.Rehydrate(
            executionId,
            tradeId,
            sequence,
            FirstExecutionAtUtc.AddMinutes(sequence - 1),
            side,
            quantity,
            price,
            commission,
            fees,
            $"EXEC-{sequence}",
            $"ORDER-{sequence}",
            "NQ");
    }

    private static void AddReferenceRecords(JournalDbContext context, Trade trade)
    {
        context.TradingAccounts.Add(new TradingAccountRecord
        {
            Id = TradingAccountId,
            Name = "Test Account",
            AccountType = TradingAccountType.Demo,
            ProviderName = null,
            ExternalAccountId = null,
            Currency = "USD",
            StartingBalance = null,
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        });

        context.Instruments.Add(new InstrumentRecord
        {
            Id = InstrumentId,
            Symbol = "NQ",
            DisplayName = "Nasdaq-100 E-mini",
            AssetClass = AssetClass.Futures,
            Exchange = "CME",
            Currency = "USD",
            TickSize = 0.25m,
            TickValue = 5m,
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        });

        if (trade.StrategyId.HasValue)
        {
            context.Strategies.Add(new StrategyRecord
            {
                Id = trade.StrategyId.Value,
                Name = "ICT 2022 Model",
                Description = null,
                IsActive = true,
                CreatedAtUtc = CreatedAtUtc,
                UpdatedAtUtc = CreatedAtUtc,
            });
        }

        if (trade.TradingSetupId.HasValue)
        {
            context.TradingSetups.Add(new TradingSetupRecord
            {
                Id = trade.TradingSetupId.Value,
                Name = "Liquidity Sweep + MSS + FVG",
                Description = null,
                IsActive = true,
                CreatedAtUtc = CreatedAtUtc,
                UpdatedAtUtc = CreatedAtUtc,
            });
        }
    }

    private static void AssertExecution(
        TradeExecution execution,
        Guid expectedId,
        int expectedSequence,
        ExecutionSide expectedSide,
        decimal expectedQuantity,
        decimal expectedPrice,
        string expectedExternalExecutionId,
        string expectedExternalOrderId)
    {
        Assert.Equal(expectedId, execution.Id);
        Assert.Equal(expectedSequence, execution.Sequence);
        Assert.Equal(
            FirstExecutionAtUtc.AddMinutes(expectedSequence - 1),
            execution.ExecutedAtUtc);
        Assert.Equal(expectedSide, execution.Side);
        Assert.Equal(expectedQuantity, execution.Quantity);
        Assert.Equal(expectedPrice, execution.Price);
        Assert.Equal(1m, execution.Commission);
        Assert.Equal(0.25m, execution.Fees);
        Assert.Equal(expectedExternalExecutionId, execution.ExternalExecutionId);
        Assert.Equal(expectedExternalOrderId, execution.ExternalOrderId);
        Assert.Equal("NQ", execution.BrokerSymbol);
    }
}
