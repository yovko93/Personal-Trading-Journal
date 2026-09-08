using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Mistakes;
using PersonalTradingJournal.Domain.Screenshots;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Precision;

public sealed class SqliteDateTimeOffsetPersistenceTests
{
    private static readonly Guid TradingAccountId =
        Guid.Parse("1f46c4bd-782d-41e9-84d9-051045670326");

    private static readonly Guid InstrumentId =
        Guid.Parse("32b29ae9-dccd-4ad5-a1f1-205176f398f1");

    private static readonly Guid StrategyId =
        Guid.Parse("db585764-ff3c-4697-a710-d6e86ca20775");

    private static readonly Guid TradingSetupId =
        Guid.Parse("53f692ff-190f-46af-951b-ff0d3e286f4e");

    private static readonly Guid TradingMistakeId =
        Guid.Parse("62cf8f87-0667-4ce2-bf62-a6557685b09e");

    private static readonly Guid TradeId =
        Guid.Parse("d3e2c913-065e-4ec2-a180-3629925b6b3b");

    private static readonly DateTimeOffset BaseTimestampUtc =
        new DateTimeOffset(2026, 9, 2, 10, 0, 0, TimeSpan.Zero)
            .AddTicks(1_234_567);

    [Fact]
    public void ReferenceEntityAuditTimestampsRoundTripExactly()
    {
        InstrumentRecord instrument = CreateInstrument(
            BaseTimestampUtc,
            BaseTimestampUtc.AddHours(1).AddTicks(11));
        TradingAccountRecord account = CreateAccount(
            BaseTimestampUtc.AddHours(2).AddTicks(22),
            BaseTimestampUtc.AddHours(3).AddTicks(33));
        var strategy = new StrategyRecord
        {
            Id = StrategyId,
            Name = "Timestamp Strategy",
            Description = null,
            IsActive = true,
            CreatedAtUtc = BaseTimestampUtc.AddHours(4).AddTicks(44),
            UpdatedAtUtc = BaseTimestampUtc.AddHours(5).AddTicks(55),
        };
        var setup = new TradingSetupRecord
        {
            Id = TradingSetupId,
            Name = "Timestamp Setup",
            Description = null,
            IsActive = true,
            CreatedAtUtc = BaseTimestampUtc.AddHours(6).AddTicks(66),
            UpdatedAtUtc = BaseTimestampUtc.AddHours(7).AddTicks(77),
        };
        TradingMistakeRecord tradingMistake = CreateTradingMistake(
            BaseTimestampUtc.AddHours(8).AddTicks(88),
            BaseTimestampUtc.AddHours(9).AddTicks(99));

        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            writeContext.Instruments.Add(instrument);
            writeContext.TradingAccounts.Add(account);
            writeContext.Strategies.Add(strategy);
            writeContext.TradingSetups.Add(setup);
            writeContext.TradingMistakes.Add(tradingMistake);
            writeContext.SaveChanges();
        }

        using var readContext = new JournalDbContext(options);
        InstrumentRecord persistedInstrument = readContext.Instruments
            .AsNoTracking()
            .Single(record => record.Id == InstrumentId);
        TradingAccountRecord persistedAccount = readContext.TradingAccounts
            .AsNoTracking()
            .Single(record => record.Id == TradingAccountId);
        StrategyRecord persistedStrategy = readContext.Strategies
            .AsNoTracking()
            .Single(record => record.Id == StrategyId);
        TradingSetupRecord persistedSetup = readContext.TradingSetups
            .AsNoTracking()
            .Single(record => record.Id == TradingSetupId);
        TradingMistakeRecord persistedTradingMistake = readContext.TradingMistakes
            .AsNoTracking()
            .Single(record => record.Id == TradingMistakeId);

