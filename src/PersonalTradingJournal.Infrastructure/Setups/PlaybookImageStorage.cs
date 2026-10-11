using System.Security.Cryptography;
using PersonalTradingJournal.Application.Common.Storage;
using PersonalTradingJournal.Application.Screenshots;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Infrastructure.Backups;

namespace PersonalTradingJournal.Infrastructure.Setups;

public sealed class PlaybookImageStorage(IApplicationPaths paths, ITradeScreenshotFileStorage files) : IPlaybookImageStorage
{
    public async Task<PlaybookExampleImage> StoreAsync(string fileName, ReadOnlyMemory<byte> bytes, CancellationToken token = default)
    {
        PlaybookExampleImage.ValidateFileName(fileName);
        if (bytes.Length is <= 0 or > PlaybookExampleImage.MaximumBytes) throw new ArgumentOutOfRangeException(nameof(bytes));
        token.ThrowIfCancellationRequested();
        SqliteDatabaseSnapshotService.EnsureNoLinks(paths.ScreenshotsDirectory);
        // Copy owned memory, never re-encode or modify the chosen image.
        byte[] owned = bytes.ToArray();
        string hash = Convert.ToHexString(SHA256.HashData(owned));
        using var stream = new MemoryStream(owned, writable: false);
        string key = await files.StoreAsync(stream, Path.GetExtension(fileName), token);
        // Do not report cancellation after publishing a file: return its key for caller compensation.
        return new(key, fileName, owned.Length, hash);
    }

    public async Task<byte[]> ReadAsync(PlaybookExampleImage image, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        string path = Path.Combine(paths.ScreenshotsDirectory, image.StorageKey);
        SqliteDatabaseSnapshotService.EnsureNoLinks(path);
        await using var input = await files.OpenReadAsync(image.StorageKey, token) ?? throw new FileNotFoundException("Example image is unavailable.");
        if (input is not FileStream file || !BackupFileIdentity.IsUnaliased(file, path) || input.Length != image.SizeBytes)
            throw new InvalidDataException("Example image content no longer matches its saved metadata.");
        byte[] bytes = new byte[checked((int)image.SizeBytes)];
        await input.ReadExactlyAsync(bytes, token);
        if (input.ReadByte() != -1 || Convert.ToHexString(SHA256.HashData(bytes)) != image.Sha256)
            throw new InvalidDataException("Example image content no longer matches its saved metadata.");
        token.ThrowIfCancellationRequested();
        return bytes;
    }

    // The production file service rechecks both ownership catalogs under capture/writer locks.
    public Task DeleteUnreferencedAsync(string storageKey, CancellationToken token = default) => files.DeleteIfExistsAsync(storageKey, token);
}
