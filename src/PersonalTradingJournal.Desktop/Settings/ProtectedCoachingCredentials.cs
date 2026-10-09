using System.IO;
using System.Security.Cryptography;
using System.Text;
using PersonalTradingJournal.Application.DailyReview.Coaching;

namespace PersonalTradingJournal.Desktop.Settings;

/// <summary>User-owned DPAPI secret, deliberately outside the journal data/backup tree.
/// Only encrypted bytes reach disk. No key is cached, logged, or exposed through configuration status.</summary>
public sealed class ProtectedCoachingCredentials(string filePath, Func<string?> environment) : ICoachingCredentials
{
    private readonly object _gate = new();
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PersonalTradingJournal.AICoaching.v1");
    private const int MaxKeyLength = 4096;

    public CoachingCredentialSource GetSource()
    {
        lock (_gate)
        {
            try
            {
                var (key, saved) = Read();
                return saved ? CoachingCredentialSource.Saved : string.IsNullOrWhiteSpace(key)
                    ? CoachingCredentialSource.None : CoachingCredentialSource.Environment;
            }
            catch (Exception) { return CoachingCredentialSource.Unreadable; }
        }
    }

    public string? Resolve()
    {
        lock (_gate)
        {
            try { return Read().Key; }
            catch (Exception) { throw new CoachingCredentialException(); }
        }
    }

    private (string? Key, bool Saved) Read()
    {
        byte[] encrypted;
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length is <= 0 or > 16384) throw new InvalidDataException();
            encrypted = new byte[(int)stream.Length];
            stream.ReadExactly(encrypted);
        }
        catch (FileNotFoundException) { return (environment(), false); }
        catch (DirectoryNotFoundException) { return (environment(), false); }
        byte[] plain = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
        try
        {
            string key = Encoding.UTF8.GetString(plain);
            if (!Valid(key)) throw new InvalidDataException();
            return (key, true);
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    public bool Save(string key)
    {
        if (!Valid(key)) return false;
        lock (_gate)
        {
            string? temporary = null;
            byte[] plain = Encoding.UTF8.GetBytes(key);
            try
            {
                byte[] encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                temporary = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(encrypted);
                    stream.Flush(flushToDisk: true);
                }
                // Same-directory rename/replacement: an interrupted write cannot leave a partial active key.
                File.Move(temporary, filePath, overwrite: true);
                return true;
            }
            catch (Exception) { return false; }
            finally
            {
                CryptographicOperations.ZeroMemory(plain);
                if (temporary is not null)
                    try { File.Delete(temporary); } catch (Exception) { /* Encrypted temporary only. */ }
            }
        }
    }

    public bool Remove()
    {
        lock (_gate)
        {
            try { File.Delete(filePath); return true; }
            catch (DirectoryNotFoundException) { return true; }
            catch (Exception) { return false; }
        }
    }

    private static bool Valid(string? key) => !string.IsNullOrWhiteSpace(key) && key.Length <= MaxKeyLength
        && key.All(c => c is >= '!' and <= '~');
}
