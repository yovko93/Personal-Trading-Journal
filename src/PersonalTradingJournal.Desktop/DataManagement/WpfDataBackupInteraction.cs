using Microsoft.Win32;
using System.IO;
using PersonalTradingJournal.Application.Backups;

namespace PersonalTradingJournal.Desktop.DataManagement;

public sealed class WpfDataBackupInteraction : IDataBackupInteraction
{
    public string? ChooseBackupDestination()
    {
        var picker = new SaveFileDialog { Title = "Create unencrypted backup — choose a new file", Filter = "PTJ backup (*.ptjbackup)|*.ptjbackup",
            FileName = $"PTJ-{DateTime.UtcNow:yyyyMMdd-HHmmss}.ptjbackup", AddExtension = true, DefaultExt = ".ptjbackup", OverwritePrompt = false };
        return picker.ShowDialog(System.Windows.Application.Current.MainWindow) == true ? picker.FileName : null;
    }
    public string? ChooseExportDestination()
    {
        var picker = new OpenFolderDialog { Title = "Choose parent folder — export creates a new PTJ-export subfolder" };
        return picker.ShowDialog(System.Windows.Application.Current.MainWindow) == true
            ? Path.Combine(picker.FolderName, $"PTJ-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}") : null;
    }
    public string? ChooseRestoreArchive()
    {
        var picker = new OpenFileDialog { Title = "Inspect backup before restore", Filter = "PTJ backup (*.ptjbackup)|*.ptjbackup|All files (*.*)|*.*", CheckFileExists = true };
        return picker.ShowDialog(System.Windows.Application.Current.MainWindow) == true ? picker.FileName : null;
    }
    public bool BeginRestore(JournalRestoreRequest request) =>
        System.Windows.Application.Current is App app && app.RequestOfflineRestore(request);
}
