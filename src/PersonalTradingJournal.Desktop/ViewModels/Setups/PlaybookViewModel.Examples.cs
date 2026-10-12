using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Desktop.Screenshots;
using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Desktop.ViewModels.Setups;

public sealed partial class PlaybookViewModel
{
    private readonly IPlaybookExampleImages? _exampleImages;
    private readonly SavePlaybookExamplesUseCase? _saveExamples;
    private Guid[] _exampleBaseline = [];
    private TradingSetupPlaybookRevision? _readingExamplesOwner;
    private CancellationTokenSource? _thumbnails;
    public ObservableCollection<PlaybookExampleItem> EditorExamples { get; } = [];
    public IReadOnlyList<PlaybookExampleItem> ReadingExamples { get; private set; } = [];
    public IAsyncRelayCommand AttachExampleCommand { get; }
    public IRelayCommand<PlaybookExampleItem> RemoveExampleCommand { get; }
    public IAsyncRelayCommand<PlaybookExampleItem> ViewImageCommand { get; }

    private readonly HashSet<PlaybookExampleItem> _observedExamples = [];
    private void OnExamplesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        foreach (var removed in _observedExamples.Where(x => !EditorExamples.Contains(x)).ToArray())
        { removed.PropertyChanged -= OnExampleValidationChanged; _observedExamples.Remove(removed); }
        foreach (var added in EditorExamples.Where(x => !_observedExamples.Contains(x)))
        { added.PropertyChanged += OnExampleValidationChanged; _observedExamples.Add(added); }
        NotifyExampleValidation();
    }
    private void OnExampleValidationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(PlaybookExampleItem.HasErrors)) NotifyExampleValidation(); }
    private void NotifyExampleValidation()
    {
        OnPropertyChanged(nameof(ValidationSummary)); OnPropertyChanged(nameof(HasValidationErrors));
        if (!HasValidationErrors && Message == "Save blocked by field validation. Your edits are retained.")
            Message = "Validation corrected. Save Playbook to create a revision.";
    }

    private void UpdateReadingExamples()
    {
        if (ReferenceEquals(_readingExamplesOwner, DisplayedRevision)) return;
        _thumbnails?.Cancel(); _thumbnails?.Dispose(); _thumbnails = new();
        _readingExamplesOwner = DisplayedRevision;
        ReadingExamples = DisplayedRevision?.Guidance.Examples.Select(e => new PlaybookExampleItem(e)).ToArray() ?? [];
        OnPropertyChanged(nameof(ReadingExamples));
        _ = LoadReadingThumbnailsAsync(ReadingExamples);
    }

    private async Task LoadReadingThumbnailsAsync(IReadOnlyList<PlaybookExampleItem> rows)
    {
        var token = _thumbnails?.Token ?? CancellationToken.None;
        // Sequential loading caps decode pressure; no full-resolution images in the list.
        foreach (var row in rows) { if (token.IsCancellationRequested) return; await LoadThumbnailAsync(row, token); }
    }

    private async Task LoadThumbnailAsync(PlaybookExampleItem row, CancellationToken token = default)
    {
        if (_exampleImages is null || !row.HasImage) return;
        row.ImageState = "Loading chart…";
        try { var image = await _exampleImages.ThumbnailAsync(row, token); token.ThrowIfCancellationRequested(); row.Thumbnail = image; row.ImageState = ""; }
        catch (OperationCanceledException) { row.ImageState = "Image loading cancelled; open the chart to try again."; }
        catch (Exception) { row.ImageState = "Image unavailable, changed, or unsupported. Saved notes remain readable."; }
    }

    private async Task AttachExampleAsync(CancellationToken token)
    {
        IsWriting = true;
        try
        {
            var example = await _exampleImages!.PickAsync(token);
            token.ThrowIfCancellationRequested();
            if (example is not null) { EditorExamples.Add(example); Message = "Choose Valid or Invalid and explain context, confirmation or failed condition, and lesson. Save commits the image and a new revision."; }
        }
        catch (OperationCanceledException) { Message = "Attachment cancelled. Your existing edits are retained."; }
        catch (Exception) { Message = "Chart could not be attached. Choose a readable single-frame PNG, JPEG or supported WebP, at most 8 MiB, 8,192 pixels per side and 16 million pixels. Your edits are retained."; }
        finally { IsWriting = false; }
    }

    private async Task ViewImageAsync(PlaybookExampleItem? row, CancellationToken token)
    {
        if (row is null || !(EditorExamples.Contains(row) || ReadingExamples.Contains(row))) return;
        IsWriting = true;
        try { await _exampleImages!.ShowAsync(row, token); }
        catch (OperationCanceledException) { Message = "Image opening cancelled. Your edits are retained."; }
        catch (Exception) { Message = "Image unavailable, changed, or unsupported. Saved notes and any edits are retained."; }
        finally { IsWriting = false; }
    }
}
