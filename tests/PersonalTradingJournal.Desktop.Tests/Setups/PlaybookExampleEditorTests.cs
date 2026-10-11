using System.Windows.Media;
using System.Security.Cryptography;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Desktop.Screenshots;
using PersonalTradingJournal.Desktop.ViewModels.Setups;
using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Desktop.Tests.Setups;

public sealed class PlaybookExampleEditorTests
{
    [Theory]
    [InlineData(PlaybookExampleKind.Valid)] [InlineData(PlaybookExampleKind.Invalid)]
    public async Task AttachClassifyViewSaveRemoveAndHistoryKeepExactBytesAndNotes(PlaybookExampleKind kind)
    {
        var images = new ExampleImagesFake(); var f = new PlaybookFixture(images, images); await f.OpenAsync(); f.Edit("guidance");
        await f.Vm.AttachExampleCommand.ExecuteAsync(null); var row = Assert.Single(f.Vm.EditorExamples);
        Assert.Empty(images.Files); Assert.True(f.Vm.HasUnsavedChanges); Assert.False(f.Vm.TryLeave());
        await f.Vm.SaveCommand.ExecuteAsync(null); Assert.True(f.Vm.IsEditing); Assert.Contains("Each new chart", f.Vm.Message);
        row.Kind = kind; row.Annotation = "Synthetic context, condition and lesson";
        await f.Vm.ViewImageCommand.ExecuteAsync(row); Assert.Same(row, images.Viewed);
        await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.False(f.Vm.IsEditing); Assert.Single(images.Files);
        var first = f.Data.Revisions[0]; var saved = Assert.Single(first.Guidance.Examples);
        Assert.Equal(kind, saved.Kind); Assert.Equal(row.Annotation, saved.Annotation); Assert.NotNull(saved.Image);
        f.Vm.EditCommand.Execute(null); Assert.False(f.Vm.HasUnsavedChanges);
        var edit = Assert.Single(f.Vm.EditorExamples); edit.Annotation = "changed"; Assert.True(f.Vm.HasUnsavedChanges);
        edit.Annotation = saved.Annotation; Assert.False(f.Vm.HasUnsavedChanges);
        f.Vm.RemoveExampleCommand.Execute(edit); Assert.True(f.Vm.HasUnsavedChanges);
        await f.Vm.SaveCommand.ExecuteAsync(null); Assert.Empty(f.Vm.ReadingExamples); Assert.Single(images.Files);
        f.Vm.ViewRevisionCommand.Execute(first); Assert.Equal(row.Annotation, Assert.Single(f.Vm.ReadingExamples).Annotation);
        Assert.False(f.Vm.AttachExampleCommand.CanExecute(null)); Assert.Equal(2, f.Data.Revisions.Count);
    }

    [Theory]
    [InlineData("conflict")] [InlineData("failure")] [InlineData("cancel")]
    public async Task FailedSaveRetainsAllPendingFieldsAndCompensatesImages(string failure)
    {
        var images = new ExampleImagesFake(); var f = new PlaybookFixture(images, images); await f.OpenAsync(); f.Edit("keep guidance");
        await f.Vm.AttachExampleCommand.ExecuteAsync(null); var row = Assert.Single(f.Vm.EditorExamples);
        row.Kind = PlaybookExampleKind.Invalid; row.Annotation = "keep annotation"; f.Data.WriteFailure = failure;
        await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.True(f.Vm.IsEditing); Assert.Same(row, Assert.Single(f.Vm.EditorExamples));
        Assert.Equal("keep annotation", row.Annotation); Assert.All(f.Vm.Fields, field => Assert.Equal("keep guidance", field.Text));
        Assert.Empty(images.Files); Assert.Empty(f.Data.Revisions); Assert.False(f.Vm.TryLeave());
        f.Data.WriteFailure = null; await f.Vm.SaveCommand.ExecuteAsync(null); Assert.False(f.Vm.IsEditing); Assert.Single(images.Files);
    }

