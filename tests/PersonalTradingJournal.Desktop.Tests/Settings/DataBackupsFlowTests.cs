using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Desktop.ViewModels.Settings;
using PersonalTradingJournal.Infrastructure.Backups;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Initialization;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using PersonalTradingJournal.Infrastructure.Storage;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Desktop.Screenshots;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Settings;

public sealed class DataBackupsFlowTests
{
    [Fact]
    public async Task IsolatedSettingsWorkflowUsesRealSnapshotPreflightAndExclusiveRecoveryThenFreshRead()
    {
        string root = Path.Combine(Path.GetTempPath(), "ptj-ui-" + Guid.NewGuid().ToString("N"));
        var paths = new LocalApplicationPaths(root); paths.EnsureDirectoriesExist();
        using var services = new ServiceCollection().AddPersistence(paths)
            .AddSingleton<INotebookImageValidator, WpfNotebookImageValidator>().BuildServiceProvider();
        string archive = Path.Combine(root, "synthetic.ptjbackup");
        var f = new BackupUiFixture { Target = archive }; f.Dialogs.ConfirmationResult = true;
        try
        {
            await services.GetRequiredService<JournalDatabaseInitializer>().InitializeAsync();
            var factory = services.GetRequiredService<IDbContextFactory<JournalDbContext>>();
            Guid id = Guid.NewGuid();
            await using (var db = await factory.CreateDbContextAsync())
            {
                db.TradingAccounts.Add(new TradingAccountRecord { Id = id, Name = "Synthetic saved Account", Currency = "USD",
                    AccountType = TradingAccountType.Personal, IsActive = true, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow });
                await db.SaveChangesAsync();
            }
            await SeedRelatedData(services, paths, id);
            var notes = services.GetRequiredService<INotebookService>();
            var note = (await notes.ExecuteAsync(new CreateNotebookNote(null, "Notebook backup", NotebookTemplate.MarketStudy))).Note!;
            var imageId = Guid.NewGuid();
            var document = new NotebookDocument([.. note.Document.Blocks, NotebookNode.Image(Guid.NewGuid(), imageId, "Saved original")]);
            using (var input = new MemoryStream(NotebookOriginalBytes))
            {
                var imported = await services.GetRequiredService<INotebookImages>().ImportAsync(new(note.Id, note.Version, imageId, document), input);
                Assert.Equal(NotebookWriteStatus.Success, imported.Status); note = imported.Note!;
            }
            var draftId = Guid.NewGuid();
            Assert.Equal(NotebookWriteStatus.Success, await services.GetRequiredService<INotebookRecoveryStore>().PutAsync(
                new(draftId, note.Id, note.Version, 1, "Unsaved recovery title", note.Document, DateTimeOffset.UtcNow), 0));
            // Advance the canonical note after its checkpoint: fresh Desktop recovery must detect this conflict.
            note = (await notes.ExecuteAsync(new UpdateNotebookTitle(note.Id, note.Version, "Newer saved title"))).Note!;
            var vm = new DataBackupsViewModel(services.GetRequiredService<IBackupArchiveService>(), services.GetRequiredService<IPortableExportService>(),
                services.GetRequiredService<IRestorePreflightService>(), f, f.Dialogs, root);
            await vm.CreateBackupCommand.ExecuteAsync(null); Assert.Equal(archive, vm.OutputLocation); Assert.True(File.Exists(archive));
            VerifyArchive(archive);
            f.Target = Path.Combine(root, "export"); await vm.ExportCommand.ExecuteAsync(null);
            Assert.Equal(6, Directory.GetFiles(f.Target).Length); Assert.Contains("Not a restorable backup", vm.Status);
            string originalTables = InspectExport(f.Target, id);
            f.Target = archive; await vm.InspectCommand.ExecuteAsync(null); Assert.True(vm.HasPreview); vm.RestoreCommand.Execute(null);
            var request = Assert.IsType<JournalRestoreRequest>(f.Request);
            await using (var db = await factory.CreateDbContextAsync())
            {
                (await db.TradingAccounts.SingleAsync()).Name = "Later change";
                await db.SaveChangesAsync();
            }
            var journals = services.GetRequiredService<IDailyJournalRepository>();
            var current = (await journals.GetAsync(new(2026, 11, 1), id))!.Entry;
            Assert.NotNull((await journals.UpdateAsync(new(current.Id, current.Revision, "Later synthetic dataset",
                ReopenCompleted: true))).Journal);
            await File.WriteAllBytesAsync(Path.Combine(paths.ScreenshotsDirectory, "synthetic.png"), [9, 8, 7]);
            var restore = new JournalRestoreService(paths);
            Assert.Equal(JournalRestoreStatus.Blocked, (await restore.RestoreAsync(request)).Status);
            services.Dispose(); // Mirrors process teardown: every normal session/context is gone first.
            var maintenance = new MaintenanceViewModel(restore, _ => Task.CompletedTask, () => { });
            await maintenance.RunAsync(request);
            Assert.StartsWith("Restore verified.", maintenance.Status); Assert.True(maintenance.MayReopen);
            Assert.True(File.Exists(maintenance.RecoveryLocation)); Assert.True(Directory.Exists(maintenance.RetainedLocation));
            VerifyArchive(maintenance.RecoveryLocation);
            var recoveryCheck = await new RestorePreflightService().InspectAsync(maintenance.RecoveryLocation, root);
            Assert.True(recoveryCheck.Validated);
            Assert.Equal(2, recoveryCheck.Summary!.JournalCount);
            Assert.Equal(4, recoveryCheck.Summary.JournalRevisionCount);
            Assert.Equal(1, recoveryCheck.Summary.AnalysisCount);
            string recoveryDatabase = Path.Combine(root, "verified-recovery.db");
            using (var recovery = ZipFile.OpenRead(maintenance.RecoveryLocation))
            {
                recovery.GetEntry(BackupArchiveContract.DatabasePath)!.ExtractToFile(recoveryDatabase);
            }
            using (var recovery = ZipFile.OpenRead(maintenance.RecoveryLocation))
            using (var bytes = recovery.GetEntry(BackupArchiveContract.ScreenshotsPrefix + "synthetic.png")!.Open())
            using (var copy = new MemoryStream())
            { await bytes.CopyToAsync(copy); Assert.Equal(new byte[] { 9, 8, 7 }, copy.ToArray()); }
            await using var restarted = new ServiceCollection().AddPersistence(paths).BuildServiceProvider();
            await restarted.GetRequiredService<JournalDatabaseInitializer>().InitializeAsync();
            string restoredExport = Path.Combine(root, "restored-export");
            Assert.True((await restarted.GetRequiredService<IPortableExportService>().CreateAsync(restoredExport)).Succeeded);
            Assert.Equal(originalTables, InspectExport(restoredExport, id)); // Every logical value, not only counts.
            using (var notebook = new NotebookWorkspaceViewModel(restarted.GetRequiredService<INotebookService>(), new FakeDialogService(),
                codec: restarted.GetRequiredService<INotebookContentCodec>(), recovery: restarted.GetRequiredService<INotebookRecoveryStore>(),
                images: restarted.GetRequiredService<INotebookImages>()))
            {
                await notebook.ActivateAsync();
                Assert.Equal(note.Id, Assert.Single(notebook.Notes).Id);
                var draft = Assert.Single(notebook.RecoveryDrafts); Assert.Equal(draftId, draft.Id);
                await notebook.RecoverCommand.ExecuteAsync(draft);
                Assert.Equal("Unsaved recovery title", notebook.DraftTitle);
                Assert.Contains("conflict", notebook.SaveState, StringComparison.OrdinalIgnoreCase);
                await notebook.SaveCommand.ExecuteAsync(null); // A conflict may checkpoint the draft, but cannot overwrite canonical content.
                Assert.Equal("Newer saved title", (await restarted.GetRequiredService<INotebookService>().LoadAsync(note.Id)).Note!.Title);
                var original = await restarted.GetRequiredService<INotebookImages>().ReadOriginalAsync(note.Id, imageId);
                Assert.Equal(NotebookImageStatus.Success, original.Status); Assert.Equal(NotebookOriginalBytes, original.Original);
            }
            Assert.Equal(ScreenshotBytes, await File.ReadAllBytesAsync(Path.Combine(paths.ScreenshotsDirectory, "synthetic.png")));
            await using var fresh = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = paths.DatabasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            await fresh.OpenAsync(); using var query = fresh.CreateCommand(); query.CommandText = "SELECT Name FROM TradingAccounts";
            Assert.Equal("Synthetic saved Account", await query.ExecuteScalarAsync());
            await using var previous = new SqliteConnection(new SqliteConnectionStringBuilder {
                DataSource = recoveryDatabase,
                Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            await previous.OpenAsync(); using var oldQuery = previous.CreateCommand(); oldQuery.CommandText = "SELECT Name FROM TradingAccounts";
            Assert.Equal("Later change", await oldQuery.ExecuteScalarAsync());
        }
        finally
        {
            services.Dispose();
            using (var pool = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = paths.DatabasePath, ForeignKeys = true }.ToString())) SqliteConnection.ClearPool(pool);
            Directory.Delete(root, true);
        }
    }

