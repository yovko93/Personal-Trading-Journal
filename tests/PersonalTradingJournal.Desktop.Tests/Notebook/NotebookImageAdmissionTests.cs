using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Screenshots;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Desktop.Tests.Journals;
using PersonalTradingJournal.Infrastructure.Notebook;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Storage;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed class NotebookImageAdmissionTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task TradePreviewUsesAuthoritativeOwnerAndDisposesSource(bool matches, bool corrupt)
    {
        var reference = NotebookImage.TradeReference(Guid.NewGuid(), 0, Guid.NewGuid(), Guid.NewGuid());
        var reader = new TradeReader(reference.TradeScreenshotId!.Value, matches ? reference.TradeId!.Value : Guid.NewGuid(),
            corrupt ? new byte[] { 1, 2, 3 } : Encode(720, 180));
        var images = new NotebookImageEditorTests.Imports(new NotebookFake());
        var presenter = new NotebookImagePresentation(images, reader);
        if (!matches) await Assert.ThrowsAsync<IOException>(() => presenter.PreviewAsync(Guid.NewGuid(), reference, CancellationToken.None));
        else if (corrupt) await Assert.ThrowsAsync<InvalidDataException>(() => presenter.PreviewAsync(Guid.NewGuid(), reference, CancellationToken.None));
        else
        {
            var preview = (BitmapSource)await presenter.PreviewAsync(Guid.NewGuid(), reference, CancellationToken.None);
            Assert.True(preview.IsFrozen); Assert.InRange(preview.PixelWidth, 1, 384);
        }
        Assert.False(reader.Stream.CanRead); Assert.Equal(0, images.OwnedReads);
    }
    private sealed class TradeReader(Guid id, Guid tradeId, byte[] bytes) : ITradeScreenshotContentReader
    {
        internal readonly MemoryStream Stream = new(bytes, false);
        public Task<TradeScreenshotContent?> OpenAsync(Guid screenshotId, CancellationToken cancellationToken = default)
        { Assert.Equal(id, screenshotId); return Task.FromResult<TradeScreenshotContent?>(new(id, tradeId, "trade.png", Stream)); }
    }
    [Fact]
    public async Task RealEditorImportSaveUndoRecoveryAndPreviewRetainExactOriginalWithoutOpenFileHandles()
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        string root = Directory.CreateTempSubdirectory("ptj-notebook-editor-image-").FullName;
        try
        {
            var paths = new LocalApplicationPaths(root); var codec = new NotebookJsonCodec();
            var factory = db.Provider.GetRequiredService<IDbContextFactory<JournalDbContext>>();
            var files = new NotebookImageFiles(paths, factory, new WpfNotebookImageValidator());
            var notes = new NotebookService(factory, codec, imageFiles: files);
            var images = new NotebookImageService(factory, notes, codec, files);
            var recovery = db.Provider.GetRequiredService<INotebookRecoveryStore>();
            var note = (await notes.ExecuteAsync(new CreateNotebookNote())).Note!;
            using var vm = new NotebookWorkspaceViewModel(notes, new FakeDialogService(), codec: codec, recovery: recovery, time: new NotebookClock(), images: images);
            await vm.ActivateAsync(); vm.SelectedNote = vm.Notes.Single(); await vm.LoadTask;
            vm.SetDraftDocument(NotebookEditingTests.Text("Draft with image")); byte[] original = Encode(720, 180);
            Assert.True(await vm.ImportImageAsync(note.Id, original, node => vm.SetDraftDocument(new(vm.DraftDocument.Blocks.Append(node)))));
            Assert.False(vm.IsDirty); var saved = vm.Note!; var image = saved.Images.Single();
            Assert.Equal("Draft with image\n\n", saved.Document.PlainText);
            var preview = await new NotebookImagePresentation(images, new NoTrade()).PreviewAsync(note.Id, image, CancellationToken.None);
            Assert.True(preview.IsFrozen); Assert.InRange(((BitmapSource)preview).PixelWidth, 1, 384);
            using (File.Open(Path.Combine(paths.ScreenshotsDirectory, image.StorageKey!), FileMode.Open, FileAccess.Read, FileShare.None)) { }
            vm.SetDraftDocument(new(saved.Document.Blocks.Where(x => x.Kind != NotebookNodeKind.Image)));
            Assert.True(await vm.SaveDraftAsync()); Assert.Empty(vm.Note!.Images);
            Assert.Equal(original, (await images.ReadOriginalAsync(note.Id, image.Id)).Original);
            // A fresh workspace can recover a same-version image-bearing checkpoint using retained metadata.
            var checkpointId = Guid.NewGuid();
            Assert.Equal(NotebookWriteStatus.Success, await recovery.PutAsync(new(checkpointId, note.Id, vm.Note.Version, 1, "Recovered", saved.Document, DateTimeOffset.UtcNow), 0));
            vm.Deactivate(); await vm.DeactivationTask;
            using var fresh = new NotebookWorkspaceViewModel(notes, new FakeDialogService(), codec: codec, recovery: recovery, time: new NotebookClock(), images: images);
            await fresh.ActivateAsync(); await fresh.RecoverCommand.ExecuteAsync(fresh.RecoveryDrafts.Single(x => x.Id == checkpointId));
            Assert.True(await fresh.SaveDraftAsync()); Assert.Single(fresh.Note!.Images);
            Assert.Equal(codec.Encode(saved.Document).Json, codec.Encode(fresh.Note.Document).Json);
            Assert.Equal(original, (await images.ReadOriginalAsync(note.Id, image.Id)).Original);
            File.Delete(Path.Combine(paths.ScreenshotsDirectory, image.StorageKey!));
            await Assert.ThrowsAsync<IOException>(() => new NotebookImagePresentation(images, new NoTrade()).PreviewAsync(note.Id, image, CancellationToken.None));
            Assert.Single((await notes.LoadAsync(note.Id)).Note!.Images);
            fresh.Deactivate(); await fresh.DeactivationTask;
        }
        finally { Directory.Delete(root, true); }
    }
    private sealed class NoTrade : ITradeScreenshotContentReader
    {
        public Task<TradeScreenshotContent?> OpenAsync(Guid screenshotId, CancellationToken cancellationToken = default) =>
            throw new Xunit.Sdk.XunitException("Owned-image presentation must not read Trade screenshots");
    }
    [Fact]
    public async Task HostDecoderAndManagedImportReadBackExactOriginalThroughApplicationPort()
    {
        await using var db = await JournalSqliteTests.JournalTestDatabase.CreateAsync();
        string root = Directory.CreateTempSubdirectory("ptj-notebook-admission-").FullName;
        try
        {
            var paths = new LocalApplicationPaths(root); var codec = new NotebookJsonCodec();
            var factory = db.Provider.GetRequiredService<IDbContextFactory<JournalDbContext>>();
            var files = new NotebookImageFiles(paths, factory, new WpfNotebookImageValidator());
            var notes = new NotebookService(factory, codec, imageFiles: files);
            INotebookImages images = new NotebookImageService(factory, notes, codec, files);
            var note = (await notes.ExecuteAsync(new CreateNotebookNote())).Note!; var id = Guid.NewGuid(); byte[] original = Encode(2, 3);
            using var input = new MemoryStream(original);
            var result = await images.ImportAsync(new(note.Id, note.Version, id,
                new([PersonalTradingJournal.Domain.Notebook.NotebookNode.Image(Guid.NewGuid(), id)])), input);
            Assert.Equal(NotebookWriteStatus.Success, result.Status);
            Assert.Equal(original, (await images.ReadOriginalAsync(note.Id, id)).Original);
        }
        finally { Directory.Delete(root, true); }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InstalledDecoderFullyValidatesPngAndJpegWithoutChangingOriginal(bool jpeg)
    {
        byte[] bytes = Encode(2, 3, jpeg); var original = bytes.ToArray();
        var probe = new WpfNotebookImageValidator().Validate(bytes);
        Assert.Equal(new(NotebookImageStatus.Success, jpeg ? ".jpg" : ".png", 2, 3), probe);
        Assert.Equal(original, bytes);
    }
    [Theory]
    [InlineData(8193, 1)]
    [InlineData(1, 8193)]
    [InlineData(4001, 4000)]
    public void DecoderRejectsDimensionAndTotalPixelBoundsBeforeMaterialization(int width, int height)
        => Assert.Equal(NotebookImageStatus.TooLarge, new WpfNotebookImageValidator().Validate(Encode(width, height)).Status);
    [Fact]
    public void WrongSignatureCorruptPayloadAndAnimatedContainerAreRejected()
    {
        var validator = new WpfNotebookImageValidator();
        Assert.Equal(NotebookImageStatus.Unsupported, validator.Validate("GIF89a"u8.ToArray()).Status);
        byte[] corrupt = [137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3];
        Assert.NotEqual(NotebookImageStatus.Success, validator.Validate(corrupt).Status);
        byte[] animated = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 8, .. "acTL"u8.ToArray(), 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0];
        Assert.Equal(NotebookImageStatus.Unsupported, validator.Validate(animated).Status);
        Assert.Equal(NotebookImageStatus.Unsupported, new UnavailableNotebookImageValidator().Validate(Encode(1, 1)).Status);
    }
    private static byte[] Encode(int width, int height, bool jpeg = false)
    {
        var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray8, null, new byte[checked(width * height)], width);
        BitmapEncoder encoder = jpeg ? new JpegBitmapEncoder { QualityLevel = 93 } : new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source)); using var output = new MemoryStream(); encoder.Save(output); return output.ToArray();
    }
}
