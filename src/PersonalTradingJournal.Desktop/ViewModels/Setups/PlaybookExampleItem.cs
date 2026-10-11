using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;
using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Desktop.ViewModels.Setups;

public sealed class PlaybookExampleItem : ObservableObject
{
    private string _annotation, _imageState = "";
    private PlaybookExampleKind _kind;
    private ImageSource? _thumbnail;
    public PlaybookExampleItem(PlaybookExample source) { Source = source; _annotation = source.Annotation; _kind = source.Kind; }
    public PlaybookExampleItem(string fileName, byte[] bytes, ImageSource thumbnail)
    { Id = Guid.NewGuid(); FileName = fileName; PendingBytes = bytes; _annotation = ""; _thumbnail = thumbnail; }
    public PlaybookExample? Source { get; }
    private Guid Id { get; } = Guid.Empty;
    public Guid ExampleId => Source?.Id ?? Id;
    public string FileName { get; } = "";
    internal byte[]? PendingBytes { get; }
    public bool HasImage => PendingBytes is not null || Source?.Image is not null || Source?.SourceScreenshotId is not null;
    public PlaybookExampleKind[] Kinds { get; } = [PlaybookExampleKind.Valid, PlaybookExampleKind.Invalid];
    public PlaybookExampleKind Kind { get => _kind; set { SetProperty(ref _kind, value); OnPropertyChanged(nameof(KindLabel)); } }
    public string KindLabel => Kind switch { PlaybookExampleKind.Valid => "Valid example · user classification", PlaybookExampleKind.Invalid => "Invalid example · user classification", _ => "Legacy example · not classified" };
    public string Annotation { get => _annotation; set => SetProperty(ref _annotation, value); }
    public ImageSource? Thumbnail { get => _thumbnail; set => SetProperty(ref _thumbnail, value); }
    public string ImageState { get => _imageState; set => SetProperty(ref _imageState, value); }
    internal PlaybookExample ToDomain() => new(ExampleId, Annotation, Source?.SourceTradeId, Source?.SourceScreenshotId, Kind, Source?.Image);
    internal bool IsUnchanged => Source is not null && Kind == Source.Kind && Annotation == Source.Annotation;
}
