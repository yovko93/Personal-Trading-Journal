using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Desktop.DataManagement;
using PersonalTradingJournal.Desktop.Dialogs;

namespace PersonalTradingJournal.Desktop.ViewModels.Settings;

public sealed class DataBackupsViewModel : ObservableObject
{
    public const string BackupDisclosure = "Backup contains the database and referenced Trade screenshots, including Journal revisions and saved AI analyses. It is unencrypted and excludes AI API keys.";
    public const string ExportDisclosure = "Export contains JSON, four CSVs and an inventory. Screenshots are metadata only. Exports are unencrypted and cannot be restored as backups.";
    private readonly IBackupArchiveService backup;
    private readonly IPortableExportService export;
    private readonly IRestorePreflightService preflight;
    private readonly IDataBackupInteraction interaction;
    private readonly IDialogService dialogs;
    private readonly string stagingParent;
    private CancellationTokenSource? operation;
    private string? archive;
    private RestorePreflightSummary? summary;
    private string status = "Choose an operation. Nothing runs automatically.";
    private string location = "";
    private bool busy, handingOff;

    public DataBackupsViewModel(IBackupArchiveService backup, IPortableExportService export,
        IRestorePreflightService preflight, IDataBackupInteraction interaction, IDialogService dialogs, string stagingParent)
    {
        this.backup = backup; this.export = export; this.preflight = preflight;
        this.interaction = interaction; this.dialogs = dialogs; this.stagingParent = stagingParent;
        CreateBackupCommand = new AsyncRelayCommand(CreateBackupAsync, () => !IsBusy);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => !IsBusy);
        InspectCommand = new AsyncRelayCommand(InspectAsync, () => !IsBusy);
        RestoreCommand = new RelayCommand(Restore, () => !IsBusy && summary is not null);
        CancelCommand = new RelayCommand(() => { operation?.Cancel(); Status = "Cancellation requested. Waiting for a safe boundary; no incomplete output is reported as successful."; }, () => IsBusy && !handingOff);
    }
    public IAsyncRelayCommand CreateBackupCommand { get; }
    public IAsyncRelayCommand ExportCommand { get; }
    public IAsyncRelayCommand InspectCommand { get; }
    public IRelayCommand RestoreCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public bool IsBusy { get => busy; private set { SetProperty(ref busy, value); NotifyCommands(); } }
    public bool CanCancel => IsBusy && !handingOff;
    public bool HasPreview => summary is not null;
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public string OutputLocation { get => location; private set => SetProperty(ref location, value); }
    public string Preview => summary is null ? "" :
        $"Created {summary.CreatedAtUtc:dd MMM yyyy HH:mm:ss} UTC · archive v{summary.ArchiveVersion}\n" +
        $"Compatible current schema: {summary.LatestMigration}\n" +
        $"{summary.AccountCount:N0} Accounts · {summary.TradeCount:N0} Trades · {summary.JournalCount:N0} Journals · {summary.JournalRevisionCount:N0} revisions\n" +
        $"{summary.AnalysisCount:N0} AI analyses · {summary.ScreenshotCount:N0} screenshots ({summary.ScreenshotFileCount:N0} files)\n" +
        $"{summary.ArchiveBytes:N0} archive bytes · {summary.PayloadBytes:N0} restored payload bytes\n" +
        "Unencrypted sensitive data. AI credentials excluded. The archive will be checked again before replacement.";

    public bool TryLeave()
    {
        if (!IsBusy || handingOff) return true;
        Status = "An operation is still running. Cancel it or wait for completion before leaving Settings or closing the application.";
        return false;
    }
    private void NotifyCommands()
    {
        CreateBackupCommand.NotifyCanExecuteChanged(); ExportCommand.NotifyCanExecuteChanged();
        InspectCommand.NotifyCanExecuteChanged(); RestoreCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanCancel));
    }
    private async Task Run(Func<CancellationToken, Task> action)
    {
        if (IsBusy) return;
        using var cts = new CancellationTokenSource(); operation = cts; IsBusy = true; OutputLocation = "";
        try { await action(cts.Token); }
        catch (OperationCanceledException) { Status = "Operation cancelled. No completed output or restore is reported."; }
        catch (Exception) { Status = "The operation could not complete. Check destination access and free space, then try again. No success is reported."; }
        finally { operation = null; IsBusy = false; }
    }
    private Task CreateBackupAsync() => Run(async token =>
    {
        string? target = interaction.ChooseBackupDestination();
        if (target is null) { Status = "Backup selection cancelled; nothing was written."; return; }
        Status = "Creating and verifying backup… Cancellation waits for safe snapshot/file boundaries.";
        var result = await backup.CreateAsync(target, token);
        if (result.Succeeded && !result.CleanupFailed) { Status = "Backup complete and verified. Keep this unencrypted archive secure."; OutputLocation = target; }
        else Status = result.Failure == BackupValidationCode.Cancelled ? "Backup cancelled; no completed archive is reported." :
            $"Backup did not complete ({result.Failure}, {result.Phase}). Check storage and referenced screenshots; existing destinations are never overwritten.";
        if (result.CleanupFailed) Status += " Cleanup is incomplete; sensitive temporary files may remain.";
    });
    private Task ExportAsync() => Run(async token =>
    {
        string? target = interaction.ChooseExportDestination();
        if (target is null) { Status = "Export selection cancelled; nothing was written."; return; }
        Status = "Exporting a consistent snapshot… Cancellation waits for a safe boundary.";
        var result = await export.CreateAsync(target, token);
        if (result.Succeeded && !result.CleanupFailed) { Status = "Export complete. JSON and four CSVs; screenshot metadata only. Not a restorable backup."; OutputLocation = target; }
        else Status = result.Status == PortableExportStatus.Cancelled ? "Export cancelled; no completed export is reported." :
            $"Export did not complete ({result.Status}). Choose a new destination with available space and write access.";
        if (result.CleanupFailed) Status += " Cleanup is incomplete; sensitive temporary files may remain.";
    });
    private Task InspectAsync() => Run(async token =>
    {
        summary = null; archive = null; OnPropertyChanged(nameof(HasPreview)); OnPropertyChanged(nameof(Preview));
        string? selected = interaction.ChooseRestoreArchive();
        if (selected is null) { Status = "Restore selection cancelled. Installed data is unchanged."; return; }
        Status = "Checking the complete backup. Installed data is not being changed…";
        var result = await preflight.InspectAsync(selected, stagingParent, token);
        Status = token.IsCancellationRequested ? "Backup inspection cancelled; installed data is unchanged." : result.Message;
        if (result.CleanupFailed) Status += " " + result.CleanupMessage;
        if (!token.IsCancellationRequested && result.Validated && !result.CleanupFailed && result.Compatibility == BackupSchemaCompatibility.Exact)
        { archive = selected; summary = result.Summary; }
        OnPropertyChanged(nameof(HasPreview)); OnPropertyChanged(nameof(Preview));
    });
    private void Restore()
    {
        if (IsBusy || summary is null || archive is null) return;
        IsBusy = true;
        try
        {
            if (!dialogs.Confirm(new("Replace installed journal?",
                $"Replace all installed journal data with the checked backup from {summary.CreatedAtUtc:dd MMM yyyy HH:mm} UTC?\n" +
                $"{summary.AccountCount:N0} Accounts · {summary.TradeCount:N0} Trades · {summary.JournalCount:N0} Journals · {summary.AnalysisCount:N0} analyses.\n\n" +
                "The app will close for offline maintenance. A verified recovery copy is retained before replacement. AI keys stay on this device.",
                "Close app and restore", isDestructive: true)))
            { Status = "Restore declined. Installed data is unchanged."; return; }
            handingOff = true; NotifyCommands();
            Status = interaction.BeginRestore(new(archive, summary.ArchiveSha256))
                ? "Handed off to offline maintenance. This is not a successful restore; read the verified result in the maintenance window."
                : "Maintenance could not start or closing was blocked. No restore was performed; keep this application open and try again.";
        }
        catch (Exception) { Status = "Maintenance could not start. No restore success is reported. Check application launch permissions."; }
        finally { handingOff = false; IsBusy = false; }
    }
}
