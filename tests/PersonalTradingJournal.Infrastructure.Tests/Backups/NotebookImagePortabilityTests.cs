using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Backups;
using PersonalTradingJournal.Infrastructure.Notebook;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Tests.Notebook;
using Fixture = PersonalTradingJournal.Infrastructure.Tests.Backups.BackupArchiveTests.Fixture;

namespace PersonalTradingJournal.Infrastructure.Tests.Backups;

public sealed class NotebookImagePortabilityTests
{
    private static (NotebookImageService Images, NotebookService Notes) Operations(Fixture f)
    {
        var factory = f.Services.GetRequiredService<IDbContextFactory<JournalDbContext>>();
        var codec = new NotebookJsonCodec(); var files = new NotebookImageFiles(f.Paths, factory, new NotebookImageTests.Probe());
        var notes = new NotebookService(factory, codec, imageFiles: files);
        return (new(factory, notes, codec, files), notes);
    }
    private static async Task<NotebookNoteSnapshot> Import(Fixture f)
    {
        var s = Operations(f); var note = NotebookFixture.Success(await s.Notes.ExecuteAsync(new CreateNotebookNote()));
        var id = Guid.NewGuid(); using var input = new MemoryStream(NotebookImageTests.Png);
        return NotebookFixture.Success(await s.Images.ImportAsync(new(note.Id, note.Version, id, NotebookImageTests.Append(note.Document, id)), input));
    }
    [Fact]
    public async Task RemovedImageLedgerRecoveryAndOriginalRoundTripInCompleteArchiveAndExport()
    {
        await using var source = await Fixture.Create(); var s = Operations(source); var note = await Import(source);
        var image = note.Images[0];
        var recovery = source.Services.GetRequiredService<INotebookRecoveryStore>();
        Assert.Equal(NotebookWriteStatus.Success, await recovery.PutAsync(new(Guid.NewGuid(), note.Id, note.Version, 1, "Recover image", note.Document, DateTimeOffset.UtcNow), 0));
        note = NotebookFixture.Success(await s.Notes.ExecuteAsync(new RemoveNotebookImage(note.Id, note.Version, image.Id, NotebookDocument.Empty, [])));
        Assert.Empty(note.Images);
        await source.Services.GetRequiredService<ITradeScreenshotFileStorage>().DeleteIfExistsAsync(image.StorageKey!);
        Assert.True(File.Exists(Path.Combine(source.Paths.ScreenshotsDirectory, image.StorageKey!)));
        var exportPath = Path.Combine(source.Root, "ledger-export");
        Assert.True((await source.Services.GetRequiredService<IPortableExportService>().CreateAsync(exportPath)).Succeeded);
        using (var json = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(exportPath, PortableExportContract.DataFile))))
        {
            Assert.Equal(17, json.RootElement.GetProperty("version").GetInt32());
            var row = Assert.Single(json.RootElement.GetProperty("tables").GetProperty("notebookOwnedImageFiles").EnumerateArray());
            Assert.Equal(image.Id, row.GetProperty("id").GetGuid()); Assert.Equal(note.Id, row.GetProperty("noteId").GetGuid());
            Assert.Equal(image.StorageKey, row.GetProperty("storageKey").GetString()); Assert.Equal(image.SizeBytes, row.GetProperty("sizeBytes").GetInt64());
            Assert.Equal(image.Sha256, row.GetProperty("sha256").GetString());
        }
        Assert.True((await source.Service().CreateAsync(source.Output)).Succeeded);
        using (var zip = ZipFile.OpenRead(source.Output))
        {
            using var input = zip.GetEntry(BackupArchiveContract.ScreenshotsPrefix + image.StorageKey)!.Open();
            using var buffer = new MemoryStream(); await input.CopyToAsync(buffer); Assert.Equal(NotebookImageTests.Png, buffer.ToArray());
        }
        var preflight = await new RestorePreflightService().InspectAsync(source.Output, source.Root); Assert.True(preflight.Validated);
        await using var target = await Fixture.Create(); await target.Services.DisposeAsync(); JournalDataSession.ClearOwnedPools(target.Paths);
        var restored = await new JournalRestoreService(target.Paths).RestoreAsync(new(source.Output, preflight.Summary!.ArchiveSha256));
        Assert.Equal(JournalRestoreStatus.Restored, restored.Status); Assert.False(restored.CleanupFailed);
        await using var fresh = new ServiceCollection().AddPersistence(target.Paths).BuildServiceProvider();
        var content = await fresh.GetRequiredService<INotebookImages>().ReadOriginalAsync(note.Id, image.Id);
        Assert.Equal(NotebookImageStatus.Success, content.Status); Assert.Equal(NotebookImageTests.Png, content.Original);
        var item = Assert.Single((await fresh.GetRequiredService<INotebookRecoveryStore>().ListAsync()).Items);
        Assert.Equal(image.Id, Assert.Single((await fresh.GetRequiredService<INotebookRecoveryStore>().LoadAsync(item.Id)).Draft!.Document.ImageNodes).ImageId);
        Assert.Empty((await fresh.GetRequiredService<INotebookService>().LoadAsync(note.Id)).Note!.Images);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrChangedOwnedOriginalCannotProduceAnApparentlyCompleteArchive(bool missing)
    {
        await using var f = await Fixture.Create(); var note = await Import(f); var path = Path.Combine(f.Paths.ScreenshotsDirectory, note.Images[0].StorageKey!);
        if (missing) File.Delete(path); else await File.WriteAllBytesAsync(path, new byte[NotebookImageTests.Png.Length]);
        var result = await f.Service().CreateAsync(f.Output);
        Assert.Equal(BackupValidationCode.DatabaseAttachmentMismatch, result.Failure); Assert.False(result.Succeeded); f.AssertNoOutput();
    }
    [Fact]
    public async Task TradeReferenceRemovalNeverReadsOrDeletesTradeOriginal()
    {
        await using var f = await Fixture.Create(); await f.Add("trade-original.png"); var s = Operations(f);
        var note = NotebookFixture.Success(await s.Notes.ExecuteAsync(new CreateNotebookNote()));
        Guid id = Guid.NewGuid();
        await using (var db = f.Context())
        {
            var screenshot = await db.TradeScreenshots.SingleAsync();
            var image = NotebookImage.TradeReference(id, 0, screenshot.TradeId, screenshot.Id);
            var doc = new NotebookDocument([NotebookNode.Image(Guid.NewGuid(), id)]);
            var domain = NotebookNote.Rehydrate(note.Id, note.Title, note.Document, note.FolderId, note.State, note.IsPinned, note.RestorePoint,
                note.TagIds, note.Links, note.Images, note.Version, note.CreatedAtUtc, note.UpdatedAtUtc);
            domain.UpdateContent(doc, [image], DateTimeOffset.UtcNow);
            NotebookMapper.Apply(await db.NotebookNotes.SingleAsync(), domain, new NotebookJsonCodec().Encode(doc));
            db.NotebookImages.Add(NotebookMapper.Image(note.Id, image)); await db.SaveChangesAsync();
        }
        Assert.Equal(NotebookImageStatus.Unsupported, (await s.Images.ReadOriginalAsync(note.Id, id)).Status);
        note = (await s.Notes.LoadAsync(note.Id)).Note!;
        NotebookFixture.Success(await s.Notes.ExecuteAsync(new RemoveNotebookImage(note.Id, note.Version, id, NotebookDocument.Empty, [])));
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(Path.Combine(f.Paths.ScreenshotsDirectory, "trade-original.png")));
        await using var verify = f.Context(); Assert.Single(await verify.TradeScreenshots.ToArrayAsync()); Assert.Empty(await verify.NotebookOwnedImageFiles.ToArrayAsync());
    }
}
