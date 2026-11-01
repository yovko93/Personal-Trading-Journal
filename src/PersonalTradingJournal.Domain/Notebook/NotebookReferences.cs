namespace PersonalTradingJournal.Domain.Notebook;

public enum NotebookTargetKind { Trade = 1, TradingSetup = 2 }

public sealed record NotebookLink
{
    public NotebookLink(NotebookTargetKind kind, Guid targetId, string lastKnownLabel)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        Kind = kind; TargetId = NotebookLimits.Id(targetId, nameof(targetId));
        LastKnownLabel = NotebookLimits.Text(lastKnownLabel, 2000, nameof(lastKnownLabel), true);
    }
    public NotebookTargetKind Kind { get; }
    public Guid TargetId { get; }
    public string LastKnownLabel { get; }
}

public enum NotebookImageOwnership { NoteOwned = 1, TradeScreenshotReference = 2 }

/// <summary>Validated metadata only. File admission, ownership verification and retention are store responsibilities.</summary>
public sealed record NotebookImage
{
    private NotebookImage(Guid id, int position, string? caption, string? alternativeText,
        string? storageKey, long? sizeBytes, string? sha256, Guid? tradeId, Guid? screenshotId)
    {
        Id = NotebookLimits.Id(id, nameof(id));
        if (position is < 0 or >= NotebookLimits.Images) throw new ArgumentOutOfRangeException(nameof(position));
        Position = position;
        Caption = NotebookLimits.Text(caption ?? "", NotebookLimits.Caption, nameof(caption));
        AlternativeText = NotebookLimits.Text(alternativeText ?? "", NotebookLimits.AlternativeText, nameof(alternativeText));
        if (storageKey is not null)
        {
            NotebookLimits.Text(storageKey, 180, nameof(storageKey), true);
            int dot = storageKey.LastIndexOf('.');
            if (storageKey != storageKey.Trim() || storageKey.Any(c => c < 32 || "<>:\"/\\|?*".Contains(c)) ||
                dot <= 0 || !new[] { ".png", ".jpg", ".jpeg", ".webp" }.Contains(storageKey[dot..], StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException("A single managed PNG/JPEG/WebP storage key is required.", nameof(storageKey));
            if (sizeBytes is null or <= 0 or > NotebookLimits.ImageBytes) throw new ArgumentOutOfRangeException(nameof(sizeBytes));
            if (sha256 is null || sha256.Length != 64 || !sha256.All(Uri.IsHexDigit))
                throw new ArgumentException("SHA-256 is required.", nameof(sha256));
            Ownership = NotebookImageOwnership.NoteOwned; StorageKey = storageKey; SizeBytes = sizeBytes; Sha256 = sha256.ToUpperInvariant();
        }
        else
        {
            TradeId = NotebookLimits.Id(tradeId ?? Guid.Empty, nameof(tradeId));
            TradeScreenshotId = NotebookLimits.Id(screenshotId ?? Guid.Empty, nameof(screenshotId));
            Ownership = NotebookImageOwnership.TradeScreenshotReference;
        }
    }
    public static NotebookImage Owned(Guid id, int position, string storageKey, long sizeBytes, string sha256,
        string? caption = null, string? alternativeText = null)
    {
        ArgumentNullException.ThrowIfNull(storageKey);
        return new(id, position, caption, alternativeText, storageKey, sizeBytes, sha256, null, null);
    }
    public static NotebookImage TradeReference(Guid id, int position, Guid tradeId, Guid screenshotId,
        string? caption = null, string? alternativeText = null) =>
        new(id, position, caption, alternativeText, null, null, null, tradeId, screenshotId);
    public Guid Id { get; }
    public int Position { get; }
    public string Caption { get; }
    public string AlternativeText { get; }
    public NotebookImageOwnership Ownership { get; }
    public string? StorageKey { get; }
    public long? SizeBytes { get; }
    public string? Sha256 { get; }
    public Guid? TradeId { get; }
    public Guid? TradeScreenshotId { get; }
}