    private static readonly byte[] ScreenshotBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aCioAAAAASUVORK5CYII=");
    // The managed import uses the decoder-tested PNG fixture; the historical Trade bytes remain unchanged.
    private static readonly byte[] NotebookOriginalBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    private static async Task SeedRelatedData(ServiceProvider services, LocalApplicationPaths paths, Guid account)
    {
        var now = new DateTimeOffset(2026, 11, 1, 6, 30, 0, TimeSpan.Zero); // Repeated NY hour, preserved as an instant.
        var day = new DateOnly(2026, 11, 1);
        await using (var db = await services.GetRequiredService<IDbContextFactory<JournalDbContext>>().CreateDbContextAsync())
        {
            Guid instrument = Guid.NewGuid(), trade = Guid.NewGuid();
            db.Instruments.Add(new InstrumentRecord { Id = instrument, Symbol = "SYN", DisplayName = "Synthetic only",
                AssetClass = AssetClass.Futures, Exchange = "TEST", Currency = "USD", TickSize = .25m, TickValue = .5m,
                IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now });
            db.Trades.Add(new TradeRecord { Id = trade, TradingAccountId = account, InstrumentId = instrument,
                PricingPointValue = 2, PricingCurrency = "USD", CreatedAtUtc = now, UpdatedAtUtc = now });
            db.TradeExecutions.AddRange(new TradeExecutionRecord { Id = Guid.NewGuid(), TradeId = trade, Sequence = 1,
                ExecutedAtUtc = now.AddHours(-1), Side = ExecutionSide.Buy, Quantity = 1, Price = 100.25m, Commission = null, Fees = 0 },
                new TradeExecutionRecord { Id = Guid.NewGuid(), TradeId = trade, Sequence = 2,
                    ExecutedAtUtc = now, Side = ExecutionSide.Sell, Quantity = 1, Price = 110.75m, Commission = .25m, Fees = null });
            db.TradeScreenshots.Add(new TradeScreenshotRecord { Id = Guid.NewGuid(), TradeId = trade, StorageKey = "synthetic.png",
                FileName = "Synthetic screenshot.png", CreatedAtUtc = now, UpdatedAtUtc = now });
            await db.SaveChangesAsync();
        }
        await File.WriteAllBytesAsync(Path.Combine(paths.ScreenshotsDirectory, "synthetic.png"), ScreenshotBytes);
        var journals = services.GetRequiredService<IDailyJournalRepository>();
        var first = (await journals.CreateAsync(new(day, account, "=Synthetic, 日本語\nsecond line"))).Journal!.Entry;
        Assert.NotNull((await journals.UpdateAsync(new(first.Id, first.Revision, "=Synthetic, 日本語\nsecond line", false))).Journal);
        Assert.NotNull((await journals.CreateAsync(new(day, null, "Synthetic All accounts draft"))).Journal);
        var packet = CoachingEvidencePacketBuilder.Build(await services.GetRequiredService<IDailyReviewEvidenceReader>().GetAsync(new(day))).Packet!;
        var snapshot = CoachingAnalysisSnapshot.Create(packet, new(CoachingGenerationStatus.Success,
            new(packet.ContractVersion, packet.PacketId, new("Synthetic summary", ["calculated:day"]), [], [], [], []),
            new("Fake", "test-model", "synthetic-client", null, null, new(1, 0, 1, 2)), "Unused"), now);
        await services.GetRequiredService<ICoachingAnalysisRepository>().SaveAsync(snapshot); // No provider call.
    }

