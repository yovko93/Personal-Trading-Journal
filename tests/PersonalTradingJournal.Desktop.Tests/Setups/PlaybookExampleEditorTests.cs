using System.Windows.Media;
using System.Security.Cryptography;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Desktop.Screenshots;
using PersonalTradingJournal.Desktop.ViewModels.Setups;
using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Desktop.Tests.Setups;

public sealed class PlaybookExampleEditorTests
{
    [Fact]
    public async Task AllExampleErrorsClearIndividuallyWithoutLosingImagesAndLegacyNotesStayValid()
    {
        var images = new ExampleImagesFake(); var f = new PlaybookFixture(images, images); f.Seed(1); await f.OpenAsync(); f.Edit("kept");
        var legacy = f.Vm.EditorExamples[0];
        await f.Vm.AttachExampleCommand.ExecuteAsync(null); await f.Vm.AttachExampleCommand.ExecuteAsync(null);
        var first = f.Vm.EditorExamples[1]; var second = f.Vm.EditorExamples[2];
        second.Annotation = new string('a', 4001);
        await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.False(legacy.HasErrors); Assert.False(legacy.RequiresClassification);
        Assert.All(new[] { first, second }, e => { Assert.True(e.HasKindError); Assert.True(e.HasAnnotationError); Assert.NotNull(e.PendingBytes); });
        Assert.Contains("Chart example 2", f.Vm.ValidationSummary); Assert.Contains("Chart example 3", f.Vm.ValidationSummary);
        Assert.Equal(0, f.Data.AppendCount); Assert.Empty(images.Files);
        first.Kind = PlaybookExampleKind.Valid; Assert.False(first.HasKindError); Assert.True(first.HasAnnotationError);
        first.Annotation = "context, condition, lesson"; Assert.False(first.HasErrors); Assert.True(f.Vm.HasValidationErrors);
        second.Kind = PlaybookExampleKind.Invalid; second.Annotation = "failed condition";
        Assert.False(f.Vm.HasValidationErrors); Assert.All(f.Vm.Fields, field => Assert.Equal("kept", field.Text));
        await f.Vm.SaveCommand.ExecuteAsync(null); Assert.False(f.Vm.IsEditing); Assert.Equal(2, images.Files.Count);
    }

    [Theory]
    [InlineData("chart.png", 0)] [InlineData("../chart.png", 1)]
    public async Task InvalidPendingAttachmentIsMarkedAndRemovalClearsSummary(string name, int size)
    {
        var f = new PlaybookFixture(); await f.OpenAsync(); f.Edit("kept");
        var image = new DrawingImage(); image.Freeze();
        var row = new PlaybookExampleItem(name, new byte[size], image) { Kind = PlaybookExampleKind.Valid, Annotation = "notes kept" };
        f.Vm.EditorExamples.Add(row); await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.True(row.HasAttachmentError); Assert.False(row.HasKindError); Assert.False(row.HasAnnotationError);
        Assert.True(f.Vm.IsEditing); Assert.Equal("notes kept", row.Annotation); Assert.Equal(0, f.Data.AppendCount);
        f.Vm.RemoveExampleCommand.Execute(row); Assert.False(f.Vm.HasValidationErrors);
    }

    [Fact]
    public async Task HistoricalPreviewSwitchingAndReturningPreservePendingFormAndSaveFromCurrentRevision()
    {
        var images = new ExampleImagesFake(); var f = new PlaybookFixture(images, images); f.Seed(3); await f.OpenAsync(); f.Edit("unsaved fields");
        await f.Vm.AttachExampleCommand.ExecuteAsync(null); var pending = f.Vm.EditorExamples.Last();
        pending.Kind = PlaybookExampleKind.Invalid; pending.Annotation = "unsaved example";
        foreach (var revision in f.Vm.Revisions.Reverse())
        {
            f.Vm.ViewRevisionCommand.Execute(revision);
            Assert.Same(revision, f.Vm.DisplayedRevision); Assert.True(f.Vm.ShowReading && f.Vm.IsEditing);
            Assert.Same(pending, f.Vm.EditorExamples.Last()); Assert.All(f.Vm.Fields, field => Assert.Equal("unsaved fields", field.Text));
            Assert.Null(f.Dialogs.ConfirmationRequest); Assert.True(f.Vm.HasUnsavedChanges);
        }
        f.Vm.ShowCurrentCommand.Execute(null); Assert.False(f.Vm.ShowReading); Assert.True(f.Vm.IsEditing);
        f.Vm.ViewRevisionCommand.Execute(f.Vm.Revisions.Last());
        await f.Vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(4, f.Vm.DisplayedRevision!.Revision); Assert.Equal("unsaved fields", f.Vm.DisplayedRevision.Guidance.MarketContext);
        Assert.False(f.Vm.IsEditing); Assert.False(f.Vm.IsHistorical);
    }

    [Theory]
    [InlineData(PlaybookExampleKind.Valid)] [InlineData(PlaybookExampleKind.Invalid)]
    public async Task AttachClassifyViewSaveRemoveAndHistoryKeepExactBytesAndNotes(PlaybookExampleKind kind)
    {
        var images = new ExampleImagesFake(); var f = new PlaybookFixture(images, images); await f.OpenAsync(); f.Edit("guidance");
        await f.Vm.AttachExampleCommand.ExecuteAsync(null); var row = Assert.Single(f.Vm.EditorExamples);
        Assert.Empty(images.Files); Assert.True(f.Vm.HasUnsavedChanges); Assert.False(f.Vm.TryLeave());
        await f.Vm.SaveCommand.ExecuteAsync(null); Assert.True(f.Vm.IsEditing); Assert.Contains("Chart example 1", f.Vm.ValidationSummary);
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
