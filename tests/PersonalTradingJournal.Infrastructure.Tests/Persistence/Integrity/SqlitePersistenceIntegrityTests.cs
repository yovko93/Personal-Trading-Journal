using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Common.Storage;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Screenshots;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Integrity;

public sealed class SqlitePersistenceIntegrityTests
{
    private static readonly Guid TradingAccountId =
        Guid.Parse("fb5989ca-f8da-4037-b9cf-d3284f729bc6");

    private static readonly Guid InstrumentId =
        Guid.Parse("a7cc6d32-a03e-41ad-8279-07019074c8fb");

    private static readonly Guid StrategyId =
        Guid.Parse("a38b361d-1d7b-403c-b936-d143769153e6");

    private static readonly Guid TradingSetupId =
        Guid.Parse("53aeef99-0563-4bfd-9d0d-e55f898f0c92");

    private static readonly Guid TradingMistakeId =
        Guid.Parse("0cbaf769-f394-4744-859d-2c43abb0ae1a");

    private static readonly Guid TradeId =
        Guid.Parse("06533e6e-c917-445a-b90b-d0af25a3bd2a");

    private static readonly Guid ExecutionId =
        Guid.Parse("8861ed98-b67b-4d17-8a42-e79fd560a550");

    private static readonly Guid ScreenshotId =
        Guid.Parse("b85a44a3-6dc7-47c0-83af-fda87404dac9");

    private static readonly Guid TradeMistakeId =
        Guid.Parse("08b9c150-f490-48bf-9718-754b472c62f8");

    private static readonly DateTimeOffset CreatedAtUtc =
        new DateTimeOffset(2026, 9, 10, 14, 0, 0, TimeSpan.Zero)
            .AddTicks(1_234_567);

    [Fact]
    public void ModelContainsExactlyExpectedRecordsWithDomainOwnedIdentifiers()
    {
        using JournalDbContext context = CreateModelContext();
        Type[] expectedRecordTypes =
        [
            typeof(InstrumentRecord),
            typeof(TradingAccountRecord),
            typeof(StrategyRecord),
            typeof(TradingSetupRecord),
            typeof(TradingMistakeRecord),
            typeof(TradeRecord),
            typeof(TradeExecutionRecord),
            typeof(TradeScreenshotRecord),
            typeof(TradeMistakeRecord),
        ];
        List<IEntityType> entityTypes = context.Model.GetEntityTypes().ToList();

        Assert.Equal(
            expectedRecordTypes.OrderBy(type => type.FullName),
            entityTypes.Select(type => type.ClrType).OrderBy(type => type.FullName));

        foreach (IEntityType entityType in entityTypes)
        {
            IProperty idProperty = entityType.FindProperty("Id")!;
            Assert.Equal(ValueGenerated.Never, idProperty.ValueGenerated);
        }
    }

    [Fact]
    public void ModelContainsCompleteForeignKeyMatrixWithOnlyExecutionCascade()
    {
        using JournalDbContext context = CreateModelContext();
        IModel model = context.Model;

        AssertForeignKey(
            model,
            typeof(TradeRecord),
            nameof(TradeRecord.TradingAccountId),
            typeof(TradingAccountRecord),
            isRequired: true,
            DeleteBehavior.Restrict);
        AssertForeignKey(
            model,
            typeof(TradeRecord),
            nameof(TradeRecord.InstrumentId),
            typeof(InstrumentRecord),
            isRequired: true,
            DeleteBehavior.Restrict);
        AssertForeignKey(
            model,
            typeof(TradeRecord),
            nameof(TradeRecord.StrategyId),
            typeof(StrategyRecord),
            isRequired: false,
            DeleteBehavior.Restrict);
        AssertForeignKey(
            model,
            typeof(TradeRecord),
            nameof(TradeRecord.TradingSetupId),
            typeof(TradingSetupRecord),
            isRequired: false,
            DeleteBehavior.Restrict);
        AssertForeignKey(
            model,
            typeof(TradeExecutionRecord),
            nameof(TradeExecutionRecord.TradeId),
            typeof(TradeRecord),
            isRequired: true,
            DeleteBehavior.Cascade);
        AssertForeignKey(
            model,
            typeof(TradeScreenshotRecord),
            nameof(TradeScreenshotRecord.TradeId),
            typeof(TradeRecord),
            isRequired: true,
            DeleteBehavior.Restrict);
        AssertForeignKey(
            model,
            typeof(TradeMistakeRecord),
            nameof(TradeMistakeRecord.TradeId),
            typeof(TradeRecord),
            isRequired: true,
            DeleteBehavior.Restrict);
        AssertForeignKey(
            model,
            typeof(TradeMistakeRecord),
            nameof(TradeMistakeRecord.TradingMistakeId),
            typeof(TradingMistakeRecord),
            isRequired: true,
            DeleteBehavior.Restrict);

        List<IForeignKey> foreignKeys = model.GetEntityTypes()
            .SelectMany(entityType => entityType.GetForeignKeys())
            .ToList();
        Assert.Equal(8, foreignKeys.Count);

        IForeignKey cascade = Assert.Single(
            foreignKeys,
            foreignKey => foreignKey.DeleteBehavior == DeleteBehavior.Cascade);
        Assert.Equal(typeof(TradeExecutionRecord), cascade.DeclaringEntityType.ClrType);
        Assert.Equal(nameof(TradeExecutionRecord.TradeId), cascade.Properties.Single().Name);
    }

