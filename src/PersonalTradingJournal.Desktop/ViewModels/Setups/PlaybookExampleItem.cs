using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;
using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Desktop.ViewModels.Setups;

public sealed class PlaybookExampleItem : ObservableObject
{
    private string _annotation, _imageState = "";
    private PlaybookExampleKind _kind;
    private ImageSource? _thumbnail;
    private bool _validate;
    private string _attachmentError = "", _kindError = "", _annotationError = "";
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
    public PlaybookExampleKind Kind { get => _kind; set { SetProperty(ref _kind, value); OnPropertyChanged(nameof(KindLabel)); if (_validate) Validate(); } }
    public string KindLabel => Kind switch { PlaybookExampleKind.Valid => "Valid example · user classification", PlaybookExampleKind.Invalid => "Invalid example · user classification", _ => "Legacy example · not classified" };
    public string Annotation { get => _annotation; set { if (SetProperty(ref _annotation, value) && _validate) Validate(); } }
    public bool RequiresClassification => PendingBytes is not null || Source?.Image is not null;
    public string SourceLabel => Source?.SourceScreenshotId is not null ? "Example from a Trade screenshot" : Source?.SourceTradeId is not null ? "Example linked to a Trade" : "";
    public string AttachmentError => _attachmentError;
    public string KindError => _kindError;
    public string AnnotationError => _annotationError;
    public bool HasAttachmentError => AttachmentError.Length > 0;
    public bool HasKindError => KindError.Length > 0;
    public bool HasAnnotationError => AnnotationError.Length > 0;
    public bool HasErrors => HasAttachmentError || HasKindError || HasAnnotationError;
    public string ErrorSummary => HasErrors ? "Complete the marked chart example fields before saving." : "";
    public void Validate()
    {
        _validate = true;
        string attachment = "";
        if (PendingBytes is not null)
        {
            if (PendingBytes.Length is <= 0 or > PlaybookExampleImage.MaximumBytes)
                attachment = "Attach a nonempty supported chart image of at most 8 MiB. Remove this example and attach the image again.";
            else
                try { PlaybookExampleImage.ValidateFileName(FileName); }
                catch (ArgumentException) { attachment = "Attach a supported chart image again using Attach chart image."; }
        }
        _attachmentError = attachment;
        _kindError = !Enum.IsDefined(Kind) || (RequiresClassification && Kind == PlaybookExampleKind.Unspecified)
            ? "Choose Valid or Invalid for this chart." : "";
        _annotationError = string.IsNullOrWhiteSpace(Annotation) ? "Describe the context, condition and lesson." :
            Annotation.Trim().Length > PlaybookGuidance.MaximumFieldLength ? "Use at most 4,000 characters for the annotation." : "";
        foreach (string property in new[] { nameof(AttachmentError), nameof(KindError), nameof(AnnotationError), nameof(HasAttachmentError), nameof(HasKindError), nameof(HasAnnotationError), nameof(HasErrors), nameof(ErrorSummary) }) OnPropertyChanged(property);
    }
    public ImageSource? Thumbnail { get => _thumbnail; set => SetProperty(ref _thumbnail, value); }
    public string ImageState { get => _imageState; set => SetProperty(ref _imageState, value); }
    internal PlaybookExample ToDomain() => new(ExampleId, Annotation, Source?.SourceTradeId, Source?.SourceScreenshotId, Kind, Source?.Image);
    internal bool IsUnchanged => Source is not null && Kind == Source.Kind && Annotation == Source.Annotation;
}
