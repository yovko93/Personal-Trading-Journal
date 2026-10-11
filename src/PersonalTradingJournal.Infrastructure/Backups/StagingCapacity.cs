using System.Runtime.InteropServices;
using PersonalTradingJournal.Application.Backups;

namespace PersonalTradingJournal.Infrastructure.Backups;

/// <summary>Advisory capacity, not a reservation. Always query the actual staging directory, not the archive drive.</summary>
internal static class StagingCapacity
{
    internal const long DefaultReserveBytes = 64L * 1024 * 1024;
    internal const long PerFileAllowance = 4096;

    internal static long Required(IEnumerable<BackupFileEntry> files, int copies, long reserve)
    {
        if (copies < 1 || reserve < 0) throw new PreflightFailure(BackupValidationCode.CapacityUnavailable);
        try
        {
            long bytes = 0;
            foreach (var file in files) bytes = checked(bytes + file.SizeBytes + PerFileAllowance);
            return checked(bytes * copies + reserve);
        }
        catch (OverflowException) { throw new PreflightFailure(BackupValidationCode.LimitExceeded); }
    }

    internal static void Require(string directory, long required, Func<string, long> available)
    {
        long bytes;
        try { bytes = available(directory); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
        { throw new PreflightFailure(BackupValidationCode.CapacityUnavailable); }
        if (bytes < 0 || required < 0) throw new PreflightFailure(BackupValidationCode.CapacityUnavailable);
        if (bytes < required) throw new PreflightFailure(BackupValidationCode.InsufficientStorage);
    }

    internal static long Available(string directory)
    {
        string path = Path.GetFullPath(directory);
        if (OperatingSystem.IsWindows())
        {
            // GetDiskFreeSpaceEx follows the volume containing this directory (including mounted volumes),
            // and returns bytes available to this caller, accounting for quotas. No root-drive inference.
            if (!GetDiskFreeSpaceEx(path, out ulong available, out _, out _) || available > long.MaxValue)
                throw new IOException("Staging capacity unavailable.");
            return (long)available;
        }
        var drive = DriveInfo.GetDrives().Where(d => path == d.Name ||
            path.StartsWith(Path.TrimEndingDirectorySeparator(d.Name) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .OrderByDescending(d => d.Name.Length).FirstOrDefault();
        return drive?.AvailableFreeSpace ?? throw new IOException("Staging capacity unavailable.");
    }

    internal static bool IsDiskFull(IOException error) => (error.HResult & 0xffff) is 39 or 112;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceEx(string directory, out ulong available, out ulong total, out ulong free);
}
