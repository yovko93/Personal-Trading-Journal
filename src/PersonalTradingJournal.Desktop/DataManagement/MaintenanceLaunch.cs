using System.Diagnostics;
using System.IO;
using PersonalTradingJournal.Application.Backups;

namespace PersonalTradingJournal.Desktop.DataManagement;

/// <summary>Only a fresh process may enter maintenance; it never creates the normal DI host.</summary>
public sealed record MaintenanceLaunch(string[] NormalArguments, JournalRestoreRequest? Restore = null, int? ParentId = null)
{
    private const string Switch = "--ptj-maintenance-restore";
    public static MaintenanceLaunch Parse(string[] args)
    {
        if (args.Length == 0 || args[0] != Switch) return new(args);
        if (args.Length < 4 || !int.TryParse(args[1], out int pid) || pid <= 0 ||
            !Path.IsPathFullyQualified(args[2]) || args[3].Length != 64 || !args[3].All(Uri.IsHexDigit))
            throw new ArgumentException("Invalid offline maintenance arguments.");
        return new(args[4..], new(args[2], args[3]), pid);
    }
    public static string[] RestoreArguments(string[] normal, JournalRestoreRequest request, int parentId) =>
        [Switch, parentId.ToString(System.Globalization.CultureInfo.InvariantCulture), request.ArchivePath, request.ConfirmedArchiveSha256, .. normal];
    public static void Start(string[] args)
    {
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "PersonalTradingJournal.Desktop.exe"))
        { UseShellExecute = false, CreateNoWindow = true };
        foreach (string arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("Application launch failed.");
    }
    public static async Task WaitForParentAsync(int? id, CancellationToken token)
    {
        if (id is null) return;
        Process parent;
        try { parent = Process.GetProcessById(id.Value); }
        catch (ArgumentException) { return; } // Already exited; exclusive maintenance still checks all sessions.
        using (parent) await parent.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(30), token);
    }
    public override string ToString() => "Offline maintenance launch (local paths omitted)";
}
