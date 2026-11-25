using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using Microsoft.Data.Sqlite;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.Common.Storage;
using PersonalTradingJournal.Domain.Screenshots;
using PersonalTradingJournal.Infrastructure.Backups;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Infrastructure.Imports.Tradovate;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Tests.Persistence;

namespace PersonalTradingJournal.Infrastructure.Tests.Imports.Tradovate;

public sealed class TradovateTimestampRepairTests
{
    [Fact]
    public async Task RepairPreservesAnnotationsEconomicsWholeSecondsAndReplayAndMovesCalendarDay()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (scope, bytes, request) = await Seed(db);
        var repair = new TradovateTimestampRepairService(db.ContextFactory);
        Assert.Contains("different timestamp", await new TradovateImportStore(db.ContextFactory).FindVerifiedImportConflictAsync(request.Preparation));
        var before = await PreservedState(db);
        var plan = await repair.PreviewAsync(scope, bytes);
        Assert.Equal(4, plan.MatchedExecutions);
        Assert.Equal(1, plan.AffectedTrades);
        Assert.Equal(2, plan.UnchangedExecutions);
        Assert.Equal(2, plan.Changes.Count(c => c.Table == "TradeExecutions"));
        Assert.Equal(2, plan.Changes.Count(c => c.Table == "TradovateImportedExecutions"));
        Assert.Equal(2, plan.Changes.Count(c => c.Table == "TradeBrowse"));
        Assert.Equal(before, await PreservedState(db));
        var calendar = db.ServiceProvider.GetRequiredService<ITradingCalendarDayReader>();
        var dayBefore = await calendar.GetAsync(TradingCalendarDayQuery.ForTradingDate(new(2026, 10, 23), scope.AccountId));
        Assert.Single(dayBefore.Trades);
        var backups = new RecordingBackup();
        var result = await repair.ApplyAsync(scope, bytes, plan, backups, "synthetic.ptjbackup");
        Assert.Equal(2, result.ChangedExecutions);
        Assert.Equal(1, backups.Calls);
        Assert.Equal(before, await PreservedState(db));
        Assert.Empty((await calendar.GetAsync(TradingCalendarDayQuery.ForTradingDate(new(2026, 10, 23), scope.AccountId))).Trades);
        Assert.Single((await calendar.GetAsync(TradingCalendarDayQuery.ForTradingDate(new(2026, 10, 24), scope.AccountId))).Trades);
        var store = new TradovateImportStore(db.ContextFactory);
        Assert.Null(await store.FindVerifiedImportConflictAsync(request.Preparation));
        // The manually edited costs remain guarded during replay.
        Assert.Equal(TradovateImportStatus.Blocked, (await store.ImportAsync(request)).Status);
        Assert.Equal(before, await PreservedState(db));
        var repeat = await repair.ApplyAsync(scope, bytes, plan, backups, "unused.ptjbackup");
        Assert.True(repeat.AlreadyApplied);
        Assert.Equal(0, repeat.ChangedExecutions);
        Assert.Equal(1, backups.Calls);
    }

    [Fact]
    public async Task VerifiedEnrichmentAndReplayAfterRepairKeepTradeAndExecutionIdentities()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (scope, bytes, request) = await Seed(db, manualCosts: false);
        var repair = new TradovateTimestampRepairService(db.ContextFactory);
        var plan = await repair.PreviewAsync(scope, bytes);
        await repair.ApplyAsync(scope, bytes, plan, new RecordingBackup(), "synthetic.ptjbackup");
        var store = new TradovateImportStore(db.ContextFactory);
        var enriched = await store.ImportAsync(request);
        Assert.Equal(0, enriched.ImportedTradeCount);
        Assert.Equal(2, enriched.UpdatedCostTradeCount);
        Assert.Equal(TradovateImportStatus.NoChanges, (await store.ImportAsync(request)).Status);
        await using var context = db.ContextFactory.CreateDbContext();
        Assert.Equal(scope.Identities.Select(i => i.ExecutionId).Order(), (await context.TradeExecutions.Select(e => e.Id).ToArrayAsync()).Order());
        Assert.Equal(2, await context.Trades.CountAsync());
    }

    [Fact]
    public async Task CompleteBackupContainsPreRepairDatabaseAndProtectedScreenshot()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (scope, bytes, _) = await Seed(db);
        var repair = new TradovateTimestampRepairService(db.ContextFactory);
        var plan = await repair.PreviewAsync(scope, bytes);
        await using var location = db.ContextFactory.CreateDbContext();
        string directory = Path.GetDirectoryName(location.Database.GetDbConnection().DataSource)!;
        string archive = Path.Combine(directory, "before-repair.ptjbackup");
        await repair.ApplyAsync(scope, bytes, plan, db.ServiceProvider.GetRequiredService<IBackupArchiveService>(), archive);
        Assert.True((await new RestorePreflightService().InspectAsync(archive, directory)).Validated);
        using var zip = ZipFile.OpenRead(archive);
        var original = Assert.Single(zip.Entries, e => e.FullName.StartsWith(BackupArchiveContract.ScreenshotsPrefix, StringComparison.Ordinal) && e.Length > 0);
        await using var verify = db.ContextFactory.CreateDbContext();
        var screenshot = await verify.TradeScreenshots.SingleAsync();
        await using var payload = original.Open();
        using var copied = new MemoryStream();
        await payload.CopyToAsync(copied);
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(directory, "screenshots", screenshot.StorageKey)), copied.ToArray());
        string extracted = Path.Combine(directory, "backup-inspect.db");
        zip.GetEntry(BackupArchiveContract.DatabasePath)!.ExtractToFile(extracted);
        var options = new DbContextOptionsBuilder<JournalDbContext>().UseSqlite(new SqliteConnectionStringBuilder
        { DataSource = extracted, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()).Options;
        await using var archived = new JournalDbContext(options);
        var originals = await archived.TradeExecutions.AsNoTracking().ToDictionaryAsync(e => e.Id);
        Assert.All(scope.Identities, identity => Assert.Equal(identity.DiagnosedUtc, originals[identity.ExecutionId].ExecutedAtUtc));
    }

    [Theory]
    [InlineData("source")]
    [InlineData("baseline")]
    [InlineData("duplicate")]
    [InlineData("timestamp")]
    [InlineData("unaffected")]
    [InlineData("description")]
    [InlineData("extra")]
    [InlineData("backup")]
    [InlineData("source-duplicates")]
    [InlineData("account")]
    [InlineData("partial")]
    [InlineData("approval")]
    public async Task DiscrepanciesAbortWithoutPartialWrites(string fault)
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (scope, bytes, _) = await Seed(db);
        var repair = new TradovateTimestampRepairService(db.ContextFactory);
        var plan = await repair.PreviewAsync(scope, bytes);
        var backups = new RecordingBackup();
        if (fault == "source") bytes = [.. bytes, 10];
        if (fault == "source-duplicates")
        {
            bytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes) + "\n" + Encoding.UTF8.GetString(bytes).Split('\n')[1]);
            scope = scope with { FillsSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)) };
        }
        if (fault == "account") scope = scope with { ExternalAccountId = "OTHER" };
        if (fault == "approval") plan = plan with { Changes = [] };
        if (fault == "partial")
        {
            await using var context = db.ContextFactory.CreateDbContext();
            var first = plan.Changes.First(c => c.Table == "TradeExecutions");
            Guid id = Guid.Parse(first.Key);
            (await context.TradeExecutions.SingleAsync(e => e.Id == id)).ExecutedAtUtc = first.After;
            (await context.TradovateImportedExecutions.SingleAsync(e => e.TradeExecutionId == id)).SourceFillExecutedAtUtc = first.After;
            await context.SaveChangesAsync();
        }
        if (fault == "baseline") scope = scope with { Identities = scope.Identities.Select((i, n) => n == 0 ? i with { DiagnosedUtc = i.DiagnosedUtc.AddSeconds(1) } : i).ToArray() };
        if (fault == "duplicate") scope = scope with { Identities = [.. scope.Identities, scope.Identities[0]] };
        if (fault == "backup") backups.Fail = true;
        if (fault is "timestamp" or "unaffected" or "description" or "extra")
        {
            await using var context = db.ContextFactory.CreateDbContext();
            if (fault == "description") (await context.Trades.FirstAsync()).Description = "Edited since approval";
            else if (fault == "extra")
            {
                var row = await context.TradeExecutions.FirstAsync();
                context.TradeExecutions.Add(new() { Id = Guid.NewGuid(), TradeId = row.TradeId,
                    Side = row.Side, Quantity = row.Quantity, Price = row.Price, ExecutedAtUtc = row.ExecutedAtUtc, Sequence = 99 });
            }
            else
            {
                var target = fault == "timestamp"
                    ? plan.Changes.First(c => c.Table == "TradeExecutions").Key
                    : scope.Identities.Single(i => i.FillId == "B2").ExecutionId.ToString();
                var execution = await context.TradeExecutions.SingleAsync(e => e.Id == Guid.Parse(target));
                execution.ExecutedAtUtc = execution.ExecutedAtUtc.AddSeconds(1);
            }
            await context.SaveChangesAsync();
        }
        var before = await RawState(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repair.ApplyAsync(scope, bytes, plan, backups, "synthetic.ptjbackup"));
        Assert.Equal(before, await RawState(db));
    }

    [Fact]
    public async Task SQLiteFailureAfterFirstUpdateRollsBackTheWholeRepair()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (scope, bytes, _) = await Seed(db);
        var repair = new TradovateTimestampRepairService(db.ContextFactory);
        var plan = await repair.PreviewAsync(scope, bytes);
        var before = await RawState(db);
        await using (var context = db.ContextFactory.CreateDbContext())
            await context.Database.ExecuteSqlRawAsync("CREATE TRIGGER refuse_second AFTER UPDATE ON TradeExecutions WHEN (SELECT COUNT(*) FROM TradeExecutions WHERE ExecutedAtUtc LIKE '%.%') >= 2 BEGIN SELECT RAISE(ABORT, 'synthetic repair failure'); END");
        await Assert.ThrowsAsync<DbUpdateException>(() => repair.ApplyAsync(scope, bytes, plan, new RecordingBackup(), "synthetic.ptjbackup"));
        Assert.Equal(before, await RawState(db));
    }

    [Fact]
    public async Task StateChangedDuringBackupAbortsBeforeWriting()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (scope, bytes, _) = await Seed(db);
        var repair = new TradovateTimestampRepairService(db.ContextFactory);
        var plan = await repair.PreviewAsync(scope, bytes);
        var backups = new RecordingBackup
        {
            AfterBackup = async () =>
            {
                await using var context = db.ContextFactory.CreateDbContext();
                (await context.Trades.FirstAsync()).Description = "Concurrent edit";
                await context.SaveChangesAsync();
            }
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => repair.ApplyAsync(scope, bytes, plan, backups, "synthetic.ptjbackup"));
        var first = plan.Changes.First(c => c.Table == "TradeExecutions");
        Guid executionId = Guid.Parse(first.Key);
        await using var verify = db.ContextFactory.CreateDbContext();
        Assert.Equal(first.Before, (await verify.TradeExecutions.SingleAsync(e => e.Id == executionId)).ExecutedAtUtc);
    }

    private static async Task<(TradovateTimestampRepairScope, byte[], TradovateImportRequest)> Seed(ReaderTestDatabase db, bool manualCosts = true)
    {
        var (performance, fills, cash) = TradovateVerifiedCostTests.FixedClockExports(false);
        var request = await TradovateVerifiedCostTests.Prepare(db, performance, fills, cash);
        var old = await TradovateVerifiedCostTests.Prepare(db, performance, fills, cash,
            useFillsTimes: false, accountId: request.Preparation.AccountSnapshot!.TradingAccountId);
        Assert.Equal(2, (await new TradovateImportStore(db.ContextFactory).ImportAsync(old with { CostSources = null })).ImportedTradeCount);
        await using var context = db.ContextFactory.CreateDbContext();
        Guid setup = Guid.NewGuid(), mistake = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        context.TradingSetups.Add(new() { Id = setup, Name = "Synthetic setup", CreatedAtUtc = now, UpdatedAtUtc = now });
        context.TradingMistakes.Add(new() { Id = mistake, Name = "Synthetic mistake", CreatedAtUtc = now, UpdatedAtUtc = now });
        var trade = await context.Trades.FirstAsync();
        trade.Description = "Manually edited description";
        trade.TradingSetupId = setup;
        context.TradeMistakes.Add(new() { Id = Guid.NewGuid(), TradeId = trade.Id, TradingMistakeId = mistake, Note = "Keep note", CreatedAtUtc = now, UpdatedAtUtc = now });
        byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        var image = await db.ServiceProvider.GetRequiredService<IPlaybookImageStorage>().StoreAsync("protected.png", png);
        context.TradeScreenshots.Add(new() { Id = Guid.NewGuid(), TradeId = trade.Id, Type = TradeScreenshotType.Other,
            FileName = "protected.png", StorageKey = image.StorageKey, CreatedAtUtc = now, UpdatedAtUtc = now });
        var execution = await context.TradeExecutions.FirstAsync();
        if (manualCosts)
        {
            execution.Commission = 1.25m;
            execution.Fees = 0.25m;
        }
        await context.SaveChangesAsync();
        foreach (var savedTrade in await context.Trades.ToArrayAsync())
        {
            var browse = await context.TradeBrowse.SingleAsync(b => b.TradeId == savedTrade.Id);
            var savedExecutions = await context.TradeExecutions.Where(e => e.TradeId == savedTrade.Id).ToArrayAsync();
            TradeBrowsePersistenceMapper.Apply(browse, TradePersistenceMapper.ToDomain(savedTrade, savedExecutions));
        }
        await context.SaveChangesAsync();
        var identities = await context.TradovateImportedExecutions.AsNoTracking().OrderBy(i => i.TradeExecutionId).ToArrayAsync();
        var bytes = Encoding.UTF8.GetBytes(fills);
        var scope = new TradovateTimestampRepairScope(trade.TradingAccountId, "SIM-COST", "USD",
            Convert.ToHexStringLower(SHA256.HashData(bytes)), identities.Select(i => new TradovateTimestampRepairIdentity(
                i.TradeId, i.TradeExecutionId, i.ExternalExecutionId, i.AllocationIndex, i.BrokerSymbol, i.Side,
                i.AllocatedQuantity!.Value, i.SourceFillQuantity!.Value, i.SourceFillPrice!.Value, i.SourceFillExecutedAtUtc!.Value)).ToArray());
        return (scope, bytes, request);
    }

    private static async Task<string> PreservedState(ReaderTestDatabase db)
    {
        await using var context = db.ContextFactory.CreateDbContext();
        return JsonSerializer.Serialize(new
        {
            Trades = await context.Trades.AsNoTracking().OrderBy(t => t.Id).ToArrayAsync(),
            Executions = await context.TradeExecutions.AsNoTracking().OrderBy(e => e.Id)
                .Select(e => new { e.Id, e.TradeId, e.Sequence, e.Quantity, e.Price, e.Commission, e.Fees, e.ExternalExecutionId, e.ExternalOrderId }).ToArrayAsync(),
            Mistakes = await context.TradeMistakes.AsNoTracking().ToArrayAsync(),
            Images = await context.TradeScreenshots.AsNoTracking().ToArrayAsync()
        });
    }

    private static async Task<string> RawState(ReaderTestDatabase db)
    {
        await using var context = db.ContextFactory.CreateDbContext();
        return JsonSerializer.Serialize(new
        {
            Executions = await context.TradeExecutions.AsNoTracking().OrderBy(e => e.Id).ToArrayAsync(),
            Ledger = await context.TradovateImportedExecutions.AsNoTracking().OrderBy(e => e.TradeExecutionId).ToArrayAsync(),
            Browse = await context.TradeBrowse.AsNoTracking().OrderBy(e => e.TradeId).ToArrayAsync(),
            Metadata = await PreservedState(db)
        });
    }

    private sealed class RecordingBackup : IBackupArchiveService
    {
        public int Calls;
        public bool Fail;
        public Func<Task>? AfterBackup;
        public async Task<BackupArchiveResult> CreateAsync(string path, CancellationToken token = default)
        {
            Calls++;
            if (AfterBackup is not null) await AfterBackup();
            return new(Guid.NewGuid(), BackupArchivePhase.Publish, Fail ? BackupValidationCode.IoFailure : null);
        }
    }
}
