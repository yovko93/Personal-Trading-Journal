using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Screenshots;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Screenshots;

public sealed class TradeScreenshotRecordRoundTripTests
{
    private static readonly Guid TradingAccountId =
        Guid.Parse("8f2e1e7b-ee3a-4189-9f3f-b70aa91dd558");

    private static readonly Guid InstrumentId =
        Guid.Parse("9d92f970-a781-4fb3-a5b7-4616632dccbd");

    private static readonly Guid TradeId =
        Guid.Parse("5425ddfb-452b-4f96-8e69-6ef91a207b41");

    private static readonly Guid ScreenshotId =
        Guid.Parse("057cf2c3-53c4-481c-8a1d-5db40895bdfa");

    private static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 8, 24, 13, 45, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 8, 24, 14, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset UpdatedAtUtc =
        CreatedAtUtc.AddMinutes(15);

    [Fact]
    public void PopulatedScreenshotRoundTripsThroughSqlite()
    {
        TradeScreenshot original = CreateScreenshot();

        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddTradeGraph(writeContext);
            writeContext.TradeScreenshots.Add(
                TradeScreenshotPersistenceMapper.ToRecord(original));
            writeContext.SaveChanges();
        }

        TradeScreenshot rehydrated;
        using (var readContext = new JournalDbContext(options))
        {
            TradeScreenshotRecord record = readContext.TradeScreenshots
                .AsNoTracking()
                .Single(candidate => candidate.Id == original.Id);

            rehydrated = TradeScreenshotPersistenceMapper.ToDomain(record);
        }

