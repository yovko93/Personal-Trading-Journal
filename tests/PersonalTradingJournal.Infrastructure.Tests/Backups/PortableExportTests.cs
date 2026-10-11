using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Backups;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using Fixture = PersonalTradingJournal.Infrastructure.Tests.Backups.BackupArchiveTests.Fixture;

namespace PersonalTradingJournal.Infrastructure.Tests.Backups;

public sealed class PortableExportTests
{
    private static string Output(Fixture f) => Path.Combine(f.Root, "export");
    private static PortableExportService Service(Fixture f, Action<PortableExportPhase, string>? hook = null, ExportLimits? limits = null) =>
        new(f.Paths, f.Services.GetRequiredService<IDatabaseSnapshotService>(), hook, limits ?? new());
    private static JsonDocument Read(Fixture f) => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Output(f), PortableExportContract.DataFile)));
    private static void NoPartial(Fixture f) => Assert.Empty(Directory.GetDirectories(f.Root, ".ptjexport-*.partial"));

    [Fact]
    public async Task EmptyExportHasVersionedTablesHeaderOnlyCsvAndVerifiableInventoryWithoutDatabaseOrSecrets()
    {
        await using var f = await Fixture.Create();
        // Resolving the service is inert; there is no AI dependency or automatic export.
        var service = f.Services.GetRequiredService<IPortableExportService>(); Assert.False(Directory.Exists(Output(f)));
        var result = await service.CreateAsync(Output(f)); Assert.True(result.Succeeded, result.ToString()); Assert.Equal(0, result.Rows);
        using var document = Read(f); var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("version").GetInt32()); Assert.False(root.GetProperty("restorableBackup").GetBoolean());
        Assert.Equal("metadata-only", root.GetProperty("screenshotContent").GetString());
        foreach (var table in PortableExportSchema.Tables) Assert.Equal(0, root.GetProperty("tables").GetProperty(table.Name).GetArrayLength());
        Assert.Equal(6, Directory.GetFiles(Output(f)).Length); NoPartial(f);
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Output(f), PortableExportContract.InventoryFile)));
        foreach (var file in manifest.RootElement.GetProperty("files").EnumerateArray())
        {
            string path = Path.Combine(Output(f), file.GetProperty("path").GetString()!);
            Assert.Equal(file.GetProperty("sizeBytes").GetInt64(), new FileInfo(path).Length);
            using var bytes = File.OpenRead(path); Assert.Equal(file.GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
        }
        Assert.Equal(result.OutputBytes, Directory.GetFiles(Output(f)).Sum(p => new FileInfo(p).Length));
        foreach (string csv in Directory.GetFiles(Output(f), "*.csv")) Assert.Single(ParseCsv(await File.ReadAllTextAsync(csv)));
    }

    [Fact]
    public async Task AllLogicalColumnsAreExplicitlyCoveredExceptRebuildableBrowseCache()
    {
        await using var f = await Fixture.Create(); using var db = f.Context();
        var entities = db.Model.GetEntityTypes().Where(t => t.ClrType != typeof(TradeBrowseRecord)).ToArray();
        Assert.Equal(entities.Length, PortableExportSchema.Tables.Length);
        foreach (var entity in entities)
        {
            var table = Assert.Single(PortableExportSchema.Tables, t => t.Source == entity.GetTableName());
            Assert.Equal(entity.GetProperties().Select(p => p.Name).Order(), table.Columns.Select(c => c.Column).Order());
        }
    }

    [Fact]
    public async Task MixedAccountsExactEconomicsRelationshipsRevisionsAndSavedSnapshotsSurviveWithoutLiveFileReads()
    {
        await using var f = await Fixture.Create(); await Seed(f);
        // Screenshots deliberately have no binaries. Metadata-only export must not require or manufacture them.
        await using var context = f.Context();
        var expectedAnalyses = await context.CoachingAnalyses.AsNoTracking().ToArrayAsync();
        var expectedExecutions = await context.TradeExecutions.AsNoTracking().ToArrayAsync();
        var result = await Service(f).CreateAsync(Output(f)); Assert.True(result.Succeeded, result.ToString());
        using var data = Read(f); var tables = data.RootElement.GetProperty("tables");
        Assert.Equal(2, tables.GetProperty("accounts").GetArrayLength());
        Assert.Equal(new[] { "EUR", "USD" }, tables.GetProperty("accounts").EnumerateArray().Select(r => r.GetProperty("currency").GetString()).Order());
        Assert.Equal(2, tables.GetProperty("screenshots").GetArrayLength()); Assert.Empty(Directory.GetDirectories(Output(f)));
        Assert.Equal(2, tables.GetProperty("journals").GetArrayLength()); Assert.Equal(3, tables.GetProperty("journalRevisions").GetArrayLength());
        Assert.Contains(tables.GetProperty("journals").EnumerateArray(), j => j.GetProperty("tradingAccountId").ValueKind == JsonValueKind.Null);
        Assert.All(tables.GetProperty("journals").EnumerateArray(), j => Assert.Equal("2026-11-01", j.GetProperty("tradingDateNewYork").GetString()));
        foreach (var execution in tables.GetProperty("executions").EnumerateArray())
        {
            var expected = expectedExecutions.Single(x => x.Id == execution.GetProperty("id").GetGuid());
            Assert.Equal(expected.Price, decimal.Parse(execution.GetProperty("price").GetString()!, CultureInfo.InvariantCulture));
            Assert.Equal(expected.TradeId, execution.GetProperty("tradeId").GetGuid());
            Assert.Equal(expected.ExecutedAtUtc, DateTimeOffset.Parse(execution.GetProperty("executedAtUtc").GetString()!, CultureInfo.InvariantCulture));
            Assert.EndsWith("Z", execution.GetProperty("executedAtUtc").GetString());
            if (expected.Commission is null) Assert.Equal(JsonValueKind.Null, execution.GetProperty("commission").ValueKind);
            else Assert.Equal(expected.Commission.Value, decimal.Parse(execution.GetProperty("commission").GetString()!, CultureInfo.InvariantCulture));
        }
        foreach (var analysis in tables.GetProperty("analyses").EnumerateArray())
        {
            var expected = expectedAnalyses.Single(a => a.Id == analysis.GetProperty("id").GetGuid());
            Assert.Equal(expected.EvidenceJson, analysis.GetProperty("evidenceJson").GetString());
            Assert.Equal(expected.ResponseJson, analysis.GetProperty("responseJson").GetString());
            Assert.Equal(expected.MetadataJson, analysis.GetProperty("metadataJson").GetString());
            Assert.Equal(expected.AccountId, analysis.GetProperty("accountId").GetGuid());
            Assert.Equal("Deleted historical", analysis.GetProperty("accountDisplayName").GetString());
            Assert.DoesNotContain(tables.GetProperty("accounts").EnumerateArray(), a => a.GetProperty("id").GetGuid() == expected.AccountId);
        }
        Assert.Single(tables.GetProperty("tradeMistakes").EnumerateArray());
        Assert.Single(tables.GetProperty("tradovateImportExecutions").EnumerateArray());
        Assert.Single(tables.GetProperty("topstepImportRows").EnumerateArray()); NoPartial(f);
    }

    [Theory]
    [InlineData("=SUM(1,2)")]
    [InlineData("+cmd")]
    [InlineData("-10+2")]
    [InlineData("@SUM(1)")]
    [InlineData("\t=1")]
    [InlineData("\r\n=1")]
    [InlineData("  =1")]
    [InlineData("＝1")]
    [InlineData("'already quoted")]
    [InlineData("Български, 日本語 \"text\"\r\nnext 😀")]
    [InlineData("")]
    [InlineData("null")]
    public async Task CsvTextIsDisplayEscapedAndQuotedButJsonRemainsExact(string value)
    {
        await using var f = await Fixture.Create();
        await f.Services.GetRequiredService<IDailyJournalRepository>().CreateAsync(new(new(2026, 10, 10), null, value));
        Assert.True((await Service(f).CreateAsync(Output(f))).Succeeded);
        using var data = Read(f); Assert.Equal(value, data.RootElement.GetProperty("tables").GetProperty("journals")[0].GetProperty("text").GetString());
        var csv = ParseCsv(await File.ReadAllTextAsync(Path.Combine(Output(f), "journals.csv")));
        Assert.Equal(value.Length == 0 ? "" : "'" + value, csv[1][Array.IndexOf(csv[0], "text")]);
        Assert.Equal("null", csv[1][Array.IndexOf(csv[0], "tradingAccountId")]);
    }

    [Fact]
    public async Task NumericValuesAreNotSpreadsheetEscapedAndUnknownIsNotZero()
    {
        await using var f = await Fixture.Create(); await Seed(f);
        Assert.True((await Service(f).CreateAsync(Output(f))).Succeeded);
        var csv = ParseCsv(await File.ReadAllTextAsync(Path.Combine(Output(f), "executions.csv")));
        int commission = Array.IndexOf(csv[0], "commission"), price = Array.IndexOf(csv[0], "price"), fees = Array.IndexOf(csv[0], "fees");
        Assert.Contains(csv.Skip(1), r => r[commission] == "null" && r[fees] == "0.0");
        Assert.Contains(csv.Skip(1), r => decimal.TryParse(r[commission], CultureInfo.InvariantCulture, out var d) && d < 0);
        Assert.All(csv.Skip(1), r => Assert.False(r[price].StartsWith('\'')));
    }

    [Fact]
    public async Task SnapshotIgnoresLaterWritesAndExportDoesNotChangeSourceOrRefreshItsCaches()
    {
        await using var f = await Fixture.Create(); await Seed(f);
        string original; using (var db = f.Context()) original = (await db.TradingAccounts.OrderBy(a => a.Id).FirstAsync()).Name;
        var result = await Service(f, (phase, _) =>
        {
            if (phase != PortableExportPhase.Read) return;
            using var db = f.Context(); var account = db.TradingAccounts.OrderBy(a => a.Id).First(); account.Name = "Later committed name"; db.SaveChanges();
        }).CreateAsync(Output(f));
        Assert.True(result.Succeeded); using var document = Read(f);
        Assert.Contains(document.RootElement.GetProperty("tables").GetProperty("accounts").EnumerateArray(), a => a.GetProperty("name").GetString() == original);
        using var verify = f.Context(); Assert.Equal("Later committed name", (await verify.TradingAccounts.OrderBy(a => a.Id).FirstAsync()).Name);
        Assert.Empty(await verify.TradeBrowse.ToArrayAsync());
        string second = Path.Combine(f.Root, "second");
        // Close persistence pools so physical byte equality is meaningful; no writer/checkpoint runs during the second export.
        await f.Services.DisposeAsync(); JournalDataSession.ClearOwnedPools(f.Paths);
        byte[] before = SHA256.HashData(await File.ReadAllBytesAsync(f.Paths.DatabasePath));
        Assert.True((await new PortableExportService(f.Paths, new SqliteDatabaseSnapshotService(f.Paths)).CreateAsync(second)).Succeeded);
        Assert.Equal(before, SHA256.HashData(await File.ReadAllBytesAsync(f.Paths.DatabasePath)));
    }

    [Fact]
    public async Task ManyRowsStayOrderedAcrossRepeatedExportsAndRetainLongFields()
    {
        await using var f = await Fixture.Create(); using (var db = f.Context())
            await db.Database.ExecuteSqlRawAsync("WITH RECURSIVE n(x) AS (SELECT 1 UNION ALL SELECT x+1 FROM n WHERE x<5000) INSERT INTO TradingMistakes (Id,Name,Description,IsActive,CreatedAtUtc,UpdatedAtUtc) SELECT '00000000-0000-0000-0000-' || printf('%012d',x), 'Synthetic ' || x, {0}, 1, '2026-10-10 00:00:00','2026-10-10 00:00:00' FROM n;", new string('x', 2000));
        Assert.True((await Service(f).CreateAsync(Output(f))).Succeeded);
        using var first = Read(f); var table = first.RootElement.GetProperty("tables").GetProperty("mistakes");
        Assert.Equal(5000, table.GetArrayLength()); Assert.All(table.EnumerateArray(), r => Assert.Equal(2000, r.GetProperty("description").GetString()!.Length));
        Assert.Equal(table.EnumerateArray().Select(r => r.GetProperty("id").GetString()).Order(), table.EnumerateArray().Select(r => r.GetProperty("id").GetString()));
        string second = Path.Combine(f.Root, "second"); Assert.True((await Service(f).CreateAsync(second)).Succeeded);
        using var next = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(second, PortableExportContract.DataFile)));
        Assert.Equal(first.RootElement.GetProperty("tables").GetRawText(), next.RootElement.GetProperty("tables").GetRawText());
    }

    [Theory]
    [InlineData(PortableExportPhase.Destination)]
    [InlineData(PortableExportPhase.Snapshot)]
    [InlineData(PortableExportPhase.Read)]
    [InlineData(PortableExportPhase.Write)]
    [InlineData(PortableExportPhase.Inventory)]
    [InlineData(PortableExportPhase.Cleanup)]
    [InlineData(PortableExportPhase.Publish)]
    public async Task CancellationNeverPublishesPartialOutput(PortableExportPhase at)
    {
        await using var f = await Fixture.Create(); await Seed(f); using var cancellation = new CancellationTokenSource();
        var result = await Service(f, (phase, _) => { if (phase == at) cancellation.Cancel(); }).CreateAsync(Output(f), cancellation.Token);
        Assert.Equal(PortableExportStatus.Cancelled, result.Status); Assert.False(Directory.Exists(Output(f))); NoPartial(f);
    }

    [Fact]
    public async Task CancellationDuringRowsAndSizeLimitsDoNotPublishOrTruncate()
    {
        await using var f = await Fixture.Create(); await Seed(f); using var cancellation = new CancellationTokenSource(); int rows = 0;
        var result = await Service(f, (phase, _) => { if (phase == PortableExportPhase.Write && ++rows == 6) cancellation.Cancel(); }).CreateAsync(Output(f), cancellation.Token);
        Assert.Equal(PortableExportStatus.Cancelled, result.Status); NoPartial(f);
        foreach (var limits in new[] { new ExportLimits(OutputBytes: 100), new ExportLimits(CellBytes: 2) })
        {
            result = await Service(f, limits: limits).CreateAsync(Output(f)); Assert.Equal(PortableExportStatus.LimitExceeded, result.Status);
            Assert.False(Directory.Exists(Output(f))); NoPartial(f);
        }
    }

    [Theory]
    [InlineData(PortableExportPhase.Read)]
    [InlineData(PortableExportPhase.Write)]
    [InlineData(PortableExportPhase.Inventory)]
    [InlineData(PortableExportPhase.Cleanup)]
    [InlineData(PortableExportPhase.Publish)]
    public async Task IoFailureRemovesOnlyOwnedStagingAndKeepsSource(PortableExportPhase at)
    {
        await using var f = await Fixture.Create(); await Seed(f);
        var result = await Service(f, (phase, _) => { if (phase == at) throw new IOException("sensitive detail must not escape"); }).CreateAsync(Output(f));
        Assert.False(result.Succeeded); Assert.DoesNotContain("sensitive", result.ToString()); Assert.DoesNotContain(f.Root, result.ToString());
        Assert.False(Directory.Exists(Output(f))); NoPartial(f); using var db = f.Context(); Assert.Equal(2, await db.Trades.CountAsync());
    }

    [Fact]
    public async Task CleanupFailureReportsRetainedUnexpectedStagingAndDoesNotDeleteIt()
    {
        await using var f = await Fixture.Create(); string? unexpected = null;
        var result = await Service(f, (phase, root) =>
        {
            if (phase != PortableExportPhase.Write) return;
            unexpected = Path.Combine(root, "not-owned.txt"); File.WriteAllText(unexpected, "synthetic unrelated file"); throw new IOException();
        }).CreateAsync(Output(f));
        Assert.False(result.Succeeded); Assert.True(result.CleanupFailed); Assert.True(File.Exists(unexpected)); Assert.False(Directory.Exists(Output(f)));
    }

    [Fact]
    public async Task ExistingDestinationAndPublicationRaceNeverOverwriteAndInstalledDirectoryIsRejected()
    {
        await using var f = await Fixture.Create();
        Assert.Equal(PortableExportStatus.DestinationUnavailable, (await Service(f).CreateAsync(Path.Combine(f.Paths.DataDirectory, "export"))).Status);
        Directory.CreateDirectory(Output(f)); string sentinel = Path.Combine(Output(f), "keep.txt"); await File.WriteAllTextAsync(sentinel, "keep");
        Assert.Equal(PortableExportStatus.DestinationUnavailable, (await Service(f).CreateAsync(Output(f))).Status); Assert.Equal("keep", await File.ReadAllTextAsync(sentinel));
        string destination = Path.Combine(f.Root, "race");
        var result = await Service(f, (phase, _) => { if (phase == PortableExportPhase.Publish) Directory.CreateDirectory(destination); }).CreateAsync(destination);
        Assert.False(result.Succeeded); Assert.Empty(Directory.GetFiles(destination)); NoPartial(f);
    }

    private static async Task Seed(Fixture f)
    {
        await f.Add("one.png", bytes: false); await f.Add("two.png", bytes: false);
        var now = new DateTimeOffset(2026, 11, 1, 5, 30, 0, TimeSpan.Zero); Guid accountId;
        using (var db = f.Context())
        {
            var trades = await db.Trades.OrderBy(t => t.Id).ToArrayAsync(); var accounts = await db.TradingAccounts.OrderBy(a => a.Id).ToArrayAsync();
            accountId = accounts[0].Id; accounts[0].Name = "=Account, \"one\""; accounts[0].StartingBalance = -123.4567890123456789012345678m;
            accounts[1].Currency = "EUR"; accounts[1].IsActive = false; trades[1].PricingCurrency = "EUR";
            var setup = new TradingSetupRecord { Id = Guid.NewGuid(), Name = "Synthetic setup", CreatedAtUtc = now, UpdatedAtUtc = now };
            var mistake = new TradingMistakeRecord { Id = Guid.NewGuid(), Name = "Synthetic mistake", CreatedAtUtc = now, UpdatedAtUtc = now };
            db.TradingSetups.Add(setup); db.TradingMistakes.Add(mistake); trades[0].TradingSetupId = setup.Id;
            db.TradeMistakes.Add(new() { Id = Guid.NewGuid(), TradeId = trades[0].Id, TradingMistakeId = mistake.Id, Note = "Unicode 日本語", CreatedAtUtc = now, UpdatedAtUtc = now });
            var entry = new TradeExecutionRecord { Id = Guid.NewGuid(), TradeId = trades[0].Id, Sequence = 1, ExecutedAtUtc = now, Side = ExecutionSide.Buy, Quantity = 1, Price = 12345.67890123456789012345m, Commission = null, Fees = 0 };
            var exit = new TradeExecutionRecord { Id = Guid.NewGuid(), TradeId = trades[0].Id, Sequence = 2, ExecutedAtUtc = now.AddHours(1), Side = ExecutionSide.Sell, Quantity = 1, Price = 12350.123m, Commission = -0.01m, Fees = null };
            db.TradeExecutions.AddRange(entry, exit);
            db.TradovateImportedExecutions.Add(new() { TradeExecutionId = entry.Id, TradeId = entry.TradeId, TradingAccountIdAtImport = trades[0].TradingAccountId, BrokerSymbol = "SYN", Side = entry.Side, ExternalExecutionId = "synthetic-fill", ImportedAtUtc = now });
            db.TopstepImportedRows.Add(new() { Id = Guid.NewGuid(), TradeId = trades[0].Id, TradingAccountIdAtImport = trades[0].TradingAccountId, SourceId = "synthetic-row", EconomicFingerprint = "hash", SourceRowJson = "{\"source\":\"exact\"}", PreviewFingerprint = "hash", SourceContentSha256 = "hash", DerivedEntryExecutionId = entry.Id, DerivedExitExecutionId = exit.Id, ImportedAtUtc = now });
            await db.SaveChangesAsync();
        }
        var journals = f.Services.GetRequiredService<IDailyJournalRepository>();
        var entryJournal = (await journals.CreateAsync(new(new(2026, 11, 1), null, "Synthetic original\n日本語"))).Journal!.Entry;
        await journals.UpdateAsync(new(entryJournal.Id, entryJournal.Revision, "Synthetic current"));
        await journals.CreateAsync(new(new(2026, 11, 1), accountId, "Exact account observations"));
        Guid historicalAccount = Guid.NewGuid();
        using (var db = f.Context())
        {
            db.TradingAccounts.Add(new() { Id = historicalAccount, Name = "Deleted historical", AccountType = PersonalTradingJournal.Domain.Accounts.TradingAccountType.Demo,
                Currency = "GBP", IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now });
            await db.SaveChangesAsync();
        }
        var oldJournal = (await journals.CreateAsync(new(new(2026, 11, 1), historicalAccount, "Saved historical observations"))).Journal!.Entry;
        var packet = CoachingEvidencePacketBuilder.Build(await f.Services.GetRequiredService<IDailyReviewEvidenceReader>().GetAsync(new(new(2026, 11, 1), historicalAccount))).Packet!;
        var snapshot = CoachingAnalysisSnapshot.Create(packet, new(CoachingGenerationStatus.Success,
            new(packet.ContractVersion, packet.PacketId, new("Synthetic summary", ["calculated:day"]), [], [], [], []),
            new("Fake", "test-model", "synthetic-client", null, null, new(1, 0, 1, 2)), "Unused"), now);
        await f.Services.GetRequiredService<ICoachingAnalysisRepository>().SaveAsync(snapshot);
        await journals.DeleteAsync(new(oldJournal.Id, oldJournal.Revision));
        using var historical = f.Context(); historical.TradingAccounts.Remove(await historical.TradingAccounts.SingleAsync(a => a.Id == historicalAccount));
        await historical.SaveChangesAsync(); // Saved evidence/identity remain exactly as generated, independent of deleted sources.
    }

    [Theory]
    [InlineData("2026-03-08T06:59:59.1234567Z")]
    [InlineData("2026-03-08T07:00:00.0000000Z")]
    [InlineData("2026-11-01T05:30:00.0000000Z")]
    [InlineData("2026-11-01T06:30:00.0000000Z")]
    public async Task UtcInstantsStayExactAcrossBothDstTransitions(string instant)
    {
        await using var f = await Fixture.Create(); await f.Add("metadata.png", bytes: false);
        using (var db = f.Context())
        {
            db.TradeExecutions.Add(new() { Id = Guid.NewGuid(), TradeId = (await db.Trades.SingleAsync()).Id, Sequence = 1,
                ExecutedAtUtc = DateTimeOffset.Parse(instant, CultureInfo.InvariantCulture), Side = ExecutionSide.Buy, Quantity = 1, Price = 1 });
            await db.SaveChangesAsync();
        }
        Assert.True((await Service(f).CreateAsync(Output(f))).Succeeded); using var data = Read(f);
        Assert.Equal(instant, data.RootElement.GetProperty("tables").GetProperty("executions")[0].GetProperty("executedAtUtc").GetString());
    }

    [Fact]
    public async Task ChangedStagedDatabaseIsRejectedBeforeReadingRows()
    {
        await using var f = await Fixture.Create();
        var result = await Service(f, (phase, root) =>
        {
            if (phase == PortableExportPhase.Read) File.WriteAllBytes(Directory.GetFiles(root, "journal.db", SearchOption.AllDirectories).Single(), [0, 1]);
        }).CreateAsync(Output(f));
        Assert.Equal(PortableExportStatus.InvalidData, result.Status); Assert.False(Directory.Exists(Output(f))); NoPartial(f);
    }

    [Fact]
    public async Task SnapshotFailureCarriesSafeStatusWithoutReadingOrPublishingData()
    {
        await using var f = await Fixture.Create();
        var result = await new PortableExportService(f.Paths, new FailedSnapshot()).CreateAsync(Output(f));
        Assert.Equal(PortableExportStatus.SnapshotFailed, result.Status); Assert.Equal(DatabaseSnapshotStatus.Busy, result.SnapshotFailure);
        Assert.False(Directory.Exists(Output(f))); NoPartial(f);
    }
    private sealed class FailedSnapshot : IDatabaseSnapshotService
    {
        public Task<DatabaseSnapshotResult> CreateAsync(string directory, CancellationToken token = default) =>
            Task.FromResult(new DatabaseSnapshotResult(DatabaseSnapshotStatus.Busy, Guid.NewGuid(), DatabaseSnapshotPhase.Copy));
    }

    // Independent RFC4180 reader for assertions, including quoted newlines and doubled quotes.
    private static List<string[]> ParseCsv(string text)
    {
        var rows = new List<string[]>(); var fields = new List<string>(); var value = new StringBuilder(); bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"') { if (quoted && i + 1 < text.Length && text[i + 1] == '"') { value.Append(c); i++; } else quoted = !quoted; }
            else if (!quoted && (c == ',' || c == '\r' || c == '\n'))
            {
                fields.Add(value.ToString()); value.Clear();
                if (c != ',') { if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; rows.Add(fields.ToArray()); fields.Clear(); }
            }
            else value.Append(c);
        }
        Assert.False(quoted); Assert.Empty(fields); return rows;
    }
}
