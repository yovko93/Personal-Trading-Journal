using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Precision;

public sealed class SqliteDecimalPersistenceTests
{
    private const decimal HighPrecisionFraction =
        0.1234567890123456789012345678m;

    private const decimal HighMagnitudeFraction =
        123456789012345.67890123456789m;

    private static readonly Guid TradingAccountId =
        Guid.Parse("bd8bb56b-66e9-4e43-b6ef-e2b37099682f");

    private static readonly Guid InstrumentId =
        Guid.Parse("b0b22086-5214-484f-bb28-4137aa63ba63");

    private static readonly Guid TradeId =
        Guid.Parse("1aedb8ab-437c-43b9-9ff6-070f32c7ed2c");

    private static readonly DateTimeOffset FirstExecutionAtUtc =
        new(2026, 9, 1, 13, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 1, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void InstrumentDecimalsRoundTripExactly()
    {
        var commonContract = CreateInstrument(
            Guid.Parse("9685340d-5ce4-4928-b002-7f2650b38246"),
            "NQ",
            0.25m,
            5m);
        var highPrecisionContract = CreateInstrument(
            Guid.Parse("a87e7c0c-dc2f-484a-8b87-546a6365cfdd"),
            "PRECISION",
            HighPrecisionFraction,
            12345.678901234567890123456789m);

        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            writeContext.Instruments.AddRange(commonContract, highPrecisionContract);
            writeContext.SaveChanges();
        }

        using var readContext = new JournalDbContext(options);
        List<InstrumentRecord> persisted = readContext.Instruments
            .AsNoTracking()
            .OrderBy(record => record.Symbol)
            .ToList();

        InstrumentRecord common = persisted.Single(record => record.Symbol == "NQ");
        Assert.Equal(0.25m, common.TickSize);
        Assert.Equal(5m, common.TickValue);

        InstrumentRecord precise = persisted.Single(
            record => record.Symbol == "PRECISION");
        Assert.Equal(HighPrecisionFraction, precise.TickSize);
        Assert.Equal(12345.678901234567890123456789m, precise.TickValue);
    }

    [Fact]
    public void StartingBalanceStatesRoundTripExactlyAndRemainDistinct()
    {
        var nullBalance = CreateAccount(
            Guid.Parse("c5dc7d33-e7e3-4d7d-a38c-146860456b1b"),
            "Null Balance",
            null);
        var zeroBalance = CreateAccount(
            Guid.Parse("822e5ddd-d1c9-47b0-bb28-533426753d00"),
            "Zero Balance",
            0m);
        var preciseBalance = CreateAccount(
            Guid.Parse("ae3a4587-b219-42e9-9a28-3840a0938577"),
            "Precise Balance",
            12345.67m);
        var highPrecisionBalance = CreateAccount(
            Guid.Parse("2dad02c4-dd95-4532-a791-c494b0e50b01"),
            "High Precision Balance",
            HighMagnitudeFraction);

        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            writeContext.TradingAccounts.AddRange(
                nullBalance,
                zeroBalance,
                preciseBalance,
                highPrecisionBalance);
            writeContext.SaveChanges();
        }

        using var readContext = new JournalDbContext(options);
        Dictionary<string, decimal?> balances = readContext.TradingAccounts
            .AsNoTracking()
            .ToDictionary(record => record.Name, record => record.StartingBalance);

