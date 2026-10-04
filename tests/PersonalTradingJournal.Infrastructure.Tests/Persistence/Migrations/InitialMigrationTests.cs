using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Screenshots;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Migrations;

public sealed partial class InitialMigrationTests
{
    private const string InitialMigrationId = "20260908122839_InitialCreate";
    private const string RemoveStrategiesMigrationId = "20260914212911_RemoveStrategies";
    private const string TradeBrowseMigrationId =
        "20260917165522_AddTradeBrowseProjection";
    private const string TradovateImportMigrationId =
        "20260923074655_AddTradovateImportPersistence";
    private const string FillAllocationsMigrationId =
        "20260925214352_AddTradovateFillAllocations";
    private const string TopstepImportMigrationId = "20260928201843_AddTopstepImportPersistence";
    private const string DailyJournalsMigrationId = "20261004145757_AddDailyJournals";
    private const string DailyReviewMigrationId = "20261004165044_AddDailyJournalReviewAnswers";

    private static readonly DateTimeOffset CreatedAtUtc =
        new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero)
            .AddTicks(1_234_567);

    private static readonly IReadOnlyDictionary<string, string[]> ExpectedApplicationColumns =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["DailyJournals"] =
            [
                "Id", "TradingDate", "TradingAccountId", "Text", "IsDraft", "Revision",
                "CreatedAtUtc", "UpdatedAtUtc", "NeedsImprovement", "NextTradingDay", "WentWell",
            ],
            ["DailyJournalRevisions"] =
            [
                "JournalId", "Revision", "Text", "IsDraft", "SavedAtUtc", "NeedsImprovement", "NextTradingDay", "WentWell",
            ],
            ["Instruments"] =
            [
                "Id", "Symbol", "DisplayName", "AssetClass", "Exchange", "Currency",
                "TickSize", "TickValue", "IsActive", "CreatedAtUtc", "UpdatedAtUtc",
            ],
            ["TradingAccounts"] =
            [
                "Id", "Name", "AccountType", "ProviderName", "ExternalAccountId",
                "Currency", "StartingBalance", "IsActive", "CreatedAtUtc", "UpdatedAtUtc",
            ],
            ["TradingSetups"] =
            [
                "Id", "Name", "Description", "IsActive", "CreatedAtUtc", "UpdatedAtUtc",
            ],
            ["TradingMistakes"] =
            [
                "Id", "Name", "Description", "IsActive", "CreatedAtUtc", "UpdatedAtUtc",
            ],
            ["Trades"] =
            [
                "Id", "CreatedAtUtc", "InstrumentId", "PricingCurrency",
                "PricingPointValue", "TradingAccountId", "TradingSetupId", "UpdatedAtUtc",
            ],
            ["TradeBrowse"] =
            [
                "TradeId", "AverageEntryPrice", "AverageEntryPriceSortKey",
                "AverageExitPrice", "ClosedAtUtc", "Direction", "GrossPnL",
                "NetPnL", "NetPnLSortKey", "OpenQuantity", "OpenQuantitySortKey",
                "OpenedAtUtc", "ProjectionVersion", "Status", "TotalCosts",
            ],
            ["TradeExecutions"] =
            [
                "Id", "BrokerSymbol", "Commission", "ExecutedAtUtc",
                "ExternalExecutionId", "ExternalOrderId", "Fees", "Price",
                "Quantity", "Sequence", "Side", "TradeId",
            ],
            ["TradeScreenshots"] =
            [
                "Id", "TradeId", "Type", "StorageKey", "FileName", "CapturedAtUtc",
                "Timeframe", "Description", "CreatedAtUtc", "UpdatedAtUtc",
            ],
            ["TradeMistakes"] =
            [
                "Id", "TradeId", "TradingMistakeId", "Note", "CreatedAtUtc", "UpdatedAtUtc",
            ],
            ["TradovateImportedExecutions"] =
            [
                "TradeExecutionId", "TradeId", "TradingAccountIdAtImport",
                "BrokerSymbol", "Side", "ExternalExecutionId", "ImportedAtUtc",
                "AllocatedQuantity", "AllocationIndex", "SourceFillExecutedAtUtc", "SourceFillPrice", "SourceFillQuantity",
            ],
            ["TopstepImportedRows"] =
            [
                "Id", "TradeId", "TradingAccountIdAtImport", "SourceId", "EconomicFingerprint", "SourceRowJson",
                "PreviewFingerprint", "SourceContentSha256", "Representation", "DerivedEntryExecutionId", "DerivedExitExecutionId", "ImportedAtUtc",
            ],
        };

    [Fact]
    public void LatestMigrationsCreateOnlyTheApprovedSchema()
    {
        RunWithMigratedDatabase((_, options) =>
        {
            using var context = new JournalDbContext(options);

            Assert.Equal(
                [InitialMigrationId, RemoveStrategiesMigrationId, TradeBrowseMigrationId,
                    TradovateImportMigrationId, FillAllocationsMigrationId, TopstepImportMigrationId,
                    DailyJournalsMigrationId, DailyReviewMigrationId],
                context.Database.GetAppliedMigrations());

            var connection = (SqliteConnection)context.Database.GetDbConnection();
            connection.Open();

            string[] expectedTables = ExpectedApplicationColumns.Keys
                .Append("__EFMigrationsHistory")
                .Append("__EFMigrationsLock")
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(expectedTables, ReadTableNames(connection));
            Assert.Equal(8L, ReadRowCount(connection, "__EFMigrationsHistory"));
            Assert.Equal(0L, ReadRowCount(connection, "__EFMigrationsLock"));

            foreach ((string tableName, string[] expectedColumns) in ExpectedApplicationColumns)
            {
                IReadOnlyList<ColumnDefinition> columns = ReadColumns(connection, tableName);
                Assert.Equal(expectedColumns, columns.Select(column => column.Name));

                string[] keyNames = tableName switch
                {
                    "TradeBrowse" => ["TradeId"],
                    "TradovateImportedExecutions" => ["TradeExecutionId"],
                    "DailyJournalRevisions" => ["JournalId", "Revision"],
                    _ => ["Id"],
                };
                Assert.Equal(keyNames, columns.Where(column => column.IsPrimaryKey)
                    .Select(column => column.Name));
                foreach (string keyName in keyNames)
                {
                    ColumnDefinition key = Assert.Single(columns, column => column.Name == keyName);
                    Assert.Equal(keyName == "Revision" ? "INTEGER" : "TEXT", key.StoreType);
                    Assert.Null(key.DefaultValue);
                }
                Assert.Equal(0L, ReadRowCount(connection, tableName));
            }

            Assert.DoesNotContain(
                "AUTOINCREMENT",
                ReadApplicationTableSql(connection),
                StringComparison.OrdinalIgnoreCase);

            AssertTimestampStoreTypes(connection);
            AssertDecimalStoreTypes(connection);
            Assert.False(FindColumn(connection, "TradeExecutions", "Commission").IsRequired);
            Assert.False(FindColumn(connection, "TradeExecutions", "Fees").IsRequired);
            Assert.False(FindColumn(connection, "TradeBrowse", "TotalCosts").IsRequired);
            AssertForeignKeys(connection);
            AssertBusinessUniqueIndexes(connection);
        });
    }

    [Fact]
    public void DailyJournalMigrationUpAndDownPreserveExistingTradeDataAndSchema()
    {
        RunWithMigratedDatabase((_, options) =>
        {
            Guid tradeId = Guid.NewGuid();
            using (var seedContext = new JournalDbContext(options))
            {
                AddTradeGraph(seedContext, tradeId);
                TradeExecutionRecord execution = CreateExecution(Guid.NewGuid(), tradeId, sequence: 1);
                execution.Quantity = 0.12345678m;
                execution.Price = -37.63m;
                execution.Commission = null;
                execution.Fees = 0.0000000001m;
                seedContext.TradeExecutions.Add(execution);
                seedContext.SaveChanges();
            }

            using var context = new JournalDbContext(options);
            TradeRecord expectedTrade = context.Trades.AsNoTracking().Single();
            TradeExecutionRecord expectedExecution = context.TradeExecutions.AsNoTracking().Single();
            TradingAccountRecord expectedAccount = context.TradingAccounts.AsNoTracking().Single();
            InstrumentRecord expectedInstrument = context.Instruments.AsNoTracking().Single();
            var connection = (SqliteConnection)context.Database.GetDbConnection();
            connection.Open();
            string[] previousTables = ReadTableNames(connection);
            string[] previousSchema = ReadNonJournalSchema(connection);

            context.Database.Migrate();

            Assert.Equal(DailyReviewMigrationId, context.Database.GetAppliedMigrations().Last());
            Assert.Equal(previousTables.Concat(["DailyJournals", "DailyJournalRevisions"])
                .OrderBy(name => name, StringComparer.Ordinal), ReadTableNames(connection));
            Assert.Equal(previousSchema, ReadNonJournalSchema(connection));
            AssertExistingTradeData();

            Guid journalId = Guid.NewGuid();
            context.DailyJournals.Add(new DailyJournalRecord
            {
                Id = journalId,
                TradingDate = new DateOnly(2026, 10, 4),
                TradingAccountId = expectedAccount.Id,
                Text = "A journal added after upgrading an existing database.",
                IsDraft = true,
                Revision = 1,
                CreatedAtUtc = CreatedAtUtc,
                UpdatedAtUtc = CreatedAtUtc,
            });
            context.DailyJournalRevisions.Add(new DailyJournalRevisionRecord
            {
                JournalId = journalId,
                Revision = 1,
                Text = "A journal added after upgrading an existing database.",
                IsDraft = true,
                SavedAtUtc = CreatedAtUtc,
            });
            context.SaveChanges();

            context.GetService<IMigrator>().Migrate(TopstepImportMigrationId);

            Assert.Equal(TopstepImportMigrationId, context.Database.GetAppliedMigrations().Last());
            Assert.Equal(previousTables, ReadTableNames(connection));
            Assert.Equal(previousSchema, ReadNonJournalSchema(connection));
            AssertExistingTradeData();

            void AssertExistingTradeData()
            {
                using var verification = new JournalDbContext(options);
                Assert.Equivalent(expectedTrade, verification.Trades.AsNoTracking().Single(), strict: true);
                Assert.Equivalent(expectedExecution, verification.TradeExecutions.AsNoTracking().Single(), strict: true);
                Assert.Equivalent(expectedAccount, verification.TradingAccounts.AsNoTracking().Single(), strict: true);
                Assert.Equivalent(expectedInstrument, verification.Instruments.AsNoTracking().Single(), strict: true);
            }
        }, TopstepImportMigrationId);
    }

    [Fact]
    public void MigratedSchemaRejectsMissingTradeAndDuplicateExecutionSequence()
    {
        RunWithMigratedDatabase((_, options) =>
        {
            using (var missingParentContext = new JournalDbContext(options))
            {
                missingParentContext.TradeExecutions.Add(
                    CreateExecution(Guid.NewGuid(), Guid.NewGuid(), sequence: 1));

                Assert.Throws<DbUpdateException>(() => missingParentContext.SaveChanges());
            }

            Guid tradeId = Guid.NewGuid();
            using (var writeContext = new JournalDbContext(options))
            {
                AddTradeGraph(writeContext, tradeId);
                writeContext.TradeExecutions.Add(
                    CreateExecution(Guid.NewGuid(), tradeId, sequence: 1));
                writeContext.SaveChanges();
            }

            using var duplicateContext = new JournalDbContext(options);
            duplicateContext.TradeExecutions.Add(
                CreateExecution(Guid.NewGuid(), tradeId, sequence: 1));

            Assert.Throws<DbUpdateException>(() => duplicateContext.SaveChanges());
        });
    }

    [Fact]
    public void MigratedSchemaCascadesTradeDeletionToExecutions()
    {
        RunWithMigratedDatabase((_, options) =>
        {
            Guid tradeId = Guid.NewGuid();
            Guid executionId = Guid.NewGuid();

            using (var writeContext = new JournalDbContext(options))
            {
                AddTradeGraph(writeContext, tradeId);
                writeContext.TradeExecutions.Add(
                    CreateExecution(executionId, tradeId, sequence: 1));
                writeContext.SaveChanges();
            }

            using (var deleteContext = new JournalDbContext(options))
            {
                TradeRecord trade = deleteContext.Trades.Single(record => record.Id == tradeId);
                deleteContext.Trades.Remove(trade);
                deleteContext.SaveChanges();
            }

            using var readContext = new JournalDbContext(options);
            Assert.False(readContext.Trades.Any(record => record.Id == tradeId));
            Assert.False(readContext.TradeExecutions.Any(record => record.Id == executionId));
        });
    }

    [Fact]
    public void MigratedSchemaRestrictsTradeDeletionWhenScreenshotExists()
    {
        RunWithMigratedDatabase((_, options) =>
        {
            Guid tradeId = Guid.NewGuid();
            Guid screenshotId = Guid.NewGuid();

            using (var writeContext = new JournalDbContext(options))
            {
                AddTradeGraph(writeContext, tradeId);
                writeContext.TradeScreenshots.Add(new TradeScreenshotRecord
                {
                    Id = screenshotId,
                    TradeId = tradeId,
                    Type = TradeScreenshotType.Entry,
                    StorageKey = "migration-tests/chart-01",
                    FileName = "chart-01.png",
                    CapturedAtUtc = CreatedAtUtc,
                    Timeframe = "2m",
                    Description = null,
                    CreatedAtUtc = CreatedAtUtc,
                    UpdatedAtUtc = CreatedAtUtc,
                });
                writeContext.SaveChanges();
            }

            using (var deleteContext = new JournalDbContext(options))
            {
                TradeRecord trade = deleteContext.Trades.Single(record => record.Id == tradeId);
                deleteContext.Trades.Remove(trade);

                Assert.Throws<DbUpdateException>(() => deleteContext.SaveChanges());
            }

            using var readContext = new JournalDbContext(options);
            Assert.True(readContext.Trades.Any(record => record.Id == tradeId));
            Assert.True(readContext.TradeScreenshots.Any(record => record.Id == screenshotId));
        });
    }

    [Fact]
    public void MigratedSchemaPreservesTimestampQueriesAndDecimalExactness()
    {
        RunWithMigratedDatabase((_, options) =>
        {
            Guid tradeId = Guid.NewGuid();
            DateTimeOffset firstTimestamp = CreatedAtUtc.AddTicks(1);
            DateTimeOffset secondTimestamp = CreatedAtUtc.AddTicks(2);
            DateTimeOffset thirdTimestamp = CreatedAtUtc.AddTicks(3);
            Guid exactExecutionId = Guid.NewGuid();

            using (var writeContext = new JournalDbContext(options))
            {
                AddTradeGraph(writeContext, tradeId);

                TradeExecutionRecord third = CreateExecution(
                    Guid.NewGuid(),
                    tradeId,
                    sequence: 3);
                third.ExecutedAtUtc = thirdTimestamp;

                TradeExecutionRecord first = CreateExecution(
                    Guid.NewGuid(),
                    tradeId,
                    sequence: 1);
                first.ExecutedAtUtc = firstTimestamp;

                TradeExecutionRecord exact = CreateExecution(
                    exactExecutionId,
                    tradeId,
                    sequence: 2);
                exact.ExecutedAtUtc = secondTimestamp;
                exact.Quantity = 0.12345678m;
                exact.Price = -37.63m;
                exact.Commission = 0.0123456789m;
                exact.Fees = 0.0000000001m;

                writeContext.TradeExecutions.AddRange(third, first, exact);
                writeContext.SaveChanges();
            }

            using var readContext = new JournalDbContext(options);
            List<DateTimeOffset> orderedTimestamps = readContext.TradeExecutions
                .AsNoTracking()
                .OrderBy(record => record.ExecutedAtUtc)
                .Select(record => record.ExecutedAtUtc)
                .ToList();
            List<DateTimeOffset> rangeTimestamps = readContext.TradeExecutions
                .AsNoTracking()
                .Where(record =>
                    record.ExecutedAtUtc >= secondTimestamp &&
                    record.ExecutedAtUtc <= thirdTimestamp)
                .OrderBy(record => record.ExecutedAtUtc)
                .Select(record => record.ExecutedAtUtc)
                .ToList();
            TradeExecutionRecord roundTripped = readContext.TradeExecutions
                .AsNoTracking()
                .Single(record => record.Id == exactExecutionId);

            Assert.Equal(
                [firstTimestamp, secondTimestamp, thirdTimestamp],
                orderedTimestamps);
            Assert.Equal([secondTimestamp, thirdTimestamp], rangeTimestamps);
            Assert.Equal(0.12345678m, roundTripped.Quantity);
            Assert.Equal(-37.63m, roundTripped.Price);
            Assert.Equal(0.0123456789m, roundTripped.Commission);
            Assert.Equal(0.0000000001m, roundTripped.Fees);
        });
    }

    [Fact]
    public void FillAllocationMigrationPreservesLegacyIdentityWithoutInventingEconomics()
    {
        RunWithMigratedDatabase((_, options) =>
        {
            Guid tradeId = Guid.NewGuid();
            Guid originalExecutionId = Guid.NewGuid();
            using (var context = new JournalDbContext(options))
            {
                AddTradeGraph(context, tradeId);
                context.SaveChanges();
                Guid accountId = context.Trades.Single().TradingAccountId;
                // Old ledger identities survive even when subsequent edits replaced executions.
                context.Database.ExecuteSqlInterpolated($"""
                    INSERT INTO TradovateImportedExecutions
                        (TradeExecutionId, TradeId, TradingAccountIdAtImport, BrokerSymbol,
                         Side, ExternalExecutionId, ImportedAtUtc)
                    VALUES ({originalExecutionId}, {tradeId}, {accountId}, {"MNQU6"},
                            {(int)ExecutionSide.Sell}, {"SYNTHETIC-LEGACY"}, {CreatedAtUtc.UtcDateTime})
                    """);
                context.Database.Migrate();
            }

            using var read = new JournalDbContext(options);
            TradovateImportedExecutionRecord identity = read.TradovateImportedExecutions.Single();
            Assert.Equal(originalExecutionId, identity.TradeExecutionId);
            Assert.Equal(tradeId, identity.TradeId);
            Assert.Equal("SYNTHETIC-LEGACY", identity.ExternalExecutionId);
            Assert.Equal(0, identity.AllocationIndex);
            Assert.Null(identity.SourceFillQuantity);
            Assert.Null(identity.AllocatedQuantity);
            Assert.Null(identity.SourceFillPrice);
            Assert.Null(identity.SourceFillExecutedAtUtc);
            Assert.Empty(read.TradeExecutions);
        }, TradovateImportMigrationId);
    }

    [Fact]
    public void RemoveStrategiesMigrationPreservesNonStrategyTradeData()
    {
        RunWithMigratedDatabase((_, options) =>
        {
            Guid tradeId = Guid.NewGuid();
            Guid setupId = Guid.NewGuid();
            Guid executionId = Guid.NewGuid();
            Guid screenshotId = Guid.NewGuid();
            Guid mistakeId = Guid.NewGuid();
            Guid tradeMistakeId = Guid.NewGuid();

            using (var writeContext = new JournalDbContext(options))
            {
                AddTradeGraph(writeContext, tradeId);
                writeContext.TradingSetups.Add(new TradingSetupRecord
                {
                    Id = setupId,
                    Name = "Migration Setup",
                    Description = "Preserved setup",
                    IsActive = true,
                    CreatedAtUtc = CreatedAtUtc,
                    UpdatedAtUtc = CreatedAtUtc,
                });
                writeContext.Trades.Local.Single(record => record.Id == tradeId)
                    .TradingSetupId = setupId;
                writeContext.TradeExecutions.Add(
                    CreateExecution(executionId, tradeId, sequence: 1));
                writeContext.TradeScreenshots.Add(new TradeScreenshotRecord
                {
                    Id = screenshotId,
                    TradeId = tradeId,
                    Type = TradeScreenshotType.Entry,
                    StorageKey = "migration-tests/preserved-chart",
                    FileName = "preserved-chart.png",
                    CapturedAtUtc = CreatedAtUtc,
                    Timeframe = "5m",
                    Description = null,
                    CreatedAtUtc = CreatedAtUtc,
                    UpdatedAtUtc = CreatedAtUtc,
                });
                writeContext.TradingMistakes.Add(new TradingMistakeRecord
                {
                    Id = mistakeId,
                    Name = "Migration Mistake",
                    Description = null,
                    IsActive = true,
                    CreatedAtUtc = CreatedAtUtc,
                    UpdatedAtUtc = CreatedAtUtc,
                });
                writeContext.TradeMistakes.Add(new TradeMistakeRecord
                {
                    Id = tradeMistakeId,
                    TradeId = tradeId,
                    TradingMistakeId = mistakeId,
                    Note = "Preserved association",
                    CreatedAtUtc = CreatedAtUtc,
                    UpdatedAtUtc = CreatedAtUtc,
                });
                writeContext.SaveChanges();
                writeContext.Database.Migrate();
            }

            using var readContext = new JournalDbContext(options);
            Assert.Equal(
                [InitialMigrationId, RemoveStrategiesMigrationId, TradeBrowseMigrationId,
                    TradovateImportMigrationId, FillAllocationsMigrationId, TopstepImportMigrationId,
                    DailyJournalsMigrationId, DailyReviewMigrationId],
                readContext.Database.GetAppliedMigrations());

            var connection = (SqliteConnection)readContext.Database.GetDbConnection();
            connection.Open();
            Assert.DoesNotContain("Strategies", ReadTableNames(connection));
            Assert.DoesNotContain(
                ReadColumns(connection, "Trades"),
                column => column.Name == "StrategyId");
            Assert.Contains(
                ReadColumns(connection, "Trades"),
                column => column.Name == "TradingSetupId");
            Assert.Contains("TradingSetups", ReadTableNames(connection));
            Assert.Contains("TradingMistakes", ReadTableNames(connection));
            Assert.Contains("TradeMistakes", ReadTableNames(connection));
            Assert.Contains("TradeExecutions", ReadTableNames(connection));
            Assert.Contains("TradeScreenshots", ReadTableNames(connection));

            TradeRecord trade = readContext.Trades.AsNoTracking().Single();
            Assert.Equal(tradeId, trade.Id);
            Assert.Equal(setupId, trade.TradingSetupId);
            Assert.True(readContext.TradingSetups.Any(record => record.Id == setupId));
            Assert.True(readContext.TradeExecutions.Any(record => record.Id == executionId));
            Assert.True(readContext.TradeScreenshots.Any(record => record.Id == screenshotId));
            Assert.True(readContext.TradingMistakes.Any(record => record.Id == mistakeId));
            Assert.True(readContext.TradeMistakes.Any(record => record.Id == tradeMistakeId));
        }, InitialMigrationId);
    }

    [Fact]
    public void RemoveStrategiesMigrationDownRestoresHistoricalSchema()
    {
        RunWithMigratedDatabase((_, options) =>
        {
            using (var context = new JournalDbContext(options))
            {
                context.GetService<IMigrator>().Migrate(InitialMigrationId);
            }

            using var inspectionContext = new JournalDbContext(options);
            var connection = (SqliteConnection)inspectionContext.Database.GetDbConnection();
            connection.Open();

            Assert.Contains("Strategies", ReadTableNames(connection));
            Assert.Contains(
                ReadColumns(connection, "Trades"),
                column => column.Name == "StrategyId" && !column.IsRequired);
            Assert.Contains(
                ReadIndexes(connection, "Trades"),
                index => index.Columns.SequenceEqual(["StrategyId"]));

            List<ForeignKeyDefinition> foreignKeys = ReadForeignKeys(connection, "Trades").ToList();
            AssertForeignKey(
                foreignKeys,
                "Trades",
                "StrategyId",
                "Strategies",
                "RESTRICT");
        });
    }

    private static void AssertTimestampStoreTypes(SqliteConnection connection)
    {
        (string Table, string Column)[] timestampColumns =
        [
            ("Instruments", "CreatedAtUtc"),
            ("Instruments", "UpdatedAtUtc"),
            ("TradingAccounts", "CreatedAtUtc"),
            ("TradingAccounts", "UpdatedAtUtc"),
            ("TradingSetups", "CreatedAtUtc"),
            ("TradingSetups", "UpdatedAtUtc"),
            ("TradingMistakes", "CreatedAtUtc"),
            ("TradingMistakes", "UpdatedAtUtc"),
            ("Trades", "CreatedAtUtc"),
            ("Trades", "UpdatedAtUtc"),
            ("TradeExecutions", "ExecutedAtUtc"),
            ("TradeScreenshots", "CapturedAtUtc"),
            ("TradeScreenshots", "CreatedAtUtc"),
            ("TradeScreenshots", "UpdatedAtUtc"),
            ("TradeMistakes", "CreatedAtUtc"),
            ("TradeMistakes", "UpdatedAtUtc"),
            ("TradeBrowse", "OpenedAtUtc"),
            ("TradeBrowse", "ClosedAtUtc"),
            ("TradovateImportedExecutions", "ImportedAtUtc"),
            ("TopstepImportedRows", "ImportedAtUtc"),
            ("DailyJournals", "CreatedAtUtc"),
            ("DailyJournals", "UpdatedAtUtc"),
            ("DailyJournalRevisions", "SavedAtUtc"),
        ];

        foreach ((string table, string column) in timestampColumns)
        {
            ColumnDefinition definition = Assert.Single(
                ReadColumns(connection, table),
                candidate => candidate.Name == column);
            Assert.Equal("TEXT", definition.StoreType);
        }
    }

    private static void AssertDecimalStoreTypes(SqliteConnection connection)
    {
        (string Table, string Column)[] decimalColumns =
        [
            ("Instruments", "TickSize"),
            ("Instruments", "TickValue"),
            ("TradingAccounts", "StartingBalance"),
            ("Trades", "PricingPointValue"),
            ("TradeExecutions", "Quantity"),
            ("TradeExecutions", "Price"),
            ("TradeExecutions", "Commission"),
            ("TradeExecutions", "Fees"),
            ("TradeBrowse", "OpenQuantity"),
            ("TradeBrowse", "AverageEntryPrice"),
            ("TradeBrowse", "AverageExitPrice"),
            ("TradeBrowse", "TotalCosts"),
            ("TradeBrowse", "GrossPnL"),
            ("TradeBrowse", "NetPnL"),
        ];

        foreach ((string table, string column) in decimalColumns)
        {
            ColumnDefinition definition = Assert.Single(
                ReadColumns(connection, table),
                candidate => candidate.Name == column);
            Assert.Equal("TEXT", definition.StoreType);
        }
    }

    private static void AssertForeignKeys(SqliteConnection connection)
    {
        List<ForeignKeyDefinition> foreignKeys = ExpectedApplicationColumns.Keys
            .SelectMany(table => ReadForeignKeys(connection, table))
            .ToList();

        Assert.Equal(12, foreignKeys.Count);
        AssertForeignKey(foreignKeys, "DailyJournals", "TradingAccountId", "TradingAccounts", "RESTRICT");
        AssertForeignKey(foreignKeys, "DailyJournalRevisions", "JournalId", "DailyJournals", "RESTRICT");
        AssertForeignKey(foreignKeys, "TopstepImportedRows", "TradeId", "Trades", "CASCADE");
        AssertForeignKey(foreignKeys, "Trades", "TradingAccountId", "TradingAccounts", "RESTRICT");
        AssertForeignKey(foreignKeys, "Trades", "InstrumentId", "Instruments", "RESTRICT");
        AssertForeignKey(foreignKeys, "Trades", "TradingSetupId", "TradingSetups", "RESTRICT");
        AssertForeignKey(foreignKeys, "TradeExecutions", "TradeId", "Trades", "CASCADE");
        AssertForeignKey(foreignKeys, "TradeBrowse", "TradeId", "Trades", "CASCADE");
        AssertForeignKey(foreignKeys, "TradeScreenshots", "TradeId", "Trades", "RESTRICT");
        AssertForeignKey(foreignKeys, "TradeMistakes", "TradeId", "Trades", "RESTRICT");
        AssertForeignKey(
            foreignKeys,
            "TradovateImportedExecutions",
            "TradeId",
            "Trades",
            "CASCADE");
        AssertForeignKey(
            foreignKeys,
            "TradeMistakes",
            "TradingMistakeId",
            "TradingMistakes",
            "RESTRICT");

        ForeignKeyDefinition[] cascades = foreignKeys
            .Where(foreignKey => foreignKey.OnDelete == "CASCADE")
            .ToArray();
        Assert.Equal(4, cascades.Length);
        Assert.Contains(cascades, cascade =>
            cascade.DependentTable == "TradeExecutions" &&
            cascade.DependentColumn == "TradeId");
        Assert.Contains(cascades, cascade =>
            cascade.DependentTable == "TradeBrowse" &&
            cascade.DependentColumn == "TradeId");
        Assert.Contains(cascades, cascade =>
            cascade.DependentTable == "TradovateImportedExecutions" &&
            cascade.DependentColumn == "TradeId");

        Assert.False(FindColumn(connection, "Trades", "TradingSetupId").IsRequired);
    }

    private static void AssertBusinessUniqueIndexes(SqliteConnection connection)
    {
        List<IndexDefinition> uniqueIndexes = ExpectedApplicationColumns.Keys
            .SelectMany(table => ReadIndexes(connection, table))
            .Where(index => index.IsUnique && index.Origin != "pk")
            .ToList();

        Assert.Equal(7, uniqueIndexes.Count);
        Assert.Contains(uniqueIndexes, index => index.Table == "DailyJournals" &&
            index.Columns.SequenceEqual(["TradingDate", "TradingAccountId"]));
        Assert.Contains(uniqueIndexes, index => index.Table == "DailyJournals" &&
            index.Columns.SequenceEqual(["TradingDate"]));
        Assert.Contains(uniqueIndexes, index => index.Table == "TopstepImportedRows" && index.Columns.SequenceEqual(["TradingAccountIdAtImport", "SourceId"]));
        Assert.Contains(uniqueIndexes, index => index.Table == "TopstepImportedRows" && index.Columns.SequenceEqual(["TradeId"]));
        Assert.Contains(
            uniqueIndexes,
            index => index.Table == "TradeExecutions" &&
                index.Columns.SequenceEqual(["TradeId", "Sequence"]));
        Assert.Contains(
            uniqueIndexes,
            index => index.Table == "TradeMistakes" &&
                index.Columns.SequenceEqual(["TradeId", "TradingMistakeId"]));
        Assert.Contains(
            uniqueIndexes,
            index => index.Table == "TradovateImportedExecutions" &&
                index.Columns.SequenceEqual(
                [
                    "TradingAccountIdAtImport",
                    "BrokerSymbol",
                    "Side",
                    "ExternalExecutionId",
                    "AllocationIndex",
                ]));
    }

    private static void AssertForeignKey(
        IEnumerable<ForeignKeyDefinition> foreignKeys,
        string dependentTable,
        string dependentColumn,
        string principalTable,
        string onDelete)
    {
        ForeignKeyDefinition foreignKey = Assert.Single(
            foreignKeys,
            candidate => candidate.DependentTable == dependentTable &&
                candidate.DependentColumn == dependentColumn);

        Assert.Equal(principalTable, foreignKey.PrincipalTable);
        Assert.Equal("Id", foreignKey.PrincipalColumn);
        Assert.Equal(onDelete, foreignKey.OnDelete);
    }

    private static void AddTradeGraph(JournalDbContext context, Guid tradeId)
    {
        Guid tradingAccountId = Guid.NewGuid();
        Guid instrumentId = Guid.NewGuid();

        context.TradingAccounts.Add(new TradingAccountRecord
        {
            Id = tradingAccountId,
            Name = "Migration Test Account",
            AccountType = TradingAccountType.Demo,
            ProviderName = null,
            ExternalAccountId = null,
            Currency = "USD",
            StartingBalance = 10_000m,
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        });
        context.Instruments.Add(new InstrumentRecord
        {
            Id = instrumentId,
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
            Id = tradeId,
            TradingAccountId = tradingAccountId,
            InstrumentId = instrumentId,
            PricingPointValue = 20m,
            PricingCurrency = "USD",
            TradingSetupId = null,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = CreatedAtUtc,
        });
    }

    private static TradeExecutionRecord CreateExecution(
        Guid executionId,
        Guid tradeId,
        int sequence)
    {
        return new TradeExecutionRecord
        {
            Id = executionId,
            TradeId = tradeId,
            Sequence = sequence,
            ExecutedAtUtc = CreatedAtUtc,
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

    private static void RunWithMigratedDatabase(
        Action<string, DbContextOptions<JournalDbContext>> test,
        string? targetMigration = null)
    {
        string testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"{nameof(InitialMigrationTests)}-{Guid.NewGuid():N}");
        string databasePath = Path.Combine(testDirectory, "migration.db");
        Directory.CreateDirectory(testDirectory);

        try
        {
            Assert.False(File.Exists(databasePath));

            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                ForeignKeys = true,
            }.ToString();
            DbContextOptions<JournalDbContext> options =
                new DbContextOptionsBuilder<JournalDbContext>()
                    .UseSqlite(connectionString)
                    .Options;

            using (var context = new JournalDbContext(options))
            {
                context.GetService<IMigrator>().Migrate(targetMigration);
            }

            Assert.True(File.Exists(databasePath));
            test(databasePath, options);
        }
        finally
        {
            SqliteTestPoolCleanup.ClearPersistencePools(databasePath);
            Directory.Delete(testDirectory, recursive: true);
            Assert.False(Directory.Exists(testDirectory));
        }
    }

    private static string[] ReadTableNames(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT name FROM sqlite_master " +
            "WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";

        using SqliteDataReader reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names.ToArray();
    }

    private static string[] ReadNonJournalSchema(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT sql FROM sqlite_master
            WHERE type IN ('table', 'index') AND name NOT LIKE 'sqlite_%'
                AND tbl_name NOT IN ('__EFMigrationsHistory', '__EFMigrationsLock',
                    'DailyJournals', 'DailyJournalRevisions')
            ORDER BY type, name;
            """;

        using SqliteDataReader reader = command.ExecuteReader();
        var definitions = new List<string>();
        while (reader.Read())
        {
            definitions.Add(reader.GetString(0));
        }

        return definitions.ToArray();
    }

    private static IReadOnlyList<ColumnDefinition> ReadColumns(
        SqliteConnection connection,
        string tableName)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({QuoteIdentifier(tableName)});";

        using SqliteDataReader reader = command.ExecuteReader();
        var columns = new List<ColumnDefinition>();
        while (reader.Read())
        {
            columns.Add(new ColumnDefinition(
                reader.GetString(1),
                reader.GetString(2),
                IsRequired: reader.GetInt64(3) == 1,
                DefaultValue: reader.IsDBNull(4) ? null : reader.GetValue(4).ToString(),
                IsPrimaryKey: reader.GetInt64(5) > 0));
        }

        return columns;
    }

    private static IReadOnlyList<ForeignKeyDefinition> ReadForeignKeys(
        SqliteConnection connection,
        string tableName)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"PRAGMA foreign_key_list({QuoteIdentifier(tableName)});";

        using SqliteDataReader reader = command.ExecuteReader();
        var foreignKeys = new List<ForeignKeyDefinition>();
        while (reader.Read())
        {
            foreignKeys.Add(new ForeignKeyDefinition(
                tableName,
                reader.GetString(3),
                reader.GetString(2),
                reader.GetString(4),
                reader.GetString(6)));
        }

        return foreignKeys;
    }

    private static IReadOnlyList<IndexDefinition> ReadIndexes(
        SqliteConnection connection,
        string tableName)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"PRAGMA index_list({QuoteIdentifier(tableName)});";

        using SqliteDataReader reader = command.ExecuteReader();
        var indexRows = new List<(string Name, bool IsUnique, string Origin)>();
        while (reader.Read())
        {
            indexRows.Add((
                reader.GetString(1),
                reader.GetInt64(2) == 1,
                reader.GetString(3)));
        }

        var indexes = new List<IndexDefinition>();
        foreach ((string name, bool isUnique, string origin) in indexRows)
        {
            using SqliteCommand columnsCommand = connection.CreateCommand();
            columnsCommand.CommandText = $"PRAGMA index_info({QuoteIdentifier(name)});";

            using SqliteDataReader columnsReader = columnsCommand.ExecuteReader();
            var columns = new List<string>();
            while (columnsReader.Read())
            {
                columns.Add(columnsReader.GetString(2));
            }

            indexes.Add(new IndexDefinition(tableName, isUnique, origin, columns));
        }

        return indexes;
    }

    private static string ReadApplicationTableSql(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT group_concat(sql, ' ') FROM sqlite_master " +
            "WHERE type = 'table' AND name NOT LIKE 'sqlite_%' " +
            "AND name <> '__EFMigrationsHistory';";

        return (string)command.ExecuteScalar()!;
    }

    private static long ReadRowCount(SqliteConnection connection, string tableName)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {QuoteIdentifier(tableName)};";
        return (long)command.ExecuteScalar()!;
    }

    private static ColumnDefinition FindColumn(
        SqliteConnection connection,
        string tableName,
        string columnName)
    {
        return Assert.Single(
            ReadColumns(connection, tableName),
            column => column.Name == columnName);
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private sealed record ColumnDefinition(
        string Name,
        string StoreType,
        bool IsRequired,
        string? DefaultValue,
        bool IsPrimaryKey);

    private sealed record ForeignKeyDefinition(
        string DependentTable,
        string DependentColumn,
        string PrincipalTable,
        string PrincipalColumn,
        string OnDelete);

    private sealed record IndexDefinition(
        string Table,
        bool IsUnique,
        string Origin,
        IReadOnlyList<string> Columns);
}
