using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace PersonalTradingJournal.Infrastructure.Backups;

internal static class BackupFileIdentity
{
    // Inspect the opened handle, not just the pre-open path. Reject hard links and a root swapped for a junction.
    internal static bool IsUnaliased(FileStream stream, string expectedPath)
    {
        if (!OperatingSystem.IsWindows()) return true; // Windows is the supported application platform.
        return WindowsIdentity(stream.SafeFileHandle, expectedPath);
    }

    [SupportedOSPlatform("windows")]
    private static bool WindowsIdentity(SafeFileHandle handle, string expectedPath)
    {
        if (!GetFileInformationByHandle(handle, out var info) || info.NumberOfLinks != 1 || (info.Attributes & 0x400) != 0)
            return false;
        var name = new StringBuilder(32768);
        uint count = GetFinalPathNameByHandle(handle, name, (uint)name.Capacity, 0);
        if (count == 0 || count >= name.Capacity) return false;
        string path = name.ToString();
        if (path.StartsWith(@"\\?\UNC\", StringComparison.Ordinal)) path = @"\\" + path[8..];
        else if (path.StartsWith(@"\\?\", StringComparison.Ordinal)) path = path[4..];
        return string.Equals(Path.GetFullPath(path), Path.GetFullPath(expectedPath), StringComparison.OrdinalIgnoreCase);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh,
            VolumeSerial, SizeHigh, SizeLow, NumberOfLinks, IndexHigh, IndexLow;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);

    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint size, uint flags);
}
