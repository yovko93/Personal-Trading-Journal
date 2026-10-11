using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Backups;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Initialization;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Storage;
using PersonalTradingJournal.Infrastructure.Tests.Persistence;

namespace PersonalTradingJournal.Infrastructure.Tests.Backups;

public sealed class SqliteDatabaseSnapshotTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = new(2026, 10, 10);

    [Fact]
    public async Task CommittedWalSnapshotIsStandaloneAndPreservesTradeJournalAndAiIdentity()
    {
        await using var fixture = await Fixture.Create();
        using var keeper = fixture.Open();
        Execute(keeper, "PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0; PRAGMA wal_checkpoint(TRUNCATE);");
        var account = new TradingAccount("Synthetic", TradingAccountType.Personal, null, null, "USD", 100m, Now);
        var instrument = new Instrument("SYN", "Synthetic", AssetClass.Futures, "TEST", "USD", .25m, .25m, Now);
        await fixture.Services.GetRequiredService<ITradingAccountStore>().AddAsync(account);
        await fixture.Services.GetRequiredService<IInstrumentStore>().AddAsync(instrument);
        var journals = fixture.Services.GetRequiredService<IDailyJournalRepository>();
        var journal = (await journals.CreateAsync(new(Day, account.Id, "Synthetic text"))).Journal!.Entry;
        await journals.UpdateAsync(new(journal.Id, journal.Revision, "Synthetic revision"));
        Guid tradeId = Guid.NewGuid();
        var trade = Trade.Rehydrate(tradeId, account.Id, instrument.Id, new(1, "USD"), null,
            [new(tradeId, 1, Now.AddHours(-1), ExecutionSide.Buy, 1, 100, null, 0, null, null, null),
             new(tradeId, 2, Now, ExecutionSide.Sell, 1, 110, 0, 0, null, null, null)], Now, Now);
        await using (var db = fixture.Context())
        {
            db.Trades.Add(TradePersistenceMapper.ToRecord(trade));
            db.TradeExecutions.AddRange(trade.Executions.Select(TradeExecutionPersistenceMapper.ToRecord));
            db.TradeBrowse.Add(TradeBrowsePersistenceMapper.ToRecord(trade));
            await db.SaveChangesAsync();
        }
        var packet = CoachingEvidencePacketBuilder.Build(await fixture.Services.GetRequiredService<IDailyReviewEvidenceReader>().GetAsync(new(Day))).Packet!;
        var snapshot = CoachingAnalysisSnapshot.Create(packet, new(CoachingGenerationStatus.Success,
            new(packet.ContractVersion, packet.PacketId, new("Synthetic summary", ["calculated:day"]), [], [], [], []),
            new("Fake", "test-model", "synthetic-client", null, null, new(1, 0, 1, 2)), "Unused"), Now);
        var saved = await fixture.Services.GetRequiredService<ICoachingAnalysisRepository>().SaveAsync(snapshot);
        Assert.True(new FileInfo(fixture.Paths.DatabasePath + "-wal").Length > 0);

        var service = fixture.Services.GetRequiredService<IDatabaseSnapshotService>();
        var first = await service.CreateAsync(fixture.Stage);
        Assert.Equal(DatabaseSnapshotStatus.Success, first.Status);
        var output = first.Snapshot!;
        Assert.False(first.CleanupFailed);
        Assert.Single(Directory.GetFiles(output.DirectoryPath));
        Assert.Equal(BackupArchiveContract.DatabasePath, output.File.Path);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(output.DatabasePath))), output.File.Sha256);
        Assert.Equal(new FileInfo(output.DatabasePath).Length, output.File.SizeBytes);
        Assert.Equal(TimeSpan.Zero, output.CreatedAtUtc.Offset);
        using var copy = Open(output.DatabasePath, SqliteOpenMode.ReadOnly);
        Assert.Equal("delete", Scalar(copy, "PRAGMA journal_mode;"));
        Assert.Equal("ok", Scalar(copy, "PRAGMA integrity_check;"));
        Assert.Null(Scalar(copy, "PRAGMA foreign_key_check;"));
        await using var restored = new JournalDbContext(new DbContextOptionsBuilder<JournalDbContext>().UseSqlite(copy).Options);
        Assert.Equal(tradeId, (await restored.Trades.SingleAsync()).Id);
        Assert.Equal(account.Id, (await restored.Trades.SingleAsync()).TradingAccountId);
        Assert.Equal(2, await restored.TradeExecutions.CountAsync());
        Assert.Contains(await restored.TradeExecutions.ToListAsync(), e => e.Commission is null);
        Assert.Equal(journal.Id, (await restored.DailyJournals.SingleAsync()).Id);
        Assert.Equal(2, await restored.DailyJournalRevisions.CountAsync());
        var analysis = await restored.CoachingAnalyses.SingleAsync();
        Assert.Equal(saved.Summary.Id, analysis.Id);
        Assert.True(saved.EvidenceJson == analysis.EvidenceJson && saved.ResponseJson == analysis.ResponseJson);
        Assert.Equal(output.Schema.AppliedMigrations.ToArray(), (await restored.Database.GetAppliedMigrationsAsync()).ToArray());
        var second = await service.CreateAsync(fixture.Stage);
        Assert.Equal(DatabaseSnapshotStatus.Success, second.Status);
        Assert.NotEqual(output.DatabasePath, second.Snapshot!.DatabasePath);
        Assert.Equal(output.File.Sha256, Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(output.DatabasePath))));
        Assert.DoesNotContain(fixture.Root, first.ToString());
        Assert.DoesNotContain(fixture.Root, output.ToString());
    }

    [Fact]
    public async Task ActiveWalWriterCannotLeakAnUncommittedOrMixedRevisionIntoSnapshot()
    {
        await using var fixture = await Fixture.Create();
        var journals = fixture.Services.GetRequiredService<IDailyJournalRepository>();
        var journal = (await journals.CreateAsync(new(Day, null, "Synthetic before"))).Journal!.Entry;
        using var writer = fixture.Open();
        Execute(writer, "PRAGMA journal_mode=WAL; BEGIN IMMEDIATE;");
        Execute(writer, "UPDATE DailyJournals SET Text='Synthetic pending', Revision=2; " +
            "INSERT INTO DailyJournalRevisions SELECT JournalId, 2, 'Synthetic pending', IsDraft, SavedAtUtc, WentWell, NeedsImprovement, NextTradingDay FROM DailyJournalRevisions;");
        var copied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new SqliteDatabaseSnapshotService(fixture.Paths, (phase, _) =>
        { if (phase == DatabaseSnapshotPhase.Integrity) { copied.SetResult(); release.Task.WaitAsync(TimeSpan.FromSeconds(20)).GetAwaiter().GetResult(); } });
        var pending = service.CreateAsync(fixture.Stage);
        try
        {
            await Task.WhenAny(copied.Task, pending).WaitAsync(TimeSpan.FromSeconds(20));
            Assert.True(copied.Task.IsCompleted);
            Execute(writer, "COMMIT;");
        }
        finally { release.TrySetResult(); }
        var result = await pending;
        Assert.Equal(DatabaseSnapshotStatus.Success, result.Status);
        using var copy = Open(result.Snapshot!.DatabasePath, SqliteOpenMode.ReadOnly);
        Assert.Equal(1L, Scalar(copy, "SELECT Revision FROM DailyJournals;"));
        Assert.Equal(1L, Scalar(copy, "SELECT COUNT(*) FROM DailyJournalRevisions;"));
        Assert.Equal(2, (await journals.GetAsync(Day, null))!.Entry.Revision);
    }

    [Theory]
    [InlineData("fk", DatabaseSnapshotStatus.ForeignKeyViolation)]
    [InlineData("check", DatabaseSnapshotStatus.InvalidDatabase)]
    [InlineData("missing-table", DatabaseSnapshotStatus.IncompatibleSchema)]
    [InlineData("missing-column", DatabaseSnapshotStatus.IncompatibleSchema)]
    [InlineData("missing-index", DatabaseSnapshotStatus.IncompatibleSchema)]
    [InlineData("future", DatabaseSnapshotStatus.IncompatibleSchema)]
    [InlineData("older", DatabaseSnapshotStatus.IncompatibleSchema)]
    [InlineData("extra-table", DatabaseSnapshotStatus.IncompatibleSchema)]
    public async Task InvalidOrIncompleteDatabaseIsRejectedWithoutChangingSource(string defect, DatabaseSnapshotStatus expected)
    {
        await using var fixture = await Fixture.Create();
        using (var connection = fixture.Open())
        {
            string sql = defect switch
            {
                "fk" => "PRAGMA foreign_keys=OFF; INSERT INTO DailyJournalRevisions(JournalId,Revision,Text,IsDraft,SavedAtUtc,WentWell,NeedsImprovement,NextTradingDay) VALUES('missing',1,'',1,'2026-10-10T12:00:00+00:00','','','');",
                "check" => "PRAGMA ignore_check_constraints=ON; INSERT INTO DailyJournals(Id,TradingDate,TradingAccountId,Text,IsDraft,Revision,CreatedAtUtc,UpdatedAtUtc,WentWell,NeedsImprovement,NextTradingDay) VALUES('invalid','2026-10-10',NULL,'',1,0,'2026-10-10T12:00:00+00:00','2026-10-10T12:00:00+00:00','','','');",
                "missing-table" => "DROP TABLE CoachingAnalyses;",
                "missing-column" => "ALTER TABLE CoachingAnalyses DROP COLUMN MetadataJson;",
                "missing-index" => "DROP INDEX IX_CoachingAnalyses_ReviewDate_ScopeKind_AccountId_GeneratedAtUtc_Id;",
                "future" => "INSERT INTO __EFMigrationsHistory VALUES('20990101000000_Future','99.0.0');",
                "extra-table" => "CREATE TABLE sqliteXUnexpected(Id INTEGER);",
                _ => "DELETE FROM __EFMigrationsHistory WHERE MigrationId='20261008195353_AddCoachingAnalysisSnapshots';"
            };
            Execute(connection, sql);
            if (defect == "fk") Assert.Equal("ok", Scalar(connection, "PRAGMA integrity_check;")); // Not a substitute for FK check.
            Execute(connection, "PRAGMA wal_checkpoint(TRUNCATE);");
        }
        // End this fixture's idle pooled writer handles before byte-for-byte comparison; never clear unrelated pools.
        SqliteTestPoolCleanup.ClearPersistencePools(fixture.Paths.DatabasePath);
        var before = SHA256.HashData(await File.ReadAllBytesAsync(fixture.Paths.DatabasePath));
        var result = await fixture.Service.CreateAsync(fixture.Stage);
        Assert.Equal(expected, result.Status); Assert.Null(result.Snapshot); Assert.False(result.CleanupFailed);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Stage));
        Assert.Equal(before, SHA256.HashData(await File.ReadAllBytesAsync(fixture.Paths.DatabasePath)));
    }

    [Theory]
    [InlineData(DatabaseSnapshotPhase.Source)]
    [InlineData(DatabaseSnapshotPhase.Copy)]
    [InlineData(DatabaseSnapshotPhase.Integrity)]
    [InlineData(DatabaseSnapshotPhase.Finalize)]
    public async Task CancellationBeforeAndAfterNativeCopyNeverReturnsOrRetainsSnapshot(DatabaseSnapshotPhase cancelAt)
    {
        await using var fixture = await Fixture.Create();
        using var cancellation = new CancellationTokenSource();
        var service = new SqliteDatabaseSnapshotService(fixture.Paths, (phase, _) => { if (phase == cancelAt) cancellation.Cancel(); });
        var result = await service.CreateAsync(fixture.Stage, cancellation.Token);
        Assert.Equal(DatabaseSnapshotStatus.Cancelled, result.Status); Assert.Null(result.Snapshot); Assert.False(result.CleanupFailed);
        Assert.True(!Directory.Exists(fixture.Stage) || !Directory.EnumerateFileSystemEntries(fixture.Stage).Any());
    }

    [Fact]
    public async Task IncompleteStagedOutputIsRejectedEvenWhenSourceSchemaIsValid()
    {
        await using var fixture = await Fixture.Create();
        var service = new SqliteDatabaseSnapshotService(fixture.Paths, (phase, destination) =>
        { if (phase == DatabaseSnapshotPhase.Integrity) Execute(destination!, "DROP TABLE CoachingAnalyses;"); });
        var result = await service.CreateAsync(fixture.Stage);
        Assert.Equal(DatabaseSnapshotStatus.IncompatibleSchema, result.Status);
        Assert.Null(result.Snapshot); Assert.False(result.CleanupFailed);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Stage));
        Assert.Equal(DatabaseSnapshotStatus.Success, (await fixture.Service.CreateAsync(fixture.Stage)).Status);
    }

    [Fact]
    public async Task AlreadyCancelledRequestDoesNotOpenSourceOrCreateStaging()
    {
        await using var fixture = await Fixture.Create();
        var service = new SqliteDatabaseSnapshotService(fixture.Paths, (_, _) => throw new InvalidOperationException("Must not start"));
        var result = await service.CreateAsync(fixture.Stage, new CancellationToken(true));
        Assert.Equal(DatabaseSnapshotStatus.Cancelled, result.Status);
        Assert.False(Directory.Exists(fixture.Stage));
    }

    [Fact]
    public async Task MissingSourceIsNotCreatedAndInvalidDestinationIsNotOverwritten()
    {
        await using var fixture = await Fixture.Create();
        var absent = new LocalApplicationPaths(Path.Combine(fixture.Root, "absent"));
        var missing = await new SqliteDatabaseSnapshotService(absent).CreateAsync(fixture.Stage);
        Assert.Equal(DatabaseSnapshotStatus.SourceMissing, missing.Status);
        Assert.False(Directory.Exists(absent.DataDirectory));
        var result = await fixture.Service.CreateAsync(fixture.Paths.DatabasePath);
        Assert.Equal(DatabaseSnapshotStatus.DestinationUnavailable, result.Status);
        Assert.False(result.CleanupFailed);
        Assert.Equal(DatabaseSnapshotStatus.DestinationUnavailable, (await fixture.Service.CreateAsync("relative-stage")).Status);
        Assert.Equal(DatabaseSnapshotStatus.Success, (await fixture.Service.CreateAsync(fixture.Stage)).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyOrNonSqliteSourceIsRejectedWithoutPublishing(bool garbage)
    {
        await using var fixture = await Fixture.Create();
        var isolated = new LocalApplicationPaths(Path.Combine(fixture.Root, "invalid")); isolated.EnsureDirectoriesExist();
        await File.WriteAllBytesAsync(isolated.DatabasePath, garbage ? [1, 2, 3, 4] : []);
        var result = await new SqliteDatabaseSnapshotService(isolated).CreateAsync(fixture.Stage);
        Assert.Equal(garbage ? DatabaseSnapshotStatus.InvalidDatabase : DatabaseSnapshotStatus.IncompatibleSchema, result.Status);
        Assert.Null(result.Snapshot); Assert.False(result.CleanupFailed);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Stage));
    }

    [Fact]
    public async Task ExclusiveRollbackWriterReturnsBoundedBusyAndLeavesNoStagedOutput()
    {
        await using var fixture = await Fixture.Create();
        SqliteTestPoolCleanup.ClearPersistencePools(fixture.Paths.DatabasePath);
        using var writer = fixture.Open();
        Execute(writer, "PRAGMA journal_mode=DELETE; BEGIN EXCLUSIVE;");
        try
        {
            var result = await fixture.Service.CreateAsync(fixture.Stage).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(DatabaseSnapshotStatus.Busy, result.Status);
            Assert.Null(result.Snapshot); Assert.False(result.CleanupFailed);
            Assert.True(!Directory.Exists(fixture.Stage) || !Directory.EnumerateFileSystemEntries(fixture.Stage).Any());
        }
        finally { Execute(writer, "ROLLBACK;"); }
    }

    [Theory]
    [InlineData(13, DatabaseSnapshotStatus.InsufficientStorage)]
    [InlineData(10, DatabaseSnapshotStatus.IoFailure)]
    public async Task NativeStorageFailuresAreSanitizedAndRemovePartialFiles(int code, DatabaseSnapshotStatus expected)
    {
        await using var fixture = await Fixture.Create();
        var service = new SqliteDatabaseSnapshotService(fixture.Paths, (phase, _) =>
        { if (phase == DatabaseSnapshotPhase.Integrity) throw new SqliteException("sensitive source must not escape", code); });
        var result = await service.CreateAsync(fixture.Stage);
        Assert.Equal(expected, result.Status); Assert.Equal(code, result.SqliteErrorCode);
        Assert.Null(result.Snapshot); Assert.False(result.CleanupFailed);
        Assert.DoesNotContain("sensitive", result.ToString());
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Stage));
    }

    [Fact]
    public async Task FailedCleanupIsReportedWithoutCallingTheIncompleteFileASnapshot()
    {
        await using var fixture = await Fixture.Create();
        FileStream? lockFile = null;
        var service = new SqliteDatabaseSnapshotService(fixture.Paths, (phase, _) =>
        {
            if (phase != DatabaseSnapshotPhase.Finalize) return;
            string file = Directory.GetFiles(fixture.Stage, "journal.db", SearchOption.AllDirectories).Single();
            lockFile = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            throw new IOException("Synthetic cleanup fault");
        });
        try
        {
            var result = await service.CreateAsync(fixture.Stage);
            Assert.Equal(DatabaseSnapshotStatus.IoFailure, result.Status); Assert.Null(result.Snapshot);
            Assert.True(result.CleanupFailed);
            Assert.EndsWith(".partial", Assert.Single(Directory.GetDirectories(fixture.Stage)));
        }
        finally { lockFile?.Dispose(); }
    }

    private static SqliteConnection Open(string path, SqliteOpenMode mode = SqliteOpenMode.ReadWrite)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = path, Mode = mode, Pooling = false, ForeignKeys = true, DefaultTimeout = 2 }.ToString());
        connection.Open(); return connection;
    }
    private static void Execute(SqliteConnection connection, string sql)
    { using var command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery(); }
    private static object? Scalar(SqliteConnection connection, string sql)
    { using var command = connection.CreateCommand(); command.CommandText = sql; return command.ExecuteScalar(); }

    private sealed class Fixture : IAsyncDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "ptj-snapshot-test-" + Guid.NewGuid().ToString("N"));
        public LocalApplicationPaths Paths { get; }
        public string Stage => Path.Combine(Root, "stage");
        public ServiceProvider Services { get; }
        public IDatabaseSnapshotService Service => Services.GetRequiredService<IDatabaseSnapshotService>();
        private Fixture()
        {
            Paths = new(Root); Paths.EnsureDirectoriesExist();
            Services = new ServiceCollection().AddPersistence(Paths).BuildServiceProvider();
        }
        public static async Task<Fixture> Create()
        {
            var fixture = new Fixture();
            try { await fixture.Services.GetRequiredService<JournalDatabaseInitializer>().InitializeAsync(); return fixture; }
            catch { await fixture.DisposeAsync(); throw; }
        }
        public SqliteConnection Open() => SqliteDatabaseSnapshotTests.Open(Paths.DatabasePath);
        public JournalDbContext Context() => Services.GetRequiredService<IDbContextFactory<JournalDbContext>>().CreateDbContext();
        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            SqliteTestPoolCleanup.ClearPersistencePools(Paths.DatabasePath);
            Directory.Delete(Root, true);
        }
    }
}
