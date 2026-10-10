using PersonalTradingJournal.Application.Backups;

namespace PersonalTradingJournal.Desktop.DataManagement;

public interface IDataBackupInteraction
{
    string? ChooseBackupDestination();
    string? ChooseExportDestination();
    string? ChooseRestoreArchive();
    // Starts a separate maintenance process and closes the guarded normal window.
    // False means no handoff was accepted, never successful restore.
    bool BeginRestore(JournalRestoreRequest request);
}
