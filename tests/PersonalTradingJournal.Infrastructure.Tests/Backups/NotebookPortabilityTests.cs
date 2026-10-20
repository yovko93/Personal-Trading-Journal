using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Backups;
using PersonalTradingJournal.Infrastructure.Notebook;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Initialization;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using PersonalTradingJournal.Infrastructure.Tests.Notebook;
using Fixture = PersonalTradingJournal.Infrastructure.Tests.Backups.BackupArchiveTests.Fixture;

namespace PersonalTradingJournal.Infrastructure.Tests.Backups;

public sealed class NotebookPortabilityTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Unreadable = " \r\n{ malformed Notebook payload — запази 😀 ";
    private const string Future = "{ \"format\":\"ptj.notebook.document\", \"version\":99, \"blocks\":[] } \r\n";
    private sealed record Seeded(Guid Active, Guid Invalid, Guid Future);

    private static async Task<Seeded> Seed(Fixture f, string image)
    {
        await f.Add(image);
        var service = f.Services.GetRequiredService<INotebookService>();
        var folder = (await service.ExecuteCatalogAsync(new CreateNotebookFolder("Café"))).Item!;
        var tag = (await service.ExecuteCatalogAsync(new CreateNotebookTag("NQ"))).Item!;
        Guid setup = Guid.NewGuid(); Guid trade; Guid screenshot;
        await using (var db = f.Context())
        {
            trade = (await db.Trades.SingleAsync()).Id; screenshot = (await db.TradeScreenshots.SingleAsync()).Id;
            var now = DateTimeOffset.UtcNow;
            // The archive fixture only needs referentially valid Trade metadata; restart additionally
            // reconstructs its economics, so supply a real execution for that stronger boundary.
            db.TradeExecutions.Add(new() { Id = Guid.NewGuid(), TradeId = trade, Sequence = 1,
                ExecutedAtUtc = now, Side = ExecutionSide.Buy, Quantity = 1, Price = 100, Commission = null, Fees = 0 });
            db.TradingSetups.Add(new() { Id = setup, Name = "Synthetic setup", IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now });
            db.DailyJournals.Add(new() { Id = Guid.NewGuid(), TradingDate = new(2026, 10, 20), Text = "Existing synthetic journal",
                Revision = 1, IsDraft = true, CreatedAtUtc = now, UpdatedAtUtc = now });
            await db.SaveChangesAsync();
        }
        var notes = new List<NotebookNoteSnapshot>();
        foreach (string title in new[] { "Active", "Archived", "Trash from archive", "Trash from active", "Unreadable", "Future" })
        {
            var note = NotebookFixture.Success(await service.ExecuteAsync(new CreateNotebookNote(folder.Id, title)));
            note = NotebookFixture.Success(await service.ExecuteAsync(new UpdateNotebookContent(note.Id, note.Version, NotebookCodecTests.Rich(), [])));
            note = NotebookFixture.Success(await service.ExecuteAsync(new AssignNotebookTag(note.Id, note.Version, tag.Id)));
            note = NotebookFixture.Success(await service.ExecuteAsync(new SetNotebookPin(note.Id, note.Version, true)));
            note = NotebookFixture.Success(await service.ExecuteAsync(new AddNotebookLink(note.Id, note.Version, new(NotebookTargetKind.Trade, trade, "Synthetic trade"))));
            note = NotebookFixture.Success(await service.ExecuteAsync(new AddNotebookLink(note.Id, note.Version, new(NotebookTargetKind.TradingSetup, setup, "Synthetic setup"))));
            if (title is "Archived" or "Trash from archive") note = NotebookFixture.Success(await service.ExecuteAsync(new ArchiveNotebookNote(note.Id, note.Version)));
            if (title.StartsWith("Trash", StringComparison.Ordinal)) note = NotebookFixture.Success(await service.ExecuteAsync(new TrashNotebookNote(note.Id, note.Version)));
            notes.Add(note);
        }
        await using (var db = f.Context())
        {
            // Legacy owned metadata fixture: complete archives now require its exact original bytes too.
            byte[] ownedBytes = [7, 6, 5, 4, 3, 2, 1];
            await File.WriteAllBytesAsync(Path.Combine(f.Paths.ScreenshotsDirectory, "notebook-owned.png"), ownedBytes);
            var owned = NotebookImage.Owned(Guid.NewGuid(), 0, "notebook-owned.png", ownedBytes.Length,
                Convert.ToHexString(SHA256.HashData(ownedBytes)), "Owned metadata", "Alt");
            var reference = NotebookImage.TradeReference(Guid.NewGuid(), 1, trade, screenshot, "Trade image");
            var content = new NotebookDocument([NotebookNode.Image(Guid.NewGuid(), owned.Id, owned.Caption, owned.AlternativeText),
                NotebookNode.Image(Guid.NewGuid(), reference.Id, reference.Caption, reference.AlternativeText)]);
            var n = notes[0];
            var domain = NotebookNote.Rehydrate(n.Id, n.Title, n.Document, n.FolderId, n.State, n.IsPinned, n.RestorePoint,
                n.TagIds, n.Links, n.Images, n.Version, n.CreatedAtUtc, n.UpdatedAtUtc);
            domain.UpdateContent(content, [owned, reference], DateTimeOffset.UtcNow);
            NotebookMapper.Apply(await db.NotebookNotes.SingleAsync(x => x.Id == n.Id), domain, new NotebookJsonCodec().Encode(content));
            db.NotebookImages.AddRange(domain.Images.Select(x => NotebookMapper.Image(n.Id, x)));
            var invalid = await db.NotebookNotes.SingleAsync(x => x.Id == notes[4].Id);
            invalid.DocumentJson = Unreadable;
            var future = await db.NotebookNotes.SingleAsync(x => x.Id == notes[5].Id);
            future.DocumentJson = Future; future.ContentVersion = 99;
            db.NotebookRecoveryDrafts.AddRange(
                new NotebookRecoveryRecord { Id = Guid.NewGuid(), NoteId = n.Id, ExpectedVersion = n.Version, Revision = 7, Title = "Unfinished recovery",
                    DocumentJson = new NotebookJsonCodec().Encode(NotebookCodecTests.Rich()).Json, CheckpointedAtUtc = DateTimeOffset.UtcNow },
                new NotebookRecoveryRecord { Id = Guid.NewGuid(), NoteId = notes[4].Id, ExpectedVersion = 1, Revision = 9, Title = "Unreadable recovery",
                    DocumentJson = Unreadable, CheckpointedAtUtc = DateTimeOffset.UtcNow },
                new NotebookRecoveryRecord { Id = Guid.NewGuid(), NoteId = notes[5].Id, ExpectedVersion = 1, Revision = 2, Title = "Future recovery",
                    EnvelopeVersion = 99, DocumentJson = Future, CheckpointedAtUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        return new(notes[0].Id, notes[4].Id, notes[5].Id);
    }

    private static async Task<JsonElement> Export(Fixture f)
    {
        string output = Path.Combine(f.Root, "export-" + Guid.NewGuid().ToString("N"));
        var result = await new PortableExportService(f.Paths, new SqliteDatabaseSnapshotService(f.Paths)).CreateAsync(output);
        Assert.True(result.Succeeded, result.ToString()); Assert.False(result.CleanupFailed);
        using var json = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, PortableExportContract.DataFile)));
        using var inventory = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(output, PortableExportContract.InventoryFile)));
        Assert.Equal(17, json.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(17, inventory.RootElement.GetProperty("version").GetInt32());
        Assert.False(json.RootElement.GetProperty("restorableBackup").GetBoolean());
        Assert.Equal("metadata-only", json.RootElement.GetProperty("screenshotContent").GetString());
        var tables = json.RootElement.GetProperty("tables");
        foreach (var table in PortableExportSchema.Tables)
            Assert.Equal(tables.GetProperty(table.Name).GetArrayLength(), inventory.RootElement.GetProperty("rows").GetProperty(table.Name).GetInt64());
        Assert.Equal(result.Rows, tables.EnumerateObject().Sum(x => (long)x.Value.GetArrayLength()));
        return tables.Clone();
    }
    private static void EqualRows<T>(IEnumerable<T> expected, JsonElement rows)
    {
        // Deserialize exported scalar fields into the actual persistence record types, checking every field.
        var actual = rows.Deserialize<T[]>(Json)!;
        Assert.Equal(expected.Select(x => JsonSerializer.Serialize(x, Json)), actual.Select(x => JsonSerializer.Serialize(x, Json)));
    }

    [Fact]
    public async Task ExportIncludesEveryNotebookFieldExactlyWithoutDecodingBodiesOrReadingOwnedImages()
    {
        await using var f = await Fixture.Create(); var seeded = await Seed(f, "trade.png");
        var tables = await Export(f);
        await using var db = f.Context();
        EqualRows(await db.NotebookNotes.OrderBy(x => x.Id).ToArrayAsync(), tables.GetProperty("notebookNotes"));
        EqualRows(await db.NotebookRecoveryDrafts.OrderBy(x => x.Id).ToArrayAsync(), tables.GetProperty("notebookRecoveryDrafts"));
        EqualRows(await db.NotebookFolders.OrderBy(x => x.Id).ToArrayAsync(), tables.GetProperty("notebookFolders"));
        EqualRows(await db.NotebookTags.OrderBy(x => x.Id).ToArrayAsync(), tables.GetProperty("notebookTags"));
        EqualRows(await db.NotebookTagAssignments.OrderBy(x => x.NoteId).ThenBy(x => x.TagId).ToArrayAsync(), tables.GetProperty("notebookTagAssignments"));
        EqualRows(await db.NotebookLinks.OrderBy(x => x.NoteId).ThenBy(x => x.Kind).ThenBy(x => x.TargetId).ToArrayAsync(), tables.GetProperty("notebookLinks"));
        EqualRows(await db.NotebookImages.OrderBy(x => x.NoteId).ThenBy(x => x.Position).ThenBy(x => x.Id).ToArrayAsync(), tables.GetProperty("notebookImages"));
        Assert.Equal(Unreadable, tables.GetProperty("notebookNotes").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == seeded.Invalid).GetProperty("documentJson").GetString());
        Assert.Equal(Future, tables.GetProperty("notebookNotes").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == seeded.Future).GetProperty("documentJson").GetString());
        Assert.Equal(new byte[] { 7, 6, 5, 4, 3, 2, 1 }, await File.ReadAllBytesAsync(Path.Combine(f.Paths.ScreenshotsDirectory, "notebook-owned.png")));
        Assert.Equal(tables.GetRawText(), (await Export(f)).GetRawText());
    }

    [Theory]
    [InlineData("success")]
    [InlineData("cancel")]
    [InlineData("failure")]
    [InlineData("interrupted")]
    public async Task ArchivePreflightOfflineRestoreAndRecoveryPreserveNotebookAndExistingRecords(string mode)
    {
        await using var source = await Fixture.Create(); var incoming = await Seed(source, "incoming.png");
        var incomingTables = await Export(source);
        Assert.True((await source.Service().CreateAsync(source.Output)).Succeeded);
        var preflight = await new RestorePreflightService().InspectAsync(source.Output, source.Root);
        Assert.True(preflight.Validated, preflight.ToString());
        Assert.Equal(BackupSchemaCompatibility.Exact, preflight.Compatibility);
        var request = new JournalRestoreRequest(source.Output, preflight.Summary!.ArchiveSha256);
        await using var target = await Fixture.Create(); var original = await Seed(target, "original.png");
        var originalTables = await Export(target);
        await target.Services.DisposeAsync(); JournalDataSession.ClearOwnedPools(target.Paths);
        using var cts = new CancellationTokenSource();
        var restore = new JournalRestoreService(target.Paths, (phase, _) =>
        {
            if (mode == "cancel" && phase == JournalRestorePhase.Prepared) cts.Cancel();
            if (phase != JournalRestorePhase.IncomingInstalled) return;
            if (mode == "failure") throw new IOException("Synthetic switch failure");
            if (mode == "interrupted") throw new RestoreInterruption();
        });
        JournalRestoreResult result;
        if (mode == "interrupted")
        {
            await Assert.ThrowsAsync<RestoreInterruption>(() => restore.RestoreAsync(request));
            result = await new JournalRestoreService(target.Paths).RecoverInterruptedAsync();
        }
        else result = await restore.RestoreAsync(request, cts.Token);
        Assert.Equal(mode switch { "success" => JournalRestoreStatus.Restored, "cancel" => JournalRestoreStatus.Cancelled, _ => JournalRestoreStatus.RecoveredOriginal }, result.Status);
        Assert.False(result.CleanupFailed); Assert.False(result.NormalUseBlocked);
        Assert.Equal((mode == "success" ? incomingTables : originalTables).GetRawText(), (await Export(target)).GetRawText());
        Assert.False(File.Exists(JournalDataSession.StatePath(target.Paths)));
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(Path.Combine(target.Paths.ScreenshotsDirectory, mode == "success" ? "incoming.png" : "original.png")));
        if (result.RecoveryArchivePath is not null)
            Assert.True((await new RestorePreflightService().InspectAsync(result.RecoveryArchivePath, target.Root)).Validated);
        await using var restarted = new ServiceCollection().AddPersistence(target.Paths).BuildServiceProvider();
        await restarted.GetRequiredService<JournalDatabaseInitializer>().InitializeAsync();
        var service = restarted.GetRequiredService<INotebookService>();
        var selected = mode == "success" ? incoming : original;
        Assert.Equal(2, (await service.LoadAsync(selected.Active)).Note!.Images.Length);
        var invalid = await service.LoadAsync(selected.Invalid);
        Assert.Equal(NotebookLoadStatus.InvalidContent, invalid.Status); Assert.Equal(Unreadable, invalid.OriginalContent);
        var future = await service.LoadAsync(selected.Future);
        Assert.Equal(NotebookLoadStatus.UnsupportedContent, future.Status); Assert.Equal(Future, future.OriginalContent);
        Assert.Equal(NotebookWriteStatus.Deferred, (await service.ExecuteAsync(new DeleteNotebookNote(selected.Active, 1, true))).Status);
    }

    [Theory]
    [InlineData("pre-notebook", BackupValidationCode.UnsupportedDatabaseSchema)]
    [InlineData("future-schema", BackupValidationCode.UnsupportedDatabaseSchema)]
    [InlineData("future-archive", BackupValidationCode.UnsupportedArchiveVersion)]
    public async Task UnsupportedArchivesNeverReplacePopulatedNotebook(string kind, BackupValidationCode expected)
    {
        await using var source = await Fixture.Create();
        Assert.True((await source.Service().CreateAsync(source.Output)).Succeeded);
        var entries = new Dictionary<string, byte[]>();
        using (var zip = ZipFile.OpenRead(source.Output))
            foreach (var entry in zip.Entries) { using var input = entry.Open(); using var buffer = new MemoryStream(); input.CopyTo(buffer); entries.Add(entry.FullName, buffer.ToArray()); }
        var manifest = JsonSerializer.Deserialize<BackupManifest>(entries[BackupArchiveContract.ManifestPath], BackupArchiveContract.JsonOptions)!;
        if (kind == "pre-notebook")
        {
            await using var previous = await NotebookFixture.Create("20261019171746_AddDailyTradingPrinciples");
            // Real previous-schema SQLite, not just a falsely relabelled current database.
            using (var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = previous.DatabasePath, Pooling = false }.ToString()))
            { db.Open(); using var command = db.CreateCommand(); command.CommandText = "PRAGMA journal_mode=DELETE;"; command.ExecuteNonQuery(); }
            byte[] bytes = await File.ReadAllBytesAsync(previous.DatabasePath);
            entries[BackupArchiveContract.DatabasePath] = bytes;
            manifest = manifest with { DatabaseSchema = manifest.DatabaseSchema with { AppliedMigrations = manifest.DatabaseSchema.AppliedMigrations.RemoveAt(manifest.DatabaseSchema.AppliedMigrations.Length - 1) },
                Files = [new(BackupArchiveContract.DatabasePath, BackupFileKind.Database, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)))] };
        }
        else if (kind == "future-schema") manifest = manifest with { DatabaseSchema = manifest.DatabaseSchema with { AppliedMigrations = manifest.DatabaseSchema.AppliedMigrations.Add("20990101000000_Future") } };
        else manifest = manifest with { ArchiveVersion = 2 };
        entries[BackupArchiveContract.ManifestPath] = JsonSerializer.SerializeToUtf8Bytes(manifest, BackupArchiveContract.JsonOptions);
        using (var output = new FileStream(source.Output, FileMode.Create))
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create))
            foreach (var entry in entries) { using var bytes = zip.CreateEntry(entry.Key).Open(); bytes.Write(entry.Value); }
        var preflight = await new RestorePreflightService().InspectAsync(source.Output, source.Root);
        Assert.Equal(expected, preflight.Failure); Assert.False(preflight.Validated); Assert.False(preflight.CleanupFailed);
        if (kind == "pre-notebook") Assert.Equal(BackupSchemaCompatibility.RequiresStagedMigration, preflight.Compatibility);
        await using var target = await Fixture.Create(); await Seed(target, "original.png"); var original = await Export(target);
        await target.Services.DisposeAsync(); JournalDataSession.ClearOwnedPools(target.Paths);
        var hash = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(source.Output)));
        var result = await new JournalRestoreService(target.Paths).RestoreAsync(new(source.Output, hash));
        Assert.Equal(JournalRestoreStatus.RejectedArchive, result.Status); Assert.Equal(expected, result.ValidationFailure);
        Assert.Equal(original.GetRawText(), (await Export(target)).GetRawText());
    }
}