        AssertTimestampPair(instrument, persistedInstrument);
        AssertTimestampPair(account, persistedAccount);
        AssertTimestampPair(strategy, persistedStrategy);
        AssertTimestampPair(setup, persistedSetup);
        AssertTimestampPair(tradingMistake, persistedTradingMistake);
    }

    [Fact]
    public void TradeAndExecutionTimestampsRoundTripExactly()
    {
        DateTimeOffset executedAtUtc = BaseTimestampUtc.AddDays(1).AddTicks(101);
        DateTimeOffset createdAtUtc = executedAtUtc.AddHours(1).AddTicks(202);
        DateTimeOffset updatedAtUtc = createdAtUtc.AddHours(2).AddTicks(303);
        TradeRecord trade = CreateTrade(createdAtUtc, updatedAtUtc);
        TradeExecutionRecord execution = CreateExecution(
            Guid.Parse("47e41da0-5581-4831-839a-1ad125d17b91"),
            1,
            executedAtUtc,
            ExecutionSide.Buy);

        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddTradeParents(writeContext);
            writeContext.Trades.Add(trade);
            writeContext.TradeExecutions.Add(execution);
            writeContext.SaveChanges();
        }

        using var readContext = new JournalDbContext(options);
        TradeRecord persistedTrade = readContext.Trades
            .AsNoTracking()
            .Single(record => record.Id == TradeId);
        TradeExecutionRecord persistedExecution = readContext.TradeExecutions
            .AsNoTracking()
            .Single(record => record.Id == execution.Id);
        TradeExecution rehydratedExecution =
            TradeExecutionPersistenceMapper.ToDomain(persistedExecution);

        AssertExactUtc(createdAtUtc, persistedTrade.CreatedAtUtc);
        AssertExactUtc(updatedAtUtc, persistedTrade.UpdatedAtUtc);
        AssertExactUtc(executedAtUtc, persistedExecution.ExecutedAtUtc);
        AssertExactUtc(executedAtUtc, rehydratedExecution.ExecutedAtUtc);
    }

    [Fact]
    public void TradeAggregatePreservesExecutionAndAuditTimelinesIndependently()
    {
        DateTimeOffset openedAtUtc =
            new DateTimeOffset(2026, 9, 2, 13, 30, 15, TimeSpan.Zero)
                .AddTicks(1_234_567);
        DateTimeOffset closedAtUtc =
            new DateTimeOffset(2026, 9, 2, 13, 35, 45, TimeSpan.Zero)
                .AddTicks(7_654_321);
        DateTimeOffset createdAtUtc =
            new DateTimeOffset(2026, 9, 2, 14, 0, 0, TimeSpan.Zero)
                .AddTicks(2_468_013);
        DateTimeOffset updatedAtUtc =
            new DateTimeOffset(2026, 9, 2, 15, 0, 0, TimeSpan.Zero)
                .AddTicks(9_876_543);

        TradeExecution openingExecution = TradeExecution.Rehydrate(
            Guid.Parse("5fda3a06-36a8-409e-816e-54f448817a29"),
            TradeId,
            1,
            openedAtUtc,
            ExecutionSide.Buy,
            1m,
            100m,
            0.5m,
            0.1m,
            null,
            null,
            "NQ");
        Trade original = Trade.Start(
            TradingAccountId,
            InstrumentId,
            new TradePricingSnapshot(20m, "USD"),
            openingExecution,
            createdAtUtc);
        TradeExecution closingExecution = TradeExecution.Rehydrate(
            Guid.Parse("cb58d6c8-9840-4f08-9eea-e6d5db817e7f"),
            TradeId,
            2,
            closedAtUtc,
            ExecutionSide.Sell,
            1m,
            101m,
            0.5m,
            0.1m,
            null,
            null,
            "NQ");
        original.AddExecution(closingExecution, createdAtUtc.AddMinutes(5));
        original.SetClassification(StrategyId, null, updatedAtUtc);

        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddTradeParents(writeContext);
            writeContext.Strategies.Add(new StrategyRecord
            {
                Id = StrategyId,
                Name = "Reviewed Strategy",
                Description = null,
                IsActive = true,
                CreatedAtUtc = createdAtUtc,
                UpdatedAtUtc = createdAtUtc,
            });
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
            List<TradeExecutionRecord> executions = readContext.TradeExecutions
                .AsNoTracking()
                .Where(record => record.TradeId == TradeId)
                .OrderBy(record => record.Sequence)
                .ToList();

            rehydrated = TradePersistenceMapper.ToDomain(tradeRecord, executions);
        }

        Assert.Equal(TradeStatus.Closed, rehydrated.Status);
        AssertExactUtc(openedAtUtc, rehydrated.OpenedAtUtc);
        Assert.True(rehydrated.ClosedAtUtc.HasValue);
        AssertExactUtc(closedAtUtc, rehydrated.ClosedAtUtc.Value);
        AssertExactUtc(createdAtUtc, rehydrated.CreatedAtUtc);
        AssertExactUtc(updatedAtUtc, rehydrated.UpdatedAtUtc);
        Assert.True(rehydrated.ClosedAtUtc < rehydrated.UpdatedAtUtc);
    }

    [Fact]
    public void ScreenshotCaptureAndAuditTimestampsRoundTripIndependently()
    {
        DateTimeOffset capturedAtUtc =
            new DateTimeOffset(2025, 12, 31, 23, 59, 59, TimeSpan.Zero)
                .AddTicks(9_876_543);
        DateTimeOffset createdAtUtc =
            new DateTimeOffset(2026, 1, 1, 0, 5, 0, TimeSpan.Zero)
                .AddTicks(1_111_111);
        DateTimeOffset updatedAtUtc = createdAtUtc.AddHours(1).AddTicks(2_222_222);
        TradeScreenshot populated = TradeScreenshot.Rehydrate(
            Guid.Parse("038dd257-0240-4b45-a2d7-a142244ae0b0"),
            TradeId,
            TradeScreenshotType.Entry,
            "future/object-store/trades/timestamp/chart-01",
            "chart-01.png",
            capturedAtUtc,
            "5m",
            null,
            createdAtUtc,
            updatedAtUtc);
        TradeScreenshot withoutCapture = TradeScreenshot.Rehydrate(
            Guid.Parse("bd586c0f-c092-4910-80d7-8d644225a05d"),
            TradeId,
            TradeScreenshotType.PostTrade,
            "future/object-store/trades/timestamp/chart-02",
            "chart-02.png",
            null,
            null,
            null,
            createdAtUtc.AddDays(1),
            updatedAtUtc.AddDays(1));

        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddTradeParents(writeContext);
            writeContext.Trades.Add(CreateTrade(createdAtUtc, updatedAtUtc));
            writeContext.TradeScreenshots.AddRange(
                TradeScreenshotPersistenceMapper.ToRecord(populated),
                TradeScreenshotPersistenceMapper.ToRecord(withoutCapture));
            writeContext.SaveChanges();
        }

        using var readContext = new JournalDbContext(options);
        List<TradeScreenshot> rehydrated = readContext.TradeScreenshots
            .AsNoTracking()
            .OrderBy(record => record.FileName)
            .ToList()
            .Select(TradeScreenshotPersistenceMapper.ToDomain)
            .ToList();

        TradeScreenshot rehydratedPopulated = rehydrated.Single(
            screenshot => screenshot.Id == populated.Id);
        TradeScreenshot rehydratedNull = rehydrated.Single(
            screenshot => screenshot.Id == withoutCapture.Id);

        Assert.True(rehydratedPopulated.CapturedAtUtc.HasValue);
        AssertExactUtc(capturedAtUtc, rehydratedPopulated.CapturedAtUtc.Value);
        AssertExactUtc(createdAtUtc, rehydratedPopulated.CreatedAtUtc);
        AssertExactUtc(updatedAtUtc, rehydratedPopulated.UpdatedAtUtc);
        Assert.NotEqual(
            rehydratedPopulated.CapturedAtUtc,
            rehydratedPopulated.CreatedAtUtc);
        Assert.NotEqual(
            rehydratedPopulated.CreatedAtUtc,
            rehydratedPopulated.UpdatedAtUtc);
        Assert.Null(rehydratedNull.CapturedAtUtc);
    }

    [Fact]
    public void TradeMistakeAuditTimestampsRoundTripExactly()
    {
        DateTimeOffset createdAtUtc = BaseTimestampUtc.AddDays(3).AddTicks(333);
        DateTimeOffset updatedAtUtc = createdAtUtc.AddHours(4).AddTicks(444);
        TradeMistake original = TradeMistake.Rehydrate(
            Guid.Parse("54c0aaf8-75bc-4355-97bb-258ced93dc11"),
            TradeId,
            TradingMistakeId,
            "Timestamp characterization.",
            createdAtUtc,
            updatedAtUtc);

        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddTradeParents(writeContext);
            writeContext.Trades.Add(CreateTrade(createdAtUtc, updatedAtUtc));
            writeContext.TradingMistakes.Add(
                CreateTradingMistake(createdAtUtc, updatedAtUtc));
            writeContext.TradeMistakes.Add(
                TradeMistakePersistenceMapper.ToRecord(original));
            writeContext.SaveChanges();
        }

        using var readContext = new JournalDbContext(options);
        TradeMistakeRecord record = readContext.TradeMistakes
            .AsNoTracking()
            .Single(candidate => candidate.Id == original.Id);
        TradeMistake rehydrated = TradeMistakePersistenceMapper.ToDomain(record);

        AssertExactUtc(createdAtUtc, rehydrated.CreatedAtUtc);
        AssertExactUtc(updatedAtUtc, rehydrated.UpdatedAtUtc);
    }

    [Fact]
    public void DateTimeOffsetEqualityPredicateIsTranslatedBySqlite()
    {
        DateTimeOffset target = BaseTimestampUtc.AddDays(4).AddTicks(404);

        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);
        PersistQueryExecutions(options, target);

        using var readContext = new JournalDbContext(options);
        int matchingSequence = readContext.TradeExecutions
            .AsNoTracking()
            .Where(record => record.ExecutedAtUtc == target)
            .Select(record => record.Sequence)
            .Single();

        Assert.Equal(2, matchingSequence);
    }

    [Fact]
    public void TimestampPropertiesHaveNoInappropriateExplicitConverters()
    {
        var options = new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new JournalDbContext(options);
        (Type EntityType, string PropertyName)[] timestampProperties =
        [
            (typeof(InstrumentRecord), nameof(InstrumentRecord.CreatedAtUtc)),
            (typeof(InstrumentRecord), nameof(InstrumentRecord.UpdatedAtUtc)),
            (typeof(TradingAccountRecord), nameof(TradingAccountRecord.CreatedAtUtc)),
            (typeof(TradingAccountRecord), nameof(TradingAccountRecord.UpdatedAtUtc)),
            (typeof(StrategyRecord), nameof(StrategyRecord.CreatedAtUtc)),
            (typeof(StrategyRecord), nameof(StrategyRecord.UpdatedAtUtc)),
            (typeof(TradingSetupRecord), nameof(TradingSetupRecord.CreatedAtUtc)),
            (typeof(TradingSetupRecord), nameof(TradingSetupRecord.UpdatedAtUtc)),
            (typeof(TradingMistakeRecord), nameof(TradingMistakeRecord.CreatedAtUtc)),
            (typeof(TradingMistakeRecord), nameof(TradingMistakeRecord.UpdatedAtUtc)),
            (typeof(TradeRecord), nameof(TradeRecord.CreatedAtUtc)),
            (typeof(TradeRecord), nameof(TradeRecord.UpdatedAtUtc)),
            (typeof(TradeExecutionRecord), nameof(TradeExecutionRecord.ExecutedAtUtc)),
            (typeof(TradeScreenshotRecord), nameof(TradeScreenshotRecord.CapturedAtUtc)),
            (typeof(TradeScreenshotRecord), nameof(TradeScreenshotRecord.CreatedAtUtc)),
            (typeof(TradeScreenshotRecord), nameof(TradeScreenshotRecord.UpdatedAtUtc)),
            (typeof(TradeMistakeRecord), nameof(TradeMistakeRecord.CreatedAtUtc)),
            (typeof(TradeMistakeRecord), nameof(TradeMistakeRecord.UpdatedAtUtc)),
        ];

        foreach ((Type entityType, string propertyName) in timestampProperties)
        {
            var property = context.Model.FindEntityType(entityType)!
                .FindProperty(propertyName)!;
            var converter = property.GetValueConverter();

            Assert.NotEqual(typeof(DateTime), converter?.ProviderClrType);
            Assert.NotEqual(typeof(long), converter?.ProviderClrType);
            Assert.NotEqual(typeof(double), converter?.ProviderClrType);
        }
    }

    private static InstrumentRecord CreateInstrument(
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        return new InstrumentRecord
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
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = updatedAtUtc,
        };
    }

    private static TradingAccountRecord CreateAccount(
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        return new TradingAccountRecord
        {
            Id = TradingAccountId,
            Name = "Test Account",
            AccountType = TradingAccountType.Demo,
            ProviderName = null,
            ExternalAccountId = null,
            Currency = "USD",
            StartingBalance = null,
            IsActive = true,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = updatedAtUtc,
        };
    }

    private static TradingMistakeRecord CreateTradingMistake(
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        return new TradingMistakeRecord
        {
            Id = TradingMistakeId,
            Name = "FOMO",
            Description = null,
            IsActive = true,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = updatedAtUtc,
        };
    }

    private static TradeRecord CreateTrade(
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        return new TradeRecord
        {
            Id = TradeId,
            TradingAccountId = TradingAccountId,
            InstrumentId = InstrumentId,
            PricingPointValue = 20m,
            PricingCurrency = "USD",
            StrategyId = null,
            TradingSetupId = null,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = updatedAtUtc,
        };
    }

    private static TradeExecutionRecord CreateExecution(
        Guid id,
        int sequence,
        DateTimeOffset executedAtUtc,
        ExecutionSide side)
    {
        return new TradeExecutionRecord
        {
            Id = id,
            TradeId = TradeId,
            Sequence = sequence,
            ExecutedAtUtc = executedAtUtc,
            Side = side,
            Quantity = 1m,
            Price = 100m,
            Commission = 0m,
            Fees = 0m,
            ExternalExecutionId = null,
            ExternalOrderId = null,
            BrokerSymbol = "NQ",
        };
    }

    private static void AddTradeParents(JournalDbContext context)
    {
        context.TradingAccounts.Add(CreateAccount(BaseTimestampUtc, BaseTimestampUtc));
        context.Instruments.Add(CreateInstrument(BaseTimestampUtc, BaseTimestampUtc));
    }

    private static void PersistQueryExecutions(
        DbContextOptions<JournalDbContext> options,
        DateTimeOffset target)
    {
        using var writeContext = new JournalDbContext(options);
        writeContext.Database.EnsureCreated();
        AddTradeParents(writeContext);
        writeContext.Trades.Add(CreateTrade(BaseTimestampUtc, BaseTimestampUtc));
        writeContext.TradeExecutions.AddRange(
            CreateExecution(
                Guid.Parse("66545cee-162f-49cb-914f-d49d32cad65e"),
                1,
                target.AddMinutes(-2),
                ExecutionSide.Buy),
            CreateExecution(
                Guid.Parse("2b15f76b-a4bf-4e3b-9034-b6de56d76bab"),
                2,
                target,
                ExecutionSide.Buy),
            CreateExecution(
                Guid.Parse("213c0adc-804e-434b-bcb7-9c5ca5e2cd13"),
                3,
                target.AddMinutes(2),
                ExecutionSide.Buy));
        writeContext.SaveChanges();
    }

    private static void AssertTimestampPair(
        InstrumentRecord expected,
        InstrumentRecord actual)
    {
        AssertExactUtc(expected.CreatedAtUtc, actual.CreatedAtUtc);
        AssertExactUtc(expected.UpdatedAtUtc, actual.UpdatedAtUtc);
    }

    private static void AssertTimestampPair(
        TradingAccountRecord expected,
        TradingAccountRecord actual)
    {
        AssertExactUtc(expected.CreatedAtUtc, actual.CreatedAtUtc);
        AssertExactUtc(expected.UpdatedAtUtc, actual.UpdatedAtUtc);
    }

    private static void AssertTimestampPair(
        StrategyRecord expected,
        StrategyRecord actual)
    {
        AssertExactUtc(expected.CreatedAtUtc, actual.CreatedAtUtc);
        AssertExactUtc(expected.UpdatedAtUtc, actual.UpdatedAtUtc);
    }

    private static void AssertTimestampPair(
        TradingSetupRecord expected,
        TradingSetupRecord actual)
    {
        AssertExactUtc(expected.CreatedAtUtc, actual.CreatedAtUtc);
        AssertExactUtc(expected.UpdatedAtUtc, actual.UpdatedAtUtc);
    }

    private static void AssertTimestampPair(
        TradingMistakeRecord expected,
        TradingMistakeRecord actual)
    {
        AssertExactUtc(expected.CreatedAtUtc, actual.CreatedAtUtc);
        AssertExactUtc(expected.UpdatedAtUtc, actual.UpdatedAtUtc);
    }

    private static void AssertExactUtc(
        DateTimeOffset expected,
        DateTimeOffset actual)
    {
        Assert.Equal(expected, actual);
        Assert.Equal(TimeSpan.Zero, actual.Offset);
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