        AssertScreenshot(original, rehydrated);
        Assert.Equal(
            "future/object-store/trades/abc/chart-01",
            rehydrated.StorageKey);
    }

    [Fact]
    public void NullOptionalMetadataRoundTripsThroughSqlite()
    {
        TradeScreenshot original = CreateScreenshot(
            null,
            null,
            null);

        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddTradeGraph(writeContext);
            writeContext.TradeScreenshots.Add(
                TradeScreenshotPersistenceMapper.ToRecord(original));
            writeContext.SaveChanges();
        }

        using var readContext = new JournalDbContext(options);
        TradeScreenshotRecord record = readContext.TradeScreenshots
            .AsNoTracking()
            .Single(candidate => candidate.Id == original.Id);
        TradeScreenshot rehydrated =
            TradeScreenshotPersistenceMapper.ToDomain(record);

        AssertScreenshot(original, rehydrated);
        Assert.Null(rehydrated.CapturedAtUtc);
        Assert.Null(rehydrated.Timeframe);
        Assert.Null(rehydrated.Description);
    }

    [Fact]
    public void MissingTradeIsRejectedBySqliteForeignKey()
    {
        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using var context = new JournalDbContext(options);
        context.Database.EnsureCreated();
        TradeScreenshotRecord record = CreateScreenshotRecord();
        record.TradeId = Guid.NewGuid();
        context.TradeScreenshots.Add(record);

        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }

    [Fact]
    public void DeletingTradeWithScreenshotIsRejectedBySqlite()
    {
        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddTradeGraph(writeContext);
            writeContext.TradeScreenshots.Add(CreateScreenshotRecord());
            writeContext.SaveChanges();
        }

        using var deleteContext = new JournalDbContext(options);
        TradeRecord trade = deleteContext.Trades.Single(record => record.Id == TradeId);
        deleteContext.Trades.Remove(trade);

        Assert.Throws<DbUpdateException>(() => deleteContext.SaveChanges());
    }

    [Fact]
    public void DeletingScreenshotDoesNotDeleteTrade()
    {
        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddTradeGraph(writeContext);
            writeContext.TradeScreenshots.Add(CreateScreenshotRecord());
            writeContext.SaveChanges();
        }

        using (var deleteContext = new JournalDbContext(options))
        {
            TradeScreenshotRecord screenshot = deleteContext.TradeScreenshots
                .Single(record => record.Id == ScreenshotId);
            deleteContext.TradeScreenshots.Remove(screenshot);
            deleteContext.SaveChanges();
        }

        using var readContext = new JournalDbContext(options);
        Assert.Empty(readContext.TradeScreenshots);
        Assert.True(readContext.Trades.Any(record => record.Id == TradeId));
    }

    [Fact]
    public void ModelMapsScreenshotMetadataAndRestrictiveTradeRelationship()
    {
        var options = new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new JournalDbContext(options);
        IEntityType entityType =
            context.Model.FindEntityType(typeof(TradeScreenshotRecord))!;

        Assert.Equal("TradeScreenshots", entityType.GetTableName());
        Assert.Equal(
            ValueGenerated.Never,
            entityType.FindProperty(nameof(TradeScreenshotRecord.Id))!.ValueGenerated);
        Assert.Equal(
            typeof(int),
            entityType.FindProperty(nameof(TradeScreenshotRecord.Type))!
                .GetProviderClrType());
        Assert.Equal(
            512,
            entityType.FindProperty(nameof(TradeScreenshotRecord.StorageKey))!
                .GetMaxLength());
        Assert.Equal(
            255,
            entityType.FindProperty(nameof(TradeScreenshotRecord.FileName))!
                .GetMaxLength());

        IProperty capturedAtProperty =
            entityType.FindProperty(nameof(TradeScreenshotRecord.CapturedAtUtc))!;
        Assert.True(capturedAtProperty.IsNullable);

        IProperty timeframeProperty =
            entityType.FindProperty(nameof(TradeScreenshotRecord.Timeframe))!;
        Assert.True(timeframeProperty.IsNullable);
        Assert.Equal(32, timeframeProperty.GetMaxLength());

        IProperty descriptionProperty =
            entityType.FindProperty(nameof(TradeScreenshotRecord.Description))!;
        Assert.True(descriptionProperty.IsNullable);
        Assert.Equal(2000, descriptionProperty.GetMaxLength());

        IForeignKey foreignKey = Assert.Single(entityType.GetForeignKeys());
        Assert.Equal(
            nameof(TradeScreenshotRecord.TradeId),
            foreignKey.Properties.Single().Name);
        Assert.Equal(typeof(TradeRecord), foreignKey.PrincipalEntityType.ClrType);
        Assert.True(foreignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Empty(entityType.GetNavigations());
    }

    private static TradeScreenshot CreateScreenshot()
    {
        return CreateScreenshot(
            CapturedAtUtc,
            "2m",
            "Entry after liquidity sweep.");
    }

    private static TradeScreenshot CreateScreenshot(
        DateTimeOffset? capturedAtUtc,
        string? timeframe,
        string? description)
    {
        return TradeScreenshot.Rehydrate(
            ScreenshotId,
            TradeId,
            TradeScreenshotType.Entry,
            "future/object-store/trades/abc/chart-01",
            "nq-entry.png",
            capturedAtUtc,
            timeframe,
            description,
            CreatedAtUtc,
            UpdatedAtUtc);
    }

    private static TradeScreenshotRecord CreateScreenshotRecord()
    {
        return TradeScreenshotPersistenceMapper.ToRecord(CreateScreenshot());
    }

    private static void AssertScreenshot(
        TradeScreenshot expected,
        TradeScreenshot actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.TradeId, actual.TradeId);
        Assert.Equal(expected.Type, actual.Type);
        Assert.Equal(expected.StorageKey, actual.StorageKey);
        Assert.Equal(expected.FileName, actual.FileName);
        Assert.Equal(expected.CapturedAtUtc, actual.CapturedAtUtc);
        Assert.Equal(expected.Timeframe, actual.Timeframe);
        Assert.Equal(expected.Description, actual.Description);
        Assert.Equal(expected.CreatedAtUtc, actual.CreatedAtUtc);
        Assert.Equal(expected.UpdatedAtUtc, actual.UpdatedAtUtc);
    }

    private static void AddTradeGraph(JournalDbContext context)
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

        context.Trades.Add(new TradeRecord
        {
            Id = TradeId,
            TradingAccountId = TradingAccountId,
            InstrumentId = InstrumentId,
            PricingPointValue = 20m,
            PricingCurrency = "USD",
            StrategyId = null,
            TradingSetupId = null,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        });
    }

    private static SqliteConnection OpenConnection()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = ":memory:",
            ForeignKeys = true,
        };

        var connection = new SqliteConnection(connectionString.ToString());
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
