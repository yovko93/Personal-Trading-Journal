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
    [Fact]
    public async Task DuplicateAndDeletePreserveExternalTradeOwnership()
    {
        await using var f=await Fixture.Create();await f.Add("external.png");var s=Operations(f);
        var note=NotebookFixture.Success(await s.Notes.ExecuteAsync(new CreateNotebookNote()));Guid trade,screenshot;
        await using(var db=f.Context())
        {
            var external=await db.TradeScreenshots.SingleAsync();trade=external.TradeId;screenshot=external.Id;
            var id=Guid.NewGuid();var image=NotebookImage.TradeReference(id,0,trade,screenshot,"External","Reference");
            var document=new NotebookDocument([NotebookNode.Image(Guid.NewGuid(),id,"External","Reference")]);
            var domain=NotebookNote.Rehydrate(note.Id,note.Title,document,null,NotebookNoteState.Active,false,null,[],[],[image],1,note.CreatedAtUtc,note.UpdatedAtUtc);
            NotebookMapper.Apply(await db.NotebookNotes.SingleAsync(),domain,new NotebookJsonCodec().Encode(document));
            db.NotebookImages.Add(NotebookMapper.Image(note.Id,image));db.NotebookLinks.Add(new(){NoteId=note.Id,Kind=1,TargetId=trade,LastKnownLabel="Trade"});await db.SaveChangesAsync();
        }
        var copy=NotebookFixture.Success(await s.Notes.ExecuteAsync(new DuplicateNotebookNote(note.Id,note.Version)));
        Assert.Equal(trade,copy.Images[0].TradeId);Assert.Equal(screenshot,copy.Images[0].TradeScreenshotId);Assert.Equal(trade,Assert.Single(copy.Links).TargetId);
        foreach(var n in new[]{note,copy}){var trash=NotebookFixture.Success(await s.Notes.ExecuteAsync(new TrashNotebookNote(n.Id,n.Version)));Assert.Equal(NotebookWriteStatus.Success,(await s.Notes.ExecuteAsync(new DeleteNotebookNote(trash.Id,trash.Version,true))).Status);}
        await using var fresh=f.Context();Assert.Single(await fresh.Trades.ToArrayAsync());Assert.Single(await fresh.TradeScreenshots.ToArrayAsync());Assert.Empty(await fresh.NotebookImages.ToArrayAsync());Assert.Empty(await fresh.NotebookOwnedImageFiles.ToArrayAsync());
        Assert.Equal(new byte[]{1,2,3},await File.ReadAllBytesAsync(Path.Combine(f.Paths.ScreenshotsDirectory,"external.png")));
    }
    [Fact]
    public async Task PendingDeletionWithoutBytesArchivesAndRestoresItsDurableCleanupState()
    {
        await using var f=await Fixture.Create();var s=Operations(f);var note=await Import(f);var image=note.Images[0];var path=Path.Combine(f.Paths.ScreenshotsDirectory,image.StorageKey!);
        await File.WriteAllBytesAsync(path,new byte[NotebookImageTests.Png.Length]);
        note=NotebookFixture.Success(await s.Notes.ExecuteAsync(new TrashNotebookNote(note.Id,note.Version)));
        var result=await s.Notes.ExecuteAsync(new DeleteNotebookNote(note.Id,note.Version,true));Assert.Equal(NotebookWriteStatus.Success,result.Status);Assert.True(result.CleanupPending);
        await using(var db=f.Context())Assert.True((await db.NotebookOwnedImageFiles.SingleAsync()).CleanupPending);
        File.Delete(path); // Simulate interruption after physical deletion and before ledger acknowledgement.
        var archive = await f.Service().CreateAsync(f.Output);
        Assert.True(archive.Succeeded, archive.ToString());
        using(var zip=ZipFile.OpenRead(f.Output))Assert.Null(zip.GetEntry(BackupArchiveContract.ScreenshotsPrefix+image.StorageKey));
        var preflight=await new RestorePreflightService().InspectAsync(f.Output,f.Root);Assert.True(preflight.Validated);
        await using var target=await Fixture.Create();await target.Services.DisposeAsync();JournalDataSession.ClearOwnedPools(target.Paths);
        Assert.Equal(JournalRestoreStatus.Restored,(await new JournalRestoreService(target.Paths).RestoreAsync(new(f.Output,preflight.Summary!.ArchiveSha256))).Status);
        await using var fresh=new ServiceCollection().AddPersistence(target.Paths).BuildServiceProvider();
        var factory=fresh.GetRequiredService<IDbContextFactory<JournalDbContext>>();await using(var db=await factory.CreateDbContextAsync()){Assert.Empty(await db.NotebookNotes.ToArrayAsync());Assert.True((await db.NotebookOwnedImageFiles.SingleAsync()).CleanupPending);}
        var files=new NotebookImageFiles(target.Paths,factory,new NotebookImageTests.Probe());Assert.True(await files.CleanupDeletedNoteAsync(note.Id,default));
        await using(var db=await factory.CreateDbContextAsync())Assert.Empty(await db.NotebookOwnedImageFiles.ToArrayAsync());
    }
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
        note = NotebookFixture.Success(await s.Notes.ExecuteAsync(new SaveNotebookImageDraft(note.Id, note.Version, note.Title, NotebookDocument.Empty, [])));
        Assert.Empty(note.Images);
        await source.Services.GetRequiredService<ITradeScreenshotFileStorage>().DeleteIfExistsAsync(image.StorageKey!);
        Assert.True(File.Exists(Path.Combine(source.Paths.ScreenshotsDirectory, image.StorageKey!)));
        var exportPath = Path.Combine(source.Root, "ledger-export");
        Assert.True((await source.Services.GetRequiredService<IPortableExportService>().CreateAsync(exportPath)).Succeeded);
        using (var json = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(exportPath, PortableExportContract.DataFile))))
        {
            Assert.Equal(18, json.RootElement.GetProperty("version").GetInt32());
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
