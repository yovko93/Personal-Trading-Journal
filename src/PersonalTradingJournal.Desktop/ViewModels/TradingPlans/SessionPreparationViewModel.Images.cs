using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Desktop.Screenshots;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

public sealed record PlanTimeframeChoice(TradingPlanTimeframe Value, string Label);
public sealed class PlanScreenshotItem : ObservableObject
{
    internal PlanScreenshotItem(SessionPreparationViewModel owner, Guid id, Guid instrument, TradingPlanTimeframe timeframe,
        PlaybookExampleImage? image, PlanImageSelection? pending)
    {
        Id = id; InstrumentId = instrument; Timeframe = timeframe; Image = image; Pending = pending; _thumbnail = pending?.Thumbnail;
        RemoveCommand = new RelayCommand(() => owner.RemoveScreenshot(this), () => owner.CanEdit);
        OpenCommand = new AsyncRelayCommand(ct => owner.OpenScreenshotAsync(this, ct), () => !owner.IsBusy && !owner.IsDisposed);
    }
    public Guid Id { get; }
    public Guid InstrumentId { get; }
    public TradingPlanTimeframe Timeframe { get; }
    public string Label => SessionPreparationViewModel.Timeframes.Single(t => t.Value == Timeframe).Label;
    internal PlaybookExampleImage? Image { get; set; }
    internal PlanImageSelection? Pending { get; set; }
    private ImageSource? _thumbnail;
    public ImageSource? Thumbnail { get => _thumbnail; internal set => SetProperty(ref _thumbnail, value); }
    public IRelayCommand RemoveCommand { get; }
    public IAsyncRelayCommand OpenCommand { get; }
}
public sealed partial class SessionPreparationViewModel
{
    private ITradingPlanImages? _images;
    private readonly List<PlanScreenshotItem> _screenshots = [];
    public static IReadOnlyList<PlanTimeframeChoice> Timeframes { get; } = [
        new(TradingPlanTimeframe.M1, "1m"), new(TradingPlanTimeframe.M5, "5m"), new(TradingPlanTimeframe.M15, "15m"),
        new(TradingPlanTimeframe.H1, "1h"), new(TradingPlanTimeframe.H4, "4h"), new(TradingPlanTimeframe.D, "D"), new(TradingPlanTimeframe.W, "W")];
    public IReadOnlyList<PlanTimeframeChoice> AvailableTimeframes => Timeframes;
    private PlanTimeframeChoice _timeframe = Timeframes[0];
    public PlanTimeframeChoice SelectedTimeframe { get => _timeframe; set { if (CanEdit && Timeframes.Contains(value)) SetProperty(ref _timeframe, value); } }
    public IReadOnlyList<PlanScreenshotItem> VisibleScreenshots => _screenshots.Where(s => s.InstrumentId == SelectedInstrument?.InstrumentId).ToArray();
    public IAsyncRelayCommand UploadScreenshotCommand { get; private set; } = null!;
    private void InitializeImages()
    {
        foreach (var s in _original.Screenshots) _screenshots.Add(new(this, s.Id, s.InstrumentId, s.Timeframe, s.Image, null));
        UploadScreenshotCommand = new AsyncRelayCommand(UploadScreenshotAsync,
            () => CanEdit && SelectedInstrument is not null && _images is not null && _screenshots.Count < TradingPlanContent.MaximumScreenshots);
    }
    public void ConfigureImages(ITradingPlanImages images) { _images = images; NotifyImages(); }
    public async Task LoadThumbnailsAsync(CancellationToken ct)
    {
        if (_images is null) return;
        foreach (var s in _screenshots.Where(s => s.Image is not null && s.Thumbnail is null))
        {
            ct.ThrowIfCancellationRequested();
            try { s.Thumbnail = await _images.ThumbnailAsync(s.Image!, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { Report("A saved screenshot is unavailable. Its historical reference is retained.", PlanNoticeKind.Error); }
        }
    }
    private async Task UploadScreenshotAsync(CancellationToken ct)
    {
        if (!CanEdit || SelectedInstrument is not { } instrument || _images is null || _screenshots.Count >= TradingPlanContent.MaximumScreenshots) return;
        var timeframe = SelectedTimeframe.Value;
        using var operation = Begin(ct);
        try
        {
            var picked = await _images.PickAsync(operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            if (picked is null) return;
            _screenshots.Add(new(this, Guid.NewGuid(), instrument.InstrumentId, timeframe, null, picked));
            Report("Screenshot added to the working Draft. Save Draft to retain it; original image bytes are unchanged.", PlanNoticeKind.Info);
        }
        catch (OperationCanceledException) { Report("Screenshot upload cancelled. Existing preparation is unchanged.", PlanNoticeKind.Info); }
        catch (Exception) { Report("Choose a valid PNG/JPEG image up to 8 MiB, 8192 pixels per side and 16 million pixels. Existing content is retained.", PlanNoticeKind.Warning); }
        finally { End(); NotifyImages(); Changed(); }
    }
    internal void RemoveScreenshot(PlanScreenshotItem item)
    {
        if (!CanEdit || !_screenshots.Remove(item)) return;
        NotifyImages(); Changed();
        Report("Removed from this Draft only. Preserved historical assets are not deleted.", PlanNoticeKind.Success);
    }
    internal async Task OpenScreenshotAsync(PlanScreenshotItem item, CancellationToken ct)
    {
        if (IsBusy || _images is null || !_screenshots.Contains(item)) return;
        using var operation = Begin(ct);
        try { await _images.ShowAsync(item.Image, item.Pending, $"Trading Plan screenshot · {item.Label}", operation.Token); }
        catch (OperationCanceledException) { Report("Image preview cancelled.", PlanNoticeKind.Info); }
        catch (Exception) { Report("The screenshot could not be opened. Its saved reference and Draft content are unchanged.", PlanNoticeKind.Error); }
        finally { End(); }
    }
    private void NotifyImages()
    {
        OnPropertyChanged(nameof(VisibleScreenshots)); UploadScreenshotCommand?.NotifyCanExecuteChanged();
        foreach (var item in _screenshots) { item.RemoveCommand.NotifyCanExecuteChanged(); item.OpenCommand.NotifyCanExecuteChanged(); }
    }
    private async Task StageImagesAsync(List<PlanScreenshotItem> staged, CancellationToken ct)
    {
        foreach (var item in _screenshots.Where(s => s.Pending is not null && s.Image is null))
        {
            item.Image = await _images!.StoreAsync(item.Pending!, ct);
            staged.Add(item); // Store returns a published key even for late cancellation, for compensation.
            ct.ThrowIfCancellationRequested();
        }
    }
    private async Task CompensateImagesAsync(List<PlanScreenshotItem> staged)
    {
        foreach (var item in staged)
        {
            try { await _images!.CleanupAsync(item.Image!.StorageKey); item.Image = null; }
            catch (Exception) { Report(Message + " Temporary screenshot cleanup could not be verified; the image is retained for retry.", PlanNoticeKind.Error); }
        }
    }
}
