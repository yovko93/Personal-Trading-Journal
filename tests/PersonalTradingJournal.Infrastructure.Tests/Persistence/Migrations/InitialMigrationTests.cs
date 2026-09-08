using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Screenshots;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Migrations;

public sealed class InitialMigrationTests
{
    private const string MigrationId = "20260908122839_InitialCreate";

    private static readonly DateTimeOffset CreatedAtUtc =
        new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero)
            .AddTicks(1_234_567);

    private static readonly IReadOnlyDictionary<string, string[]> ExpectedApplicationColumns =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
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
            ["Strategies"] =
            [
                "Id", "Name", "Description", "IsActive", "CreatedAtUtc", "UpdatedAtUtc",
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
                "Id", "TradingAccountId", "InstrumentId", "PricingPointValue",
                "PricingCurrency", "StrategyId", "TradingSetupId", "CreatedAtUtc",
                "UpdatedAtUtc",
            ],
            ["TradeExecutions"] =
            [
                "Id", "TradeId", "Sequence", "ExecutedAtUtc", "Side", "Quantity", "Price",
                "Commission", "Fees", "ExternalExecutionId", "ExternalOrderId", "BrokerSymbol",
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
        };

    [Fact]
    public void InitialMigrationCreatesOnlyTheApprovedSchema()
    {
        RunWithMigratedDatabase((_, options) =>
        {
            using var context = new JournalDbContext(options);

            Assert.Equal([MigrationId], context.Database.GetAppliedMigrations());

            var connection = (SqliteConnection)context.Database.GetDbConnection();
            connection.Open();

            string[] expectedTables = ExpectedApplicationColumns.Keys
                .Append("__EFMigrationsHistory")
                .Append("__EFMigrationsLock")
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(expectedTables, ReadTableNames(connection));
            Assert.Equal(1L, ReadRowCount(connection, "__EFMigrationsHistory"));
            Assert.Equal(0L, ReadRowCount(connection, "__EFMigrationsLock"));

            foreach ((string tableName, string[] expectedColumns) in ExpectedApplicationColumns)
            {
                IReadOnlyList<ColumnDefinition> columns = ReadColumns(connection, tableName);
                Assert.Equal(expectedColumns, columns.Select(column => column.Name));

                ColumnDefinition id = Assert.Single(columns, column => column.Name == "Id");
                Assert.Equal("TEXT", id.StoreType);
                Assert.True(id.IsPrimaryKey);
                Assert.Null(id.DefaultValue);
                Assert.Equal(0L, ReadRowCount(connection, tableName));
            }

            Assert.DoesNotContain(
                "AUTOINCREMENT",
                ReadApplicationTableSql(connection),
                StringComparison.OrdinalIgnoreCase);

            AssertTimestampStoreTypes(connection);
            AssertDecimalStoreTypes(connection);
            AssertForeignKeys(connection);
            AssertBusinessUniqueIndexes(connection);
        });
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

    private static void AssertTimestampStoreTypes(SqliteConnection connection)
    {
        (string Table, string Column)[] timestampColumns =
        [
            ("Instruments", "CreatedAtUtc"),
            ("Instruments", "UpdatedAtUtc"),
            ("TradingAccounts", "CreatedAtUtc"),
            ("TradingAccounts", "UpdatedAtUtc"),
            ("Strategies", "CreatedAtUtc"),
            ("Strategies", "UpdatedAtUtc"),
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

        Assert.Equal(8, foreignKeys.Count);
        AssertForeignKey(foreignKeys, "Trades", "TradingAccountId", "TradingAccounts", "RESTRICT");
        AssertForeignKey(foreignKeys, "Trades", "InstrumentId", "Instruments", "RESTRICT");
        AssertForeignKey(foreignKeys, "Trades", "StrategyId", "Strategies", "RESTRICT");
        AssertForeignKey(foreignKeys, "Trades", "TradingSetupId", "TradingSetups", "RESTRICT");
        AssertForeignKey(foreignKeys, "TradeExecutions", "TradeId", "Trades", "CASCADE");
        AssertForeignKey(foreignKeys, "TradeScreenshots", "TradeId", "Trades", "RESTRICT");
        AssertForeignKey(foreignKeys, "TradeMistakes", "TradeId", "Trades", "RESTRICT");
        AssertForeignKey(
            foreignKeys,
            "TradeMistakes",
            "TradingMistakeId",
            "TradingMistakes",
            "RESTRICT");

        ForeignKeyDefinition cascade = Assert.Single(
            foreignKeys,
            foreignKey => foreignKey.OnDelete == "CASCADE");
        Assert.Equal("TradeExecutions", cascade.DependentTable);
        Assert.Equal("TradeId", cascade.DependentColumn);

        Assert.False(FindColumn(connection, "Trades", "StrategyId").IsRequired);
        Assert.False(FindColumn(connection, "Trades", "TradingSetupId").IsRequired);
    }

    private static void AssertBusinessUniqueIndexes(SqliteConnection connection)
    {
        List<IndexDefinition> uniqueIndexes = ExpectedApplicationColumns.Keys
            .SelectMany(table => ReadIndexes(connection, table))
            .Where(index => index.IsUnique && index.Origin != "pk")
            .ToList();

        Assert.Equal(2, uniqueIndexes.Count);
        Assert.Contains(
            uniqueIndexes,
            index => index.Table == "TradeExecutions" &&
                index.Columns.SequenceEqual(["TradeId", "Sequence"]));
        Assert.Contains(
            uniqueIndexes,
            index => index.Table == "TradeMistakes" &&
                index.Columns.SequenceEqual(["TradeId", "TradingMistakeId"]));
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
            StrategyId = null,
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
        Action<string, DbContextOptions<JournalDbContext>> test)
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
                context.Database.Migrate();
            }

            Assert.True(File.Exists(databasePath));
            test(databasePath, options);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
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
                IsPrimaryKey: reader.GetInt64(5) == 1));
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
