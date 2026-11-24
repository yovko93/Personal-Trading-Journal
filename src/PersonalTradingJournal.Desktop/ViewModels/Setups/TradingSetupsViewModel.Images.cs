using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Desktop.Screenshots;

namespace PersonalTradingJournal.Desktop.ViewModels.Setups;

public sealed record SetupExampleCard(SetupExample Example, ImageSource? Thumbnail)
{
    public string Label => Example.Timeframe is { Length: > 0 } timeframe
        ? $"{Example.File.FileName}, {timeframe}" : Example.File.FileName;
}

public sealed class StagedSetupExample : ObservableObject
{
    public StagedSetupExample(PlanImageSelection upload)
    {
        Upload = upload;
        FileName = upload.FileName;
        Thumbnail = upload.Thumbnail;
    }

    public StagedSetupExample(SetupExampleCard saved)
    {
        Saved = saved.Example;
        FileName = saved.Example.File.FileName;
        Thumbnail = saved.Thumbnail;
        _timeframe = saved.Example.Timeframe;
    }

    public string FileName { get; }
    public ImageSource? Thumbnail { get; }
    public PlanImageSelection? Upload { get; }
    public SetupExample? Saved { get; }
    private string? _timeframe;
    public string? Timeframe { get => _timeframe; set => SetProperty(ref _timeframe, value); }
}

public sealed record SetupImagePreview(SetupExampleCard Card, ImageSource Original);

public sealed partial class TradingSetupsViewModel
{
    private readonly ISetupExampleStore? _examples;
    private readonly IWorkspaceImageViewer? _viewer;
    private IReadOnlyList<SetupExampleCard> _cards = [];
    private PlanImageSelection? _pending;
    private string? _timeframe;
    private bool _imageBusy;
    private IReadOnlyList<StagedSetupExample> _staged = [];
    private SetupImagePreview? _preview;
    public SetupImagePreview? Preview { get => _preview; private set => SetProperty(ref _preview, value); }
    public IReadOnlyList<StagedSetupExample> StagedExamples { get => _staged; private set => SetProperty(ref _staged, value); }

    public IAsyncRelayCommand StageExamplesCommand { get; }
    public IRelayCommand<StagedSetupExample> RemoveStagedCommand { get; }
    public IRelayCommand BackToCardsCommand { get; }

    private bool IsImageFormOpen => IsCreateFormVisible || IsEditFormVisible;

    private void RemoveStaged(StagedSetupExample? item)
    {
        if (item is null || !StagedExamples.Contains(item)) return;
        if (item.Saved is not null && !_dialogService.Confirm(new ConfirmationDialogRequest(
            "Remove example image?", $"Remove {item.FileName} when you save this Setup?", "Remove image", isDestructive: true))) return;
        StagedExamples = StagedExamples.Where(example => example != item).ToArray();
    }

    private NewSetupExample[] NewUploads() => StagedExamples.Where(item => item.Upload is not null)
        .Select(item => new NewSetupExample(item.FileName, item.Upload!.Bytes, item.Timeframe)).ToArray();

    private SetupImageChanges? GetImageEdits()
    {
        var retained = StagedExamples.Where(item => item.Saved is not null).ToDictionary(item => item.Saved!.Id);
        var edits = new List<SetupImageEdit>();
        foreach (var card in ExampleCards)
        {
            var saved = card.Example;
            if (!retained.TryGetValue(saved.Id, out var draft))
                edits.Add(new(saved.Id, saved.Timeframe, null, true));
            else if (draft.Timeframe != saved.Timeframe)
                edits.Add(new(saved.Id, saved.Timeframe, draft.Timeframe, false));
        }
        var added = NewUploads();
        return added.Length == 0 && edits.Count == 0 ? null : new(added, edits);
    }

    private async Task StageExamplesAsync(CancellationToken token)
    {
        if (_viewer is null || !IsImageFormOpen || IsBusy) return;
        IsImageBusy = true;
        SaveErrorMessage = null;
        try
        {
            var selection = await _viewer.PickMultipleAsync("Select Setup example images", token);
            if (!token.IsCancellationRequested && IsImageFormOpen)
                StagedExamples = [.. StagedExamples, .. selection.Select(image => new StagedSetupExample(image))];
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception)
        {
            const string message = "Choose readable PNG or JPEG images of up to 8 MiB each.";
            if (IsEditFormVisible) EditErrorMessage = message;
            else SaveErrorMessage = message;
        }
        finally { IsImageBusy = false; }
    }