    [Fact]
    public void ModelIsNavigationFreeAndHasNoGlobalQueryFilters()
    {
        using JournalDbContext context = CreateModelContext();

        foreach (IEntityType entityType in context.Model.GetEntityTypes())
        {
            Assert.Empty(entityType.GetNavigations());
            Assert.Empty(entityType.GetSkipNavigations());
            Assert.Empty(entityType.GetDeclaredQueryFilters());
        }
    }

    [Fact]
    public void ModelContainsOnlyTheTwoIntendedBusinessUniqueIndexes()
    {
        using JournalDbContext context = CreateModelContext();
        var uniqueIndexes = context.Model.GetEntityTypes()
            .SelectMany(entityType => entityType.GetIndexes()
                .Where(index => index.IsUnique)
                .Select(index => new
                {
                    EntityType = entityType.ClrType,
                    Properties = index.Properties
                        .Select(property => property.Name)
                        .ToArray(),
                }))
            .ToList();

        Assert.Equal(2, uniqueIndexes.Count);
        Assert.Contains(
            uniqueIndexes,
            index => index.EntityType == typeof(TradeExecutionRecord) &&
                index.Properties.SequenceEqual(
                [
                    nameof(TradeExecutionRecord.TradeId),
                    nameof(TradeExecutionRecord.Sequence),
                ]));
        Assert.Contains(
            uniqueIndexes,
            index => index.EntityType == typeof(TradeMistakeRecord) &&
                index.Properties.SequenceEqual(
                [
                    nameof(TradeMistakeRecord.TradeId),
                    nameof(TradeMistakeRecord.TradingMistakeId),
                ]));
    }

    [Fact]
    public void FailedMixedTradeDeletionPreservesEntireGraphAtomically()
    {
        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddReferenceRecords(writeContext, isActive: true, includeClassifications: false);
            writeContext.Trades.Add(CreateTrade(classified: false));
            writeContext.TradeExecutions.Add(CreateExecution());
            writeContext.TradeScreenshots.Add(CreateScreenshot());
            writeContext.TradeMistakes.Add(CreateTradeMistake());
            writeContext.SaveChanges();
        }

        using (var deleteContext = new JournalDbContext(options))
        {
            TradeRecord trade = deleteContext.Trades.Single(record => record.Id == TradeId);
            deleteContext.Trades.Remove(trade);

            Assert.Throws<DbUpdateException>(() => deleteContext.SaveChanges());
        }

