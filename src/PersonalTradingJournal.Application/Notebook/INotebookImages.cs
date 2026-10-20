using PersonalTradingJournal.Domain.Notebook;
using System.Buffers.Binary;

namespace PersonalTradingJournal.Application.Notebook;

public enum NotebookImageStatus { Success, Invalid, Unsupported, TooLarge, Missing, Unreadable, Cancelled }
public sealed record NotebookImageProbe(NotebookImageStatus Status, string Extension = "", int Width = 0, int Height = 0);
/// <summary>Host decoder boundary. Must fully decode one frame after checking dimensions. No UI or clipboard access.</summary>
public interface INotebookImageValidator
{
    NotebookImageProbe Validate(ReadOnlyMemory<byte> content);
}
/// <summary>Fail closed when the host has not supplied its installed-codec adapter.</summary>
public sealed class UnavailableNotebookImageValidator : INotebookImageValidator
{
    public NotebookImageProbe Validate(ReadOnlyMemory<byte> content) => new(NotebookImageStatus.Unsupported);
}
public sealed record NotebookStoredImage(string StorageKey, long SizeBytes, string Sha256);
public sealed record NotebookImageContent(NotebookImageStatus Status, NotebookStoredImage? Metadata = null, byte[]? Original = null);
/// <summary>The caller owns Content's stream. Document is the entire proposed document, including the new ImageId.
/// Other nodes remain exact unless EditorPlacement supplies the validated editor's selection replacement/paragraph split.
/// Position/caption/alt text derive from the image node, never a separate projection.</summary>
public sealed record ImportNotebookImage(Guid NoteId, long ExpectedVersion, Guid ImageId, NotebookDocument Document, bool EditorPlacement = false);
public interface INotebookImages
{
    Task<NotebookWriteResult> ImportAsync(ImportNotebookImage request, Stream content, CancellationToken token = default);
    /// <summary>Reads current or retained owned bytes without changing any note. Metadata survives reference removal.</summary>
    Task<NotebookImageContent> ReadOriginalAsync(Guid noteId, Guid imageId, CancellationToken token = default);
}
/// <summary>Managed staging/verified original reading. Compensation may delete only after a fresh writer-locked
/// ownership check; uncertain resolution retains bytes and returns false. Never use for committed-reference removal.</summary>
public interface INotebookImageFiles
{
    Task<NotebookStoredImage> StageAsync(Guid noteId, Guid imageId, Stream content, CancellationToken token);
    Task<byte[]> ReadAsync(NotebookStoredImage image, CancellationToken token, bool requireAdmission = false);
    Task<bool> DiscardUncommittedAsync(NotebookStoredImage image, CancellationToken token);
}
public sealed class NotebookImageFileException(NotebookImageStatus status, bool cleanupPending = false) : IOException("Notebook image content is unavailable or invalid.")
{
    public NotebookImageStatus Status { get; } = status;
    public bool CleanupPending { get; } = cleanupPending;
}

/// <summary>Portable container allowlist before the host's actual decoder. Animation may be hidden by codecs
/// that expose only a first frame, so inspect the bounded container chunks as well.</summary>
public static class NotebookImageAdmission
{
    public static string? Extension(ReadOnlySpan<byte> bytes) =>
        bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? ".png" :
        bytes.StartsWith(new byte[] { 255, 216, 255 }) ? ".jpg" :
        bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8) ? ".webp" : null;
    public static bool HasAnimation(ReadOnlySpan<byte> bytes, string extension)
    {
        int offset = extension == ".png" ? 8 : 12;
        while (extension is ".png" or ".webp" && offset <= bytes.Length - 8)
        {
            var chunk = bytes[offset..];
            uint length = extension == ".png" ? BinaryPrimitives.ReadUInt32BigEndian(chunk) : BinaryPrimitives.ReadUInt32LittleEndian(chunk[4..]);
            var kind = extension == ".png" ? chunk.Slice(4, 4) : chunk[..4];
            if (extension == ".png" && kind.SequenceEqual("acTL"u8) || extension == ".webp" &&
                (kind.SequenceEqual("ANIM"u8) || kind.SequenceEqual("ANMF"u8) || kind.SequenceEqual("VP8X"u8) && length >= 1 && chunk.Length > 8 && (chunk[8] & 2) != 0)) return true;
            long step = extension == ".png" ? 12L + length : 8L + length + (length & 1);
            if (step > bytes.Length - offset) return false; // Malformed/truncated containers still require decoder rejection.
            offset += (int)step;
        }
        return false;
    }
}