        Assert.Null(balances["Null Balance"]);
        Assert.Equal(0m, balances["Zero Balance"]);
        Assert.Equal(12345.67m, balances["Precise Balance"]);
        Assert.Equal(HighMagnitudeFraction, balances["High Precision Balance"]);
    }

    [Fact]
    public void HistoricalPricingPointValueRoundTripsExactly()
    {
        const decimal pricingPointValue =
            37.630000000000000000000000001m;

        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddRequiredTradeParents(writeContext);
            writeContext.Trades.Add(CreateTradeRecord(pricingPointValue));
            writeContext.SaveChanges();
        }

        using var readContext = new JournalDbContext(options);
        decimal persistedPointValue = readContext.Trades
            .AsNoTracking()
            .Single(record => record.Id == TradeId)
            .PricingPointValue;

        Assert.Equal(pricingPointValue, persistedPointValue);
    }

    [Fact]
    public void ExecutionDecimalsRoundTripExactly()
    {
        List<TradeExecutionRecord> originalRecords =
        [
            CreateExecutionRecord(1, 0.1m, 0.1m, 0m, 0.01m),
            CreateExecutionRecord(2, 0.25m, 0.2m, 0.01m, 0.1m),
            CreateExecutionRecord(3, 0.5m, 0.3m, 0.1m, 1.25m),
            CreateExecutionRecord(4, 1.23456789m, -37.63m, 1.25m, 0m),
            CreateExecutionRecord(
                5,
                HighPrecisionFraction,
                12345.67m,
                0.0000000000000000000000000001m,
                HighPrecisionFraction),
            CreateExecutionRecord(
                6,
                1.0000000000000000000000000001m,
                HighMagnitudeFraction,
                HighPrecisionFraction,
                12345.678901234567890123456789m),
        ];

        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddRequiredTradeParents(writeContext);
            writeContext.Trades.Add(CreateTradeRecord(20m));
            writeContext.TradeExecutions.AddRange(originalRecords);
            writeContext.SaveChanges();
        }

        using var readContext = new JournalDbContext(options);
        List<TradeExecutionRecord> persistedRecords = readContext.TradeExecutions
            .AsNoTracking()
            .OrderBy(record => record.Sequence)
            .ToList();

        Assert.Equal(originalRecords.Count, persistedRecords.Count);

        for (int index = 0; index < originalRecords.Count; index++)
        {
            TradeExecutionRecord original = originalRecords[index];
            TradeExecutionRecord persisted = persistedRecords[index];

            Assert.Equal(original.Quantity, persisted.Quantity);
            Assert.Equal(original.Price, persisted.Price);
            Assert.Equal(original.Commission, persisted.Commission);
            Assert.Equal(original.Fees, persisted.Fees);
        }
    }

    [Fact]
    public void ClosedTradeEconomicsRemainExactAfterSqliteRoundTrip()
    {
        const decimal pointValue = 2.5m;
        const decimal quantity = 0.12345678m;
        const decimal entryPrice = 37.63m;
        const decimal exitPrice = 37.93m;
        const decimal entryCommission = 0.01m;
        const decimal entryFees = 0.0012345678m;
        const decimal exitCommission = 0.02m;
        const decimal exitFees = 0.0023456789m;
        const decimal expectedTotalCosts = 0.0335802467m;
        const decimal expectedGrossPnl = 0.092592585m;
        const decimal expectedNetPnl = 0.0590123383m;

        TradeExecution openingExecution = TradeExecution.Rehydrate(
            Guid.Parse("33ebf01f-5708-40cd-85a9-3fb8e7acef88"),
            TradeId,
            1,
            FirstExecutionAtUtc,
            ExecutionSide.Buy,
            quantity,
            entryPrice,
            entryCommission,
            entryFees,
            null,
            null,
            "NQ");
        Trade original = Trade.Start(
            TradingAccountId,
            InstrumentId,
            new TradePricingSnapshot(pointValue, "USD"),
            openingExecution,
            CreatedAtUtc);
        TradeExecution closingExecution = TradeExecution.Rehydrate(
            Guid.Parse("d574c9ad-ff2d-4b98-bd86-490aa0860fed"),
            TradeId,
            2,
            FirstExecutionAtUtc.AddMinutes(1),
            ExecutionSide.Sell,
            quantity,
            exitPrice,
            exitCommission,
            exitFees,
            null,
            null,
            "NQ");
        original.AddExecution(closingExecution, CreatedAtUtc.AddMinutes(1));

        Assert.Equal(expectedGrossPnl, original.GrossPnL);
        Assert.Equal(expectedNetPnl, original.NetPnL);
        Assert.Equal(expectedTotalCosts, original.TotalCosts);

        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddRequiredTradeParents(writeContext);
            writeContext.Trades.Add(TradePersistenceMapper.ToRecord(original));
            writeContext.TradeExecutions.AddRange(
                original.Executions.Select(TradeExecutionPersistenceMapper.ToRecord));
            writeContext.SaveChanges();
        }

        Trade rehydrated;
        using (var readContext = new JournalDbContext(options))
        {
            TradeRecord tradeRecord = readContext.Trades
                .AsNoTracking()
                .Single(record => record.Id == TradeId);
            List<TradeExecutionRecord> executionRecords = readContext.TradeExecutions
                .AsNoTracking()
                .Where(record => record.TradeId == TradeId)
                .OrderBy(record => record.Sequence)
                .ToList();

            rehydrated = TradePersistenceMapper.ToDomain(
                tradeRecord,
                executionRecords);
        }

        Assert.Equal(TradeStatus.Closed, rehydrated.Status);
        Assert.Equal(pointValue, rehydrated.Pricing.PointValue);
        Assert.Equal(quantity, rehydrated.Executions[0].Quantity);
        Assert.Equal(quantity, rehydrated.Executions[1].Quantity);
        Assert.Equal(entryPrice, rehydrated.Executions[0].Price);
        Assert.Equal(exitPrice, rehydrated.Executions[1].Price);
        Assert.Equal(entryCommission, rehydrated.Executions[0].Commission);
        Assert.Equal(entryFees, rehydrated.Executions[0].Fees);
        Assert.Equal(exitCommission, rehydrated.Executions[1].Commission);
        Assert.Equal(exitFees, rehydrated.Executions[1].Fees);
        Assert.Equal(entryPrice, rehydrated.AverageEntryPrice);
        Assert.Equal(exitPrice, rehydrated.AverageExitPrice);
        Assert.Equal(expectedTotalCosts, rehydrated.TotalCosts);
        Assert.Equal(expectedGrossPnl, rehydrated.GrossPnL);
        Assert.Equal(expectedNetPnl, rehydrated.NetPnL);
        Assert.Equal(original.TotalCosts, rehydrated.TotalCosts);
        Assert.Equal(original.GrossPnL, rehydrated.GrossPnL);
        Assert.Equal(original.NetPnL, rehydrated.NetPnL);
    }

    [Fact]
    public void DecimalPropertiesHaveNoExplicitFloatingPointConverters()
    {
        var options = new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new JournalDbContext(options);
        (Type EntityType, string PropertyName)[] decimalProperties =
        [
            (typeof(InstrumentRecord), nameof(InstrumentRecord.TickSize)),
            (typeof(InstrumentRecord), nameof(InstrumentRecord.TickValue)),
            (typeof(TradingAccountRecord), nameof(TradingAccountRecord.StartingBalance)),
            (typeof(TradeRecord), nameof(TradeRecord.PricingPointValue)),
            (typeof(TradeExecutionRecord), nameof(TradeExecutionRecord.Quantity)),
            (typeof(TradeExecutionRecord), nameof(TradeExecutionRecord.Price)),
            (typeof(TradeExecutionRecord), nameof(TradeExecutionRecord.Commission)),
            (typeof(TradeExecutionRecord), nameof(TradeExecutionRecord.Fees)),
        ];

        foreach ((Type entityType, string propertyName) in decimalProperties)
        {
            var property = context.Model.FindEntityType(entityType)!
                .FindProperty(propertyName)!;
            var converter = property.GetValueConverter();

            Assert.NotEqual(typeof(double), converter?.ProviderClrType);
            Assert.NotEqual(typeof(float), converter?.ProviderClrType);
        }
    }

    private static InstrumentRecord CreateInstrument(
        Guid id,
        string symbol,
        decimal tickSize,
        decimal tickValue)
    {
        return new InstrumentRecord
        {
            Id = id,
            Symbol = symbol,
            DisplayName = symbol,
            AssetClass = AssetClass.Futures,
            Exchange = "CME",
            Currency = "USD",
            TickSize = tickSize,
            TickValue = tickValue,
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        };
    }

    private static TradingAccountRecord CreateAccount(
        Guid id,
        string name,
        decimal? startingBalance)
    {
        return new TradingAccountRecord
        {
            Id = id,
            Name = name,
            AccountType = TradingAccountType.Demo,
            ProviderName = null,
            ExternalAccountId = null,
            Currency = "USD",
            StartingBalance = startingBalance,
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        };
    }

    private static TradeRecord CreateTradeRecord(decimal pricingPointValue)
    {
        return new TradeRecord
        {
            Id = TradeId,
            TradingAccountId = TradingAccountId,
            InstrumentId = InstrumentId,
            PricingPointValue = pricingPointValue,
            PricingCurrency = "USD",
            StrategyId = null,
            TradingSetupId = null,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        };
    }

    private static TradeExecutionRecord CreateExecutionRecord(
        int sequence,
        decimal quantity,
        decimal price,
        decimal commission,
        decimal fees)
    {
        return new TradeExecutionRecord
        {
            Id = Guid.NewGuid(),
            TradeId = TradeId,
            Sequence = sequence,
            ExecutedAtUtc = FirstExecutionAtUtc.AddMinutes(sequence - 1),
            Side = ExecutionSide.Buy,
            Quantity = quantity,
            Price = price,
            Commission = commission,
            Fees = fees,
            ExternalExecutionId = null,
            ExternalOrderId = null,
            BrokerSymbol = "NQ",
        };
    }

    private static void AddRequiredTradeParents(JournalDbContext context)
    {
        context.TradingAccounts.Add(
            CreateAccount(TradingAccountId, "Test Account", 12345.67m));
        context.Instruments.Add(
            CreateInstrument(InstrumentId, "NQ", 0.25m, 5m));
    }

    private static SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static DbContextOptions<JournalDbContext> CreateOptions(
        SqliteConnection connection)
    {
        return new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite(connection)
            .Options;
    }
}
