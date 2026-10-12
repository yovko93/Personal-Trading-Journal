using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PersonalTradingJournal.Desktop.ViewModels.Setups;

// Closed, text-free vocabulary: never accept an exception, path, name or authored text.
public enum PlaybookSavePhase { Invoked, ValidationRejected, WriteStarted, Committed, Readback, Reloaded, Finished }
public enum PlaybookSaveResult { Pending, InvalidFields, InvalidExamples, Verified, ReadbackMissing, ReadbackFailed, ReloadIncomplete, Cancelled, Invalid, Conflict, Unavailable, StorageFailure }
public sealed record PlaybookSaveEvent(Guid Attempt, PlaybookSavePhase Phase, PlaybookSaveResult Result,
    Guid? SetupId, int ExpectedRevision, int? Revision, Guid? SelectedSetupId,
    bool Editing, bool Ready, double ElapsedMilliseconds);

/// <summary>Opt-in bounded local diagnostic, separate from ordinary application logs.</summary>
public sealed class PlaybookSaveDiagnostics
{
    public const int MaximumEvents = 256;
    public const long MaximumBytes = 512 * 1024;
    private readonly string? _file;
    private readonly object _gate = new();
    private int _events;
    private static readonly JsonSerializerOptions Json = new() { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    public PlaybookSaveDiagnostics(string directory, string databasePath, bool enabled)
    {
        if (!enabled) return;
        _file = Path.Combine(directory, "playbook-save-diagnostics.jsonl");
        var assembly = typeof(PlaybookSaveDiagnostics).Assembly;
        Write(new { Kind = "Runtime", Pid = Environment.ProcessId, Build = assembly.ManifestModule.ModuleVersionId,
            BinarySha256 = HashFile(assembly.Location), ExecutablePathFingerprint = PathFingerprint(Environment.ProcessPath ?? assembly.Location),
            DatabasePathFingerprint = PathFingerprint(databasePath) });
    }

    public static string PathFingerprint(string path) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant())));
    private static string? HashFile(string path)
    {
        try { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }
    public void Record(PlaybookSaveEvent entry) => Write(entry);
    private void Write<T>(T entry)
    {
        if (_file is null) return;
        lock (_gate)
        {
            if (_events++ >= MaximumEvents) return;
            try
            {
                string line = JsonSerializer.Serialize(new { Utc = DateTimeOffset.UtcNow, Data = entry }, Json) + Environment.NewLine;
                using var stream = new FileStream(_file, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
                byte[] bytes = Encoding.UTF8.GetBytes(line);
                if (stream.Length + bytes.Length > MaximumBytes) return;
                stream.Position = stream.Length; stream.Write(bytes);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* Diagnostics never decide a Save outcome. */ }
        }
    }
}