    [Fact]
    public async Task PickerCancellationMissingImageAndRemovalNeverDiscardGuidanceOrBypassGuard()
    {
        var images = new ExampleImagesFake { CancelPick = true }; var f = new PlaybookFixture(images, images); await f.OpenAsync(); f.Edit("keep");
        await f.Vm.AttachExampleCommand.ExecuteAsync(null); Assert.Empty(f.Vm.EditorExamples);
        images.CancelPick = false; await f.Vm.AttachExampleCommand.ExecuteAsync(null);
        var row = f.Vm.EditorExamples[0]; row.Kind = PlaybookExampleKind.Valid; row.Annotation = "notes";
        images.FailRead = true; await f.Vm.ViewImageCommand.ExecuteAsync(row); Assert.Contains("unavailable", f.Vm.Message);
        f.Vm.CancelEditCommand.Execute(null); Assert.True(f.Vm.IsEditing); Assert.Single(f.Vm.EditorExamples);
        Assert.False(f.Vm.TrySelectSetup(f.Data.Row(f.Other))); Assert.Same(row, f.Vm.EditorExamples[0]);
        f.Dialogs.ConfirmationResult = true; f.Vm.CancelEditCommand.Execute(null);
        Assert.False(f.Vm.IsEditing); Assert.Empty(f.Vm.EditorExamples); Assert.Empty(images.Files); Assert.Empty(f.Data.Revisions);
    }

    [Fact]
    public async Task AttachmentCountIsBoundedAndCancelledAttachmentLeavesExistingRows()
    {
        var images = new ExampleImagesFake(); var f = new PlaybookFixture(images, images); await f.OpenAsync(); f.Edit("guidance");
        for (int i = 0; i < 12; i++) await f.Vm.AttachExampleCommand.ExecuteAsync(null);
        Assert.False(f.Vm.AttachExampleCommand.CanExecute(null));
        f.Vm.RemoveExampleCommand.Execute(f.Vm.EditorExamples[0]); Assert.True(f.Vm.AttachExampleCommand.CanExecute(null));
        images.ThrowCancellation = true; await f.Vm.AttachExampleCommand.ExecuteAsync(null);
        Assert.Equal(11, f.Vm.EditorExamples.Count); Assert.Contains("cancelled", f.Vm.Message); Assert.Empty(images.Files);
    }
}

internal sealed class ExampleImagesFake : IPlaybookExampleImages, IPlaybookImageStorage
{
    internal static readonly byte[] Bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    internal readonly Dictionary<string, byte[]> Files = [];
    internal bool CancelPick, ThrowCancellation, FailRead;
    internal PlaybookExampleItem? Viewed;
    internal ImageSource? Preview;
    private ImageSource Thumbnail() { if (Preview is not null) return Preview; var image = new DrawingImage(); image.Freeze(); return image; }
    public Task<PlaybookExampleItem?> PickAsync(CancellationToken token)
    { token.ThrowIfCancellationRequested(); if (ThrowCancellation) throw new OperationCanceledException(); return Task.FromResult(CancelPick ? null : new PlaybookExampleItem("chart.png", Bytes.ToArray(), Thumbnail())); }
    public Task<ImageSource> ThumbnailAsync(PlaybookExampleItem row, CancellationToken token)
    { token.ThrowIfCancellationRequested(); if (FailRead) throw new IOException("synthetic private path"); return Task.FromResult(Thumbnail()); }
    public Task ShowAsync(PlaybookExampleItem row, CancellationToken token)
    { if (FailRead) throw new IOException("synthetic private path"); Viewed = row; return Task.CompletedTask; }
    public Task<PlaybookExampleImage> StoreAsync(string name, ReadOnlyMemory<byte> bytes, CancellationToken token = default)
    { token.ThrowIfCancellationRequested(); string key = Guid.NewGuid().ToString("N") + ".png"; Files[key] = bytes.ToArray(); return Task.FromResult(new PlaybookExampleImage(key, name, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes.Span)))); }
    public Task<byte[]> ReadAsync(PlaybookExampleImage image, CancellationToken token = default) => Task.FromResult(Files[image.StorageKey]);
    public Task DeleteUnreferencedAsync(string key, CancellationToken token = default) { Files.Remove(key); return Task.CompletedTask; }
}
