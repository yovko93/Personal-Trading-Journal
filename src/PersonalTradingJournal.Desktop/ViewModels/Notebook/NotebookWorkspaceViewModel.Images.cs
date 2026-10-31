using System.Collections.Immutable;
using System.IO;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Desktop.ViewModels.Notebook;

public sealed partial class NotebookWorkspaceViewModel
{
    private readonly INotebookImages? _images;
    private readonly Dictionary<Guid, NotebookImage> _knownImages = [];
    private bool _imageOperation, _preparingImage;
    public Screenshots.INotebookImagePresentation? ImagePresentation { get; }
    public NotebookImage? FindImage(Guid id) => _knownImages.GetValueOrDefault(id);
    private void RememberImages(NotebookNoteSnapshot note)
    { foreach (var image in note.Images) _knownImages[image.Id] = image; }

    private async Task<ImmutableArray<NotebookImage>?> DraftImagesAsync(Guid noteId, NotebookDocument document, CancellationToken ct)
    {
        var images = ImmutableArray.CreateBuilder<NotebookImage>();
        foreach (var node in document.ImageNodes)
        {
            var id = node.ImageId!.Value;
            if (!_knownImages.TryGetValue(id, out var image))
            {
                if (_images is null) return null;
                var original = await _images.ReadOriginalAsync(noteId, id, ct);
                if (original.Metadata is not { } file) return null;
                image = NotebookImage.Owned(id, images.Count, file.StorageKey, file.SizeBytes, file.Sha256);
                _knownImages[id] = image;
            }
            images.Add(image.Ownership == NotebookImageOwnership.NoteOwned
                ? NotebookImage.Owned(id, images.Count, image.StorageKey!, image.SizeBytes!.Value, image.Sha256!, node.Caption, node.AlternativeText)
                : NotebookImage.TradeReference(id, images.Count, image.TradeId!.Value, image.TradeScreenshotId!.Value, node.Caption, node.AlternativeText));
        }
        return images.ToImmutable();
    }

    /// <summary>Import commits the final candidate once. Acknowledgement updates the editor without
    /// replacing newer typing; only edits made after the candidate was captured need another save.</summary>
    public async Task<bool> ImportImageAsync(Guid noteId, byte[] bytes, Action<NotebookNode> insert,
        Func<NotebookNode, NotebookDocument>? placement = null)
    {
        if (_noteOperation || _preparingImage || _imageOperation || _images is null || Note?.Id != noteId || !CanEditBody || _recoveryConflict) return false;
        _preparingImage = true;
        var operation = RunAsync();
        try { await Track(operation); return await operation; }
        finally { _preparingImage = false; }
        async Task<bool> RunAsync()
        {
            var activation = _lifetime;
            StopDebounce(); await _flushTask;
            if (!Current(activation?.Token ?? CancellationToken.None) || Note?.Id != noteId) return false;
            if (IsDirty && !await SaveDraftAsync()) { ReportMessage("Save pending content before inserting the image. Your selection is unchanged.", NotebookNotificationKind.Warning); return false; }
            if (!CanMutate || Note?.Id != noteId || !ReferenceEquals(activation, _lifetime)) return false;
            var captured = Note; var node = NotebookNode.Image(Guid.NewGuid(), Guid.NewGuid());
            var proposed = placement?.Invoke(node) ?? new NotebookDocument(captured.Document.Blocks.Append(node));
            ValidateDocument(proposed);
            _imageOperation = true; IsSaving = true; StopDebounce(); DraftChanged();
            bool applied = false;
            try
            {
                await MutateAsync(async ct =>
                {
                    using var input = new MemoryStream(bytes, false);
                    var result = await Task.Run(() => _images.ImportAsync(new(noteId, captured.Version, node.ImageId!.Value, proposed, placement is not null), input, ct), ct);
                    if (_disposed || !_active || !ReferenceEquals(activation, _lifetime) || Note?.Id != noteId) return;
                    if (result.Status != NotebookWriteStatus.Success || result.Note is not { } saved || saved.Id != noteId ||
                        saved.Version != captured.Version + 1 || Signature(saved.Title, saved.Document) != Signature(captured.Title, proposed))
                    {
                        _autosavePaused = result.Status is NotebookWriteStatus.Conflict or NotebookWriteStatus.StorageFailure or NotebookWriteStatus.NotFound;
                        PublishWrite(result.Status == NotebookWriteStatus.Success ? NotebookWriteStatus.StorageFailure : result.Status, "");
                        Message = result.Status == NotebookWriteStatus.UnsupportedContent
                            ? "Unsupported image or unavailable codec. Use a single-frame PNG or JPEG; WebP requires an installed decoder."
                            : result.Status == NotebookWriteStatus.TooLarge ? "Image exceeds the byte or dimension limits. Your selection is unchanged."
                            : result.Status == NotebookWriteStatus.Invalid ? "This image could not be decoded safely. Your selection is unchanged." : Message;
                        if (result.CleanupPending) Message += " Original retention requires later verification.";
                        ShowNotification(Message, Notification?.Kind ?? NotebookNotificationKind.Error);
                        return;
                    }
                    RememberImages(saved); Note = saved; _savedSignature = Signature(saved.Title, saved.Document);
                    // No draft/editor replacement: live TextPointers follow typing while the import runs.
                    insert(node); applied = true;
                    _autosavePaused = false; _saveState = IsDirty ? "Unsaved changes" : "Saved"; Message = "";
                });
            }
            finally
            {
                _imageOperation = false; IsSaving = false; DraftChanged();
                if (!_disposed && _active && IsDirty) ScheduleAutosave();
            }
            return applied;
        }
    }
}
