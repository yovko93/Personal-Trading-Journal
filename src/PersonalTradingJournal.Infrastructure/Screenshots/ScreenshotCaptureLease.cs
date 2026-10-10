using System.Security.Cryptography;
using System.Text;

namespace PersonalTradingJournal.Infrastructure.Screenshots;

/// <summary>Thread-affine, cross-process deletion barrier. Hold synchronously; never across an await.</summary>
internal sealed class ScreenshotCaptureLease : IDisposable
{
    private readonly Mutex _mutex;
    private ScreenshotCaptureLease(Mutex mutex) => _mutex = mutex;

    internal static ScreenshotCaptureLease Acquire(string root, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        string canonical = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (OperatingSystem.IsWindows()) canonical = canonical.ToUpperInvariant();
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        var mutex = new Mutex(false, (OperatingSystem.IsWindows() ? @"Global\" : "") + "PTJ-ScreenshotCapture-" + digest);
        bool acquired = false;
        try
        {
            try { acquired = WaitHandle.WaitAny([mutex, token.WaitHandle], TimeSpan.FromSeconds(2)) == 0; }
            catch (AbandonedMutexException e) when (e.MutexIndex == 0) { acquired = true; }
            token.ThrowIfCancellationRequested();
            if (!acquired) throw new ScreenshotCaptureBusyException();
            return new(mutex);
        }
        catch { if (acquired) mutex.ReleaseMutex(); mutex.Dispose(); throw; }
    }

    public void Dispose() { _mutex.ReleaseMutex(); _mutex.Dispose(); }
}

internal sealed class ScreenshotCaptureBusyException() : IOException("Screenshot capture is busy; file cleanup can be attempted later.");
