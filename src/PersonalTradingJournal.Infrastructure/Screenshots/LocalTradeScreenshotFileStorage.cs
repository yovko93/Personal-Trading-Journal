using PersonalTradingJournal.Application.Common.Storage;
using PersonalTradingJournal.Application.Screenshots;

namespace PersonalTradingJournal.Infrastructure.Screenshots;

public sealed class LocalTradeScreenshotFileStorage : ITradeScreenshotFileStorage
{
    private const int FileBufferSize = 81920;

    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".webp",
        };

    private readonly string _screenshotsDirectory;

    public LocalTradeScreenshotFileStorage(IApplicationPaths applicationPaths)
    {
        ArgumentNullException.ThrowIfNull(applicationPaths);

        if (string.IsNullOrWhiteSpace(applicationPaths.ScreenshotsDirectory))
        {
            throw new ArgumentException(
                "The screenshots directory must be provided.",
                nameof(applicationPaths));
        }

        _screenshotsDirectory = Path.GetFullPath(applicationPaths.ScreenshotsDirectory);
    }

    public async Task<string> StoreAsync(
        Stream content,
        string fileExtension,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!content.CanRead)
        {
            throw new ArgumentException("The content stream must be readable.", nameof(content));
        }

        string normalizedExtension = NormalizeExtension(fileExtension);
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_screenshotsDirectory);

        string storageKey = $"{Guid.NewGuid():N}{normalizedExtension}";
        string finalPath = Path.Combine(_screenshotsDirectory, storageKey);
        string temporaryPath = Path.Combine(
            _screenshotsDirectory,
            $".{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var temporaryStream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                FileBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await content.CopyToAsync(temporaryStream, cancellationToken);
                await temporaryStream.FlushAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, finalPath);

            return storageKey;
        }
        catch
        {
            TryDeleteTemporaryFile(temporaryPath);
            throw;
        }
    }

    public Task<Stream?> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        string filePath = ResolveStoragePath(storageKey);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (!File.Exists(filePath))
            {
                return Task.FromResult<Stream?>(null);
            }

            Stream stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                FileBufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            return Task.FromResult<Stream?>(stream);
        }
        catch (FileNotFoundException)
        {
            return Task.FromResult<Stream?>(null);
        }
        catch (DirectoryNotFoundException)
        {
            return Task.FromResult<Stream?>(null);
        }
    }

    public Task DeleteIfExistsAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        string filePath = ResolveStoragePath(storageKey);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            File.Delete(filePath);
        }
        catch (FileNotFoundException)
        {
            // Idempotent compensating cleanup treats an already-missing file as success.
        }
        catch (DirectoryNotFoundException)
        {
            // A missing screenshots directory also means the requested file is absent.
        }

        return Task.CompletedTask;
    }

    private static string NormalizeExtension(string fileExtension)
    {
        if (string.IsNullOrWhiteSpace(fileExtension))
        {
            throw new ArgumentException("A file extension is required.", nameof(fileExtension));
        }

        if (!string.Equals(fileExtension, fileExtension.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The file extension cannot contain surrounding whitespace.",
                nameof(fileExtension));
        }

        string normalizedExtension = fileExtension.StartsWith('.')
            ? fileExtension.ToLowerInvariant()
            : $".{fileExtension.ToLowerInvariant()}";

        if (!SupportedExtensions.Contains(normalizedExtension))
        {
            throw new ArgumentException(
                "The file extension must be .png, .jpg, .jpeg, or .webp.",
                nameof(fileExtension));
        }

        return normalizedExtension;
    }

    private string ResolveStoragePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new ArgumentException("A storage key is required.", nameof(storageKey));
        }

        if (!string.Equals(storageKey, storageKey.Trim(), StringComparison.Ordinal) ||
            Path.IsPathRooted(storageKey) ||
            storageKey is "." or ".." ||
            storageKey.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
            storageKey.IndexOf(Path.AltDirectorySeparatorChar) >= 0 ||
            storageKey.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !string.Equals(Path.GetFileName(storageKey), storageKey, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The storage key must be a trimmed single file name.",
                nameof(storageKey));
        }

        string filePath = Path.GetFullPath(Path.Combine(_screenshotsDirectory, storageKey));
        string relativePath = Path.GetRelativePath(_screenshotsDirectory, filePath);
        if (Path.IsPathRooted(relativePath) ||
            relativePath is "." or ".." ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal) ||
            relativePath.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
            relativePath.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
        {
            throw new ArgumentException(
                "The storage key must resolve directly inside the screenshots directory.",
                nameof(storageKey));
        }

        return filePath;
    }

    private static void TryDeleteTemporaryFile(string temporaryPath)
    {
        try
        {
            File.Delete(temporaryPath);
        }
        catch
        {
            // Cleanup is best-effort and must not replace the original storage failure.
        }
    }
}
