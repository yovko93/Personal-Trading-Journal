using System.IO;
using PersonalTradingJournal.Application.DailyReview.Coaching;

namespace PersonalTradingJournal.Desktop.Settings;

/// <summary>Provider choice is nonsecret; each secret has its own DPAPI file and environment fallback.</summary>
public sealed class CoachingConfiguration : ICoachingConfiguration
{
    private readonly string _selectionPath;
    private readonly ProtectedCoachingCredentials _openAi, _groq;
    private readonly object _gate = new();
    public CoachingProviderKind SelectedProvider { get; private set; }
    public CoachingConfiguration(string selectionPath, ProtectedCoachingCredentials openAi,
        ProtectedCoachingCredentials groq, bool existingInstallation)
    {
        _selectionPath = selectionPath; _openAi = openAi; _groq = groq;
        try
        {
            using var stream = new FileStream(selectionPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > 32) throw new InvalidDataException();
            using var reader = new StreamReader(stream);
            SelectedProvider = reader.ReadToEnd() switch
            {
                "Groq" => CoachingProviderKind.Groq, "OpenAI" => CoachingProviderKind.OpenAI,
                _ => CoachingProviderKind.Unavailable,
            };
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            SelectedProvider = existingInstallation || openAi.GetSource() != CoachingCredentialSource.None
                ? CoachingProviderKind.OpenAI : CoachingProviderKind.Groq;
            // Persist the inferred legacy/new default so removing a key cannot switch providers on restart.
            if (!Select(SelectedProvider)) SelectedProvider = CoachingProviderKind.Unavailable;
        }
        catch (Exception) { SelectedProvider = CoachingProviderKind.Unavailable; }
    }

    public bool Select(CoachingProviderKind provider)
    {
        if (provider is not (CoachingProviderKind.OpenAI or CoachingProviderKind.Groq)) return false;
        lock (_gate)
        {
            string? temporary = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_selectionPath)!);
                temporary = _selectionPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using var writer = new StreamWriter(stream, leaveOpen: true);
                    writer.Write(provider.ToString()); writer.Flush(); stream.Flush(true);
                }
                File.Move(temporary, _selectionPath, overwrite: true);
                SelectedProvider = provider;
                return true;
            }
            catch (Exception) { return false; }
            finally { if (temporary is not null) try { File.Delete(temporary); } catch (Exception) { } }
        }
    }
    public ProtectedCoachingCredentials StoreFor(CoachingProviderKind provider) => provider switch
    {
        CoachingProviderKind.OpenAI => _openAi, CoachingProviderKind.Groq => _groq,
        _ => throw new CoachingCredentialException(),
    };
    public ICoachingCredentials CredentialsFor(CoachingProviderKind provider) => StoreFor(provider);
    public CoachingCredentialSource GetSource() => SelectedProvider == CoachingProviderKind.Unavailable
        ? CoachingCredentialSource.Unreadable : StoreFor(SelectedProvider).GetSource();
    public string? Resolve() => StoreFor(SelectedProvider).Resolve();
}
