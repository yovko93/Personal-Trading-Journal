using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Backups;

namespace PersonalTradingJournal.Desktop.ViewModels.Settings;

public sealed class MaintenanceViewModel : ObservableObject
{
    private readonly IJournalRestoreService service;
    private readonly Func<CancellationToken, Task> waitForParent;
    private readonly Action reopen;
    private CancellationTokenSource? operation;
    private bool busy, mayReopen;
    private string status = "Normal journal use is blocked. Close other PTJ instances. Recover interrupted restore to verify a safe dataset before reopening. Never remove recovery files or the startup marker manually.";
    private string recovery = "", retained = "";
    public MaintenanceViewModel(IJournalRestoreService service, Func<CancellationToken, Task> waitForParent, Action reopen)
    {
        this.service = service; this.waitForParent = waitForParent; this.reopen = reopen;
        RecoverCommand = new AsyncRelayCommand(() => RunAsync(null), () => !IsBusy);
        CancelCommand = new RelayCommand(() => { operation?.Cancel(); Status = "Cancellation requested. After replacement begins, verification/recovery must finish before closing."; }, () => IsBusy);
        ReopenCommand = new RelayCommand(() => { try { reopen(); } catch (Exception) { Status = "Application could not reopen. Recovery files remain retained. Close maintenance and start PTJ again."; } }, () => !IsBusy && MayReopen);
    }
    public IAsyncRelayCommand RecoverCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand ReopenCommand { get; }
    public bool IsBusy { get => busy; private set { SetProperty(ref busy, value); Notify(); } }
    public bool MayReopen { get => mayReopen; private set { SetProperty(ref mayReopen, value); Notify(); } }
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public string RecoveryLocation { get => recovery; private set => SetProperty(ref recovery, value); }
    public string RetainedLocation { get => retained; private set => SetProperty(ref retained, value); }
    public bool TryClose()
    {
        if (!IsBusy) return true;
        Status = "Wait for the operation to reach a safe outcome. Use Cancel; do not terminate the application during replacement or recovery.";
        return false;
    }
    private void Notify() { RecoverCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged(); ReopenCommand.NotifyCanExecuteChanged(); }
    public async Task RunAsync(JournalRestoreRequest? request)
    {
        if (IsBusy) return;
        using var cts = new CancellationTokenSource(); operation = cts; IsBusy = true; MayReopen = false;
        try
        {
            Status = "Waiting for the previous application process to exit. Normal journal services are not running here…";
            await waitForParent(cts.Token);
            cts.Token.ThrowIfCancellationRequested();
            Status = request is null ? "Recovering and verifying retained data…" : "Offline restore: revalidating backup, retaining verified recovery, replacing and verifying data…";
            var result = request is null ? await service.RecoverInterruptedAsync(cts.Token) : await service.RestoreAsync(request, cts.Token);
            Status = result.Message;
            if (result.CleanupFailed) Status += " Cleanup is incomplete. Preserve retained files; sensitive temporary data may remain.";
            RecoveryLocation = result.RecoveryArchivePath ?? ""; RetainedLocation = result.RetainedDirectoryPath ?? "";
            MayReopen = result.Status is JournalRestoreStatus.Restored or JournalRestoreStatus.RecoveredOriginal or JournalRestoreStatus.NothingToRecover
                or JournalRestoreStatus.FailedOriginalIntact or JournalRestoreStatus.Cancelled or JournalRestoreStatus.RejectedArchive
                or JournalRestoreStatus.SourceChanged or JournalRestoreStatus.RecoveryCaptureFailed or JournalRestoreStatus.InsufficientStorage;
        }
        catch (OperationCanceledException) { Status = "Maintenance cancelled without a verified result. No restore success is reported. Recover/check the dataset before reopening."; }
        catch (TimeoutException) { Status = "The previous process has not exited. No replacement was attempted. Close it, then use Recover interrupted restore to check the dataset."; }
        catch (Exception) { Status = "Maintenance did not reach a verified result. Normal use remains blocked here. Preserve recovery files, check free space and permissions, then use Recover interrupted restore."; }
        finally { operation = null; IsBusy = false; }
    }
}
