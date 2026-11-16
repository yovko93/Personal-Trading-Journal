using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Screenshots;

namespace PersonalTradingJournal.Desktop.ViewModels.Trades;

public sealed record StagedTradeScreenshot(ManualTradeScreenshot Draft, ImageSource? Image);
public sealed class TradeScreenshotCard(TradeScreenshotListItem item, Func<Task<ImageSource?>> load)
    : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public TradeScreenshotListItem Item { get; } = item;
    private bool _started;
    private ImageSource? _image;
    public ImageSource? Image
    {
        get { if (!_started) { _started = true; _ = LoadAsync(); } return _image; }
    }
    private async Task LoadAsync() { _image = await load(); OnPropertyChanged(nameof(Image)); }
}

public sealed partial class TradesViewModel
{
    private string _tradeDescriptionText = string.Empty;
    public string TradeDescriptionText { get => _tradeDescriptionText; set => SetProperty(ref _tradeDescriptionText, value); }
    private ManualTradeScreenshot? _uploadedScreenshot;
    private ImageSource? _uploadedScreenshotImage;
    public ImageSource? UploadedScreenshotImage { get => _uploadedScreenshotImage; private set => SetProperty(ref _uploadedScreenshotImage, value); }
    private string? _uploadedScreenshotFileName;
    public string? UploadedScreenshotFileName { get => _uploadedScreenshotFileName; private set => SetProperty(ref _uploadedScreenshotFileName, value); }
    public ObservableCollection<StagedTradeScreenshot> StagedScreenshots { get; } = [];
    private IReadOnlyList<TradeScreenshotCard> _screenshotCards = [];
    public IReadOnlyList<TradeScreenshotCard> ScreenshotCards { get => _screenshotCards; private set => SetProperty(ref _screenshotCards, value); }
    public IAsyncRelayCommand UploadScreenshotCommand { get; }
    public IRelayCommand<StagedTradeScreenshot> RemoveStagedScreenshotCommand { get; }
    public IRelayCommand<StagedTradeScreenshot> OpenStagedScreenshotCommand { get; }

    private async Task UploadScreenshotAsync(CancellationToken token)
    {
        ScreenshotSaveErrorMessage = null;
        IsAddingScreenshot = true;
        try
        {
            await using var selection = _tradeScreenshotFilePicker.Pick();
            if (selection is null) return;
            using var memory = new MemoryStream();
            byte[] buffer = new byte[81920];
            int read;
            while ((read = await selection.Content.ReadAsync(buffer, token)) > 0)
            {
                if (memory.Length + read > 20 * 1024 * 1024) throw new InvalidDataException("Choose an image up to 20 MB.");
                await memory.WriteAsync(buffer.AsMemory(0, read), token);
            }
            memory.Position = 0;
            var image = _tradeScreenshotImageDecoder.Decode(memory);
            _uploadedScreenshot = new(TradeScreenshotType.Other, null, selection.FileName, memory.ToArray());
            UploadedScreenshotImage = image;
            UploadedScreenshotFileName = selection.FileName;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { ScreenshotSaveErrorMessage = "Screenshot could not be opened. Choose a supported image up to 20 MB."; }
        finally { IsAddingScreenshot = false; }
    }

    private Task LoadScreenshotCardsAsync(IReadOnlyList<TradeScreenshotListItem> items, Guid tradeId, CancellationToken token)
    {
        ScreenshotCards = items.Select(item => new TradeScreenshotCard(item, async () =>
        {
            try
            {
                var content = await _tradeScreenshotContentReader.OpenAsync(item.Id, token);
                if (content is not null && content.TradeId == tradeId)
                {
                    await using (content.Content) { return _tradeScreenshotImageDecoder.Decode(content.Content); }
                }
                if (content is not null) await content.Content.DisposeAsync();
            }
            catch (Exception) { /* The card retains its guarded View/Remove actions for unavailable files. */ }
            return null;
        })).ToArray();
        return Task.CompletedTask;
    }
}