    public IReadOnlyList<SetupExampleCard> ExampleCards { get => _cards; private set => SetProperty(ref _cards, value); }
    public PlanImageSelection? PendingExample
    {
        get => _pending;
        private set { if (SetProperty(ref _pending, value)) NotifyCommands(); }
    }
    public string? ExampleTimeframe { get => _timeframe; set => SetProperty(ref _timeframe, value); }
    private bool IsImageBusy { get => _imageBusy; set { if (SetProperty(ref _imageBusy, value)) NotifyCommands(); } }
    private bool CanUseImages => _examples is not null && _viewer is not null && SelectedTradingSetup is not null && !IsBusy && !IsEditFormVisible;
    private bool IsCurrentCard(SetupExampleCard? card) => CanUseImages && card is not null &&
        card.Example.SetupId == SelectedTradingSetup?.Id && ExampleCards.Contains(card);

    public IAsyncRelayCommand UploadExampleCommand { get; }
    public IAsyncRelayCommand SaveExampleCommand { get; }
    public IRelayCommand CancelExampleCommand { get; }
    public IAsyncRelayCommand<SetupExampleCard> ViewExampleCommand { get; }
    public IAsyncRelayCommand<SetupExampleCard> RemoveExampleCommand { get; }

    private void ClearPendingExample() { PendingExample = null; ExampleTimeframe = null; }

    private async Task LoadExamplesAsync(Guid owner, CancellationToken token)
    {
        if (_examples is null || _viewer is null) return;
        var cards = new List<SetupExampleCard>();
        foreach (var image in await _examples.ListAsync(owner, token))
        {
            ImageSource? thumb = null;
            try { thumb = await _viewer.ThumbnailAsync(image.File, token); }
            catch (Exception e) when (e is not OperationCanceledException)
            { if (SelectedTradingSetup?.Id == owner) ActionErrorMessage = "An example image could not be read."; }
            cards.Add(new(image, thumb));
        }
        token.ThrowIfCancellationRequested();
        if (SelectedTradingSetup?.Id == owner) ExampleCards = cards;
    }

    private async Task UploadExampleAsync(CancellationToken token)
    {
        if (!CanUseImages || PendingExample is not null) return;
        var owner = SelectedTradingSetup!.Id;
        IsImageBusy = true;
        ActionErrorMessage = null;
        try
        {
            var image = await _viewer!.PickAsync("Upload Trading Setup example", token);
            if (!token.IsCancellationRequested && SelectedTradingSetup?.Id == owner) PendingExample = image;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception) { ActionErrorMessage = "Choose a readable PNG or JPEG image of up to 8 MiB."; }
        finally { IsImageBusy = false; }
    }

    private async Task SaveExampleAsync(CancellationToken token)
    {
        if (!CanUseImages || PendingExample is not { } pending) return;
        var owner = SelectedTradingSetup!.Id;
        IsImageBusy = true;
        ActionErrorMessage = null;
        try
        {
            var image = await _examples!.AddAsync(owner, pending.FileName, pending.Bytes, ExampleTimeframe, token);
            if (SelectedTradingSetup?.Id == owner)
            {
                ExampleCards = [.. ExampleCards, new(image, pending.Thumbnail)];
                ClearPendingExample();
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception) { ActionErrorMessage = "The example image could not be saved."; }
        finally { IsImageBusy = false; }
    }

    private async Task ViewExampleAsync(SetupExampleCard? card, CancellationToken token)
    {
        if (!IsCurrentCard(card)) return;
        IsImageBusy = true;
        try
        {
            var original = await _viewer!.OriginalAsync(card!.Example.File, token);
            if (!token.IsCancellationRequested && SelectedTradingSetup?.Id == card.Example.SetupId && ExampleCards.Contains(card))
                Preview = new(card, original);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception) { ActionErrorMessage = "The original example image could not be opened."; }
        finally { IsImageBusy = false; }
    }

    private async Task RemoveExampleAsync(SetupExampleCard? card, CancellationToken token)
    {
        if (!IsCurrentCard(card) || !_dialogService.Confirm(new ConfirmationDialogRequest(
            "Remove example image?", $"Remove {card!.Example.File.FileName} from this Setup?", "Remove image", isDestructive: true))) return;
        IsImageBusy = true;
        ActionErrorMessage = null;
        try
        {
            await _examples!.RemoveAsync(card.Example.SetupId, card.Example.Id, token);
            if (Preview?.Card == card) Preview = null;
            ExampleCards = ExampleCards.Where(x => x != card).ToArray();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception)
        {
            ActionErrorMessage = "Image removal could not be completed. Reopen the Setup to check its images.";
        }
        finally { IsImageBusy = false; }
    }
}
