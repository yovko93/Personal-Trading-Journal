namespace PersonalTradingJournal.Domain.Setups;

public enum PlaybookExampleKind { Unspecified = 0, Valid = 1, Invalid = 2 }

/// <summary>Frozen metadata for original image bytes. A revision owns a retention reference, not a mutable file.</summary>
public sealed record PlaybookExampleImage
{
    public const int MaximumBytes = 8 * 1024 * 1024;
    public PlaybookExampleImage(string storageKey, string fileName, long sizeBytes, string sha256)
    {
        ValidateFileName(storageKey); ValidateFileName(fileName);
        if (sizeBytes is <= 0 or > MaximumBytes) throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        if (sha256 is null || sha256.Length != 64 || !sha256.All(Uri.IsHexDigit))
            throw new ArgumentException("An SHA-256 image digest is required.", nameof(sha256));
        StorageKey = storageKey; FileName = fileName; SizeBytes = sizeBytes; Sha256 = sha256.ToUpperInvariant();
    }
    public string StorageKey { get; }
    public string FileName { get; }
    public long SizeBytes { get; }
    public string Sha256 { get; }
    public static void ValidateFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 180 || name != name.Trim() ||
            name.Any(c => c < 32 || "<>:\"/\\|?*".Contains(c)) || name is "." or ".." ||
            !new[] { ".png", ".jpg", ".jpeg", ".webp" }.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Use a single PNG, JPEG or WebP filename (at most 180 characters).", nameof(name));
    }
}