        using var readContext = new JournalDbContext(options);
        Assert.True(readContext.Trades.Any(record => record.Id == TradeId));
        Assert.True(readContext.TradeExecutions.Any(record => record.Id == ExecutionId));
        Assert.True(readContext.TradeScreenshots.Any(record => record.Id == ScreenshotId));
        Assert.True(readContext.TradeMistakes.Any(record => record.Id == TradeMistakeId));
    }

    [Fact]
    public void ReferencedTradeParentsCannotBeDeleted()
    {
        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddReferenceRecords(writeContext, isActive: true, includeClassifications: true);
            writeContext.Trades.Add(CreateTrade(classified: true));
            writeContext.SaveChanges();
        }

        AssertParentDeletionRejected(
            options,
            context => context.TradingAccounts.Remove(
                context.TradingAccounts.Single(record => record.Id == TradingAccountId)));
        AssertParentDeletionRejected(
            options,
            context => context.Instruments.Remove(
                context.Instruments.Single(record => record.Id == InstrumentId)));
        AssertParentDeletionRejected(
            options,
            context => context.Strategies.Remove(
                context.Strategies.Single(record => record.Id == StrategyId)));
        AssertParentDeletionRejected(
            options,
            context => context.TradingSetups.Remove(
                context.TradingSetups.Single(record => record.Id == TradingSetupId)));
    }

    [Fact]
    public void ReferencedTradingMistakeDefinitionCannotBeDeleted()
    {
        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddReferenceRecords(writeContext, isActive: true, includeClassifications: false);
            writeContext.Trades.Add(CreateTrade(classified: false));
            writeContext.TradeMistakes.Add(CreateTradeMistake());
            writeContext.SaveChanges();
        }

        using (var deleteContext = new JournalDbContext(options))
        {
            TradingMistakeRecord mistake = deleteContext.TradingMistakes
                .Single(record => record.Id == TradingMistakeId);
            deleteContext.TradingMistakes.Remove(mistake);

            Assert.Throws<DbUpdateException>(() => deleteContext.SaveChanges());
        }

        using var readContext = new JournalDbContext(options);
        Assert.True(readContext.TradingMistakes.Any(
            record => record.Id == TradingMistakeId));
        Assert.True(readContext.TradeMistakes.Any(
            record => record.Id == TradeMistakeId));
    }

    [Fact]
    public void InactiveReferenceDataRemainsAvailableToHistoricalGraph()
    {
        using SqliteConnection connection = OpenConnection();
        DbContextOptions<JournalDbContext> options = CreateOptions(connection);

        using (var writeContext = new JournalDbContext(options))
        {
            writeContext.Database.EnsureCreated();
            AddReferenceRecords(writeContext, isActive: false, includeClassifications: true);
            writeContext.Trades.Add(CreateTrade(classified: true));
            writeContext.TradeExecutions.Add(CreateExecution());
            writeContext.TradeMistakes.Add(CreateTradeMistake());
            writeContext.SaveChanges();
        }

        using var readContext = new JournalDbContext(options);
        TradeRecord trade = readContext.Trades.AsNoTracking().Single();
        TradeExecutionRecord execution = readContext.TradeExecutions
            .AsNoTracking()
            .Single();
        TradeMistakeRecord tradeMistake = readContext.TradeMistakes
            .AsNoTracking()
            .Single();

        Assert.Equal(TradingAccountId, trade.TradingAccountId);
        Assert.Equal(InstrumentId, trade.InstrumentId);
        Assert.Equal(StrategyId, trade.StrategyId);
        Assert.Equal(TradingSetupId, trade.TradingSetupId);
        Assert.Equal(TradeId, execution.TradeId);
        Assert.Equal(TradeId, tradeMistake.TradeId);
        Assert.Equal(TradingMistakeId, tradeMistake.TradingMistakeId);
        Assert.False(readContext.TradingAccounts.AsNoTracking().Single().IsActive);
        Assert.False(readContext.Instruments.AsNoTracking().Single().IsActive);
        Assert.False(readContext.Strategies.AsNoTracking().Single().IsActive);
        Assert.False(readContext.TradingSetups.AsNoTracking().Single().IsActive);
        Assert.False(readContext.TradingMistakes.AsNoTracking().Single().IsActive);
    }

    [Fact]
    public void ProductionAddPersistenceEnforcesForeignKeysAndCleansTemporaryDatabase()
    {
        string testDirectory = CreateTestDirectory();
        var applicationPaths = new TestApplicationPaths(testDirectory);

        try
        {
            var services = new ServiceCollection();
            services.AddPersistence(applicationPaths);

            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IDbContextFactory<JournalDbContext> factory =
                serviceProvider.GetRequiredService<IDbContextFactory<JournalDbContext>>();
            using JournalDbContext context = factory.CreateDbContext();
            context.Database.EnsureCreated();
            TradeExecutionRecord execution = CreateExecution();
            execution.TradeId = Guid.NewGuid();
            context.TradeExecutions.Add(execution);

            Assert.Throws<DbUpdateException>(() => context.SaveChanges());
            Assert.True(File.Exists(applicationPaths.DatabasePath));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(testDirectory, recursive: true);
        }

        Assert.False(Directory.Exists(testDirectory));
    }

    private static void AssertForeignKey(
        IModel model,
        Type dependentType,
        string foreignKeyProperty,
        Type principalType,
        bool isRequired,
        DeleteBehavior deleteBehavior)
    {
        IEntityType dependentEntity = model.FindEntityType(dependentType)!;
        IForeignKey foreignKey = Assert.Single(
            dependentEntity.GetForeignKeys(),
            candidate => candidate.Properties.Count == 1 &&
                candidate.Properties[0].Name == foreignKeyProperty);

        Assert.Equal(principalType, foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal("Id", foreignKey.PrincipalKey.Properties.Single().Name);
        Assert.Equal(isRequired, foreignKey.IsRequired);
        Assert.Equal(deleteBehavior, foreignKey.DeleteBehavior);
    }

    private static void AssertParentDeletionRejected(
        DbContextOptions<JournalDbContext> options,
        Action<JournalDbContext> deleteParent)
    {
        using (var deleteContext = new JournalDbContext(options))
        {
            deleteParent(deleteContext);
            Assert.Throws<DbUpdateException>(() => deleteContext.SaveChanges());
        }

        using var readContext = new JournalDbContext(options);
        Assert.True(readContext.Trades.Any(record => record.Id == TradeId));
    }

    private static void AddReferenceRecords(
        JournalDbContext context,
        bool isActive,
        bool includeClassifications)
    {
        context.TradingAccounts.Add(new TradingAccountRecord
        {
            Id = TradingAccountId,
            Name = "Integrity Account",
            AccountType = TradingAccountType.Demo,
            ProviderName = null,
            ExternalAccountId = null,
            Currency = "USD",
            StartingBalance = 10_000m,
            IsActive = isActive,
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
            IsActive = isActive,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        });
        context.TradingMistakes.Add(new TradingMistakeRecord
        {
            Id = TradingMistakeId,
            Name = "FOMO",
            Description = null,
            IsActive = isActive,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        });

        if (!includeClassifications)
        {
            return;
        }

        context.Strategies.Add(new StrategyRecord
        {
            Id = StrategyId,
            Name = "Integrity Strategy",
            Description = null,
            IsActive = isActive,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        });
        context.TradingSetups.Add(new TradingSetupRecord
        {
            Id = TradingSetupId,
            Name = "Integrity Setup",
            Description = null,
            IsActive = isActive,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        });
    }

    private static TradeRecord CreateTrade(bool classified)
    {
        return new TradeRecord
        {
            Id = TradeId,
            TradingAccountId = TradingAccountId,
            InstrumentId = InstrumentId,
            PricingPointValue = 20m,
            PricingCurrency = "USD",
            StrategyId = classified ? StrategyId : null,
            TradingSetupId = classified ? TradingSetupId : null,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        };
    }

    private static TradeExecutionRecord CreateExecution()
    {
        return new TradeExecutionRecord
        {
            Id = ExecutionId,
            TradeId = TradeId,
            Sequence = 1,
            ExecutedAtUtc = CreatedAtUtc.AddHours(-1),
            Side = ExecutionSide.Buy,
            Quantity = 1m,
            Price = 20_000m,
            Commission = 1m,
            Fees = 0.25m,
            ExternalExecutionId = null,
            ExternalOrderId = null,
            BrokerSymbol = "NQ",
        };
    }

    private static TradeScreenshotRecord CreateScreenshot()
    {
        return new TradeScreenshotRecord
        {
            Id = ScreenshotId,
            TradeId = TradeId,
            Type = TradeScreenshotType.Entry,
            StorageKey = "future/object-store/integrity/chart-01",
            FileName = "chart-01.png",
            CapturedAtUtc = CreatedAtUtc.AddHours(-1),
            Timeframe = "2m",
            Description = null,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        };
    }

    private static TradeMistakeRecord CreateTradeMistake()
    {
        return new TradeMistakeRecord
        {
            Id = TradeMistakeId,
            TradeId = TradeId,
            TradingMistakeId = TradingMistakeId,
            Note = null,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        };
    }

    private static JournalDbContext CreateModelContext()
    {
        var options = new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        return new JournalDbContext(options);
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

    private static string CreateTestDirectory()
    {
        string testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"{nameof(SqlitePersistenceIntegrityTests)}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        return testDirectory;
    }

    private sealed class TestApplicationPaths : IApplicationPaths
    {
        public TestApplicationPaths(string dataDirectory)
        {
            DataDirectory = dataDirectory;
            DatabasePath = Path.Combine(dataDirectory, "integrity.db");
            ScreenshotsDirectory = Path.Combine(dataDirectory, "screenshots");
            LogsDirectory = Path.Combine(dataDirectory, "logs");
            BackupsDirectory = Path.Combine(dataDirectory, "backups");
        }

        public string DataDirectory { get; }

        public string DatabasePath { get; }

        public string ScreenshotsDirectory { get; }

        public string LogsDirectory { get; }

        public string BackupsDirectory { get; }
    }
}