    private static void VerifyArchive(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        using var json = zip.GetEntry(BackupArchiveContract.ManifestPath)!.Open();
        var manifest = JsonSerializer.Deserialize<BackupManifest>(json, BackupArchiveContract.JsonOptions)!;
        Assert.Equal(BackupArchiveContract.Version, manifest.ArchiveVersion);
        Assert.Equal(TimeSpan.Zero, manifest.CreatedAtUtc.Offset);
        Assert.Equal(4, zip.Entries.Count); // Manifest, DB, Trade screenshot and Notebook original. No credential/log/cache payloads.
        Assert.Equal(3, manifest.Files.Length);
        foreach (var file in manifest.Files)
        {
            var entry = zip.GetEntry(file.Path)!; Assert.Equal(file.SizeBytes, entry.Length);
            using var input = entry.Open(); Assert.Equal(file.Sha256, Convert.ToHexStringLower(SHA256.HashData(input)));
        }
    }

    private static string InspectExport(string directory, Guid account)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "journal.json")));
        Assert.False(document.RootElement.GetProperty("restorableBackup").GetBoolean());
        Assert.Equal("metadata-only", document.RootElement.GetProperty("screenshotContent").GetString());
        var tables = document.RootElement.GetProperty("tables");
        Assert.Equal(account, tables.GetProperty("accounts")[0].GetProperty("id").GetGuid());
        Assert.Equal("USD", tables.GetProperty("trades")[0].GetProperty("pricingCurrency").GetString());
        Assert.Equal(2, tables.GetProperty("executions").GetArrayLength());
        Assert.Contains(tables.GetProperty("executions").EnumerateArray(), e => e.GetProperty("commission").ValueKind == JsonValueKind.Null);
        Assert.Contains(tables.GetProperty("executions").EnumerateArray(), e => e.GetProperty("fees").ValueKind == JsonValueKind.Null);
        Assert.All(tables.GetProperty("executions").EnumerateArray(), e => Assert.EndsWith("Z", e.GetProperty("executedAtUtc").GetString()));
        Assert.Equal(2, tables.GetProperty("journals").GetArrayLength());
        Assert.Equal(3, tables.GetProperty("journalRevisions").GetArrayLength());
        Assert.Equal(1, tables.GetProperty("analyses").GetArrayLength());
        Assert.Equal(1, tables.GetProperty("screenshots").GetArrayLength());
        Assert.All(tables.GetProperty("journals").EnumerateArray(), j => Assert.Equal("2026-11-01", j.GetProperty("tradingDateNewYork").GetString()));
        Assert.Equal(4, Directory.GetFiles(directory, "*.csv").Length);
        foreach (string csv in new[] { "accounts", "trades", "executions", "journals" })
            Assert.Contains("\"id\"", File.ReadAllText(Path.Combine(directory, csv + ".csv")));
        Assert.Contains("'=Synthetic, 日本語\nsecond line", File.ReadAllText(Path.Combine(directory, "journals.csv")));
        Assert.Empty(Directory.GetDirectories(directory));
        return tables.GetRawText();
    }
}
