using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Desktop.DataManagement;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Settings;

namespace PersonalTradingJournal.Desktop.Tests.Settings;

public sealed class DataBackupsTests
{
    [Fact]
    public async Task BackupAndExportAreExplicitAndReportOnlyCompleteOutput()
    {
        var f = new BackupUiFixture(); var vm = f.ViewModel();
        Assert.Equal(0, f.Calls); Assert.False(vm.HasPreview);
        await vm.CreateBackupCommand.ExecuteAsync(null);
        Assert.Equal(1, f.Backups); Assert.Contains("verified", vm.Status); Assert.Equal(f.Target, vm.OutputLocation);
        await vm.ExportCommand.ExecuteAsync(null);
        Assert.Equal(1, f.Exports); Assert.Contains("Not a restorable backup", vm.Status); Assert.Equal(f.Target, vm.OutputLocation);
        Assert.Contains("unencrypted", DataBackupsViewModel.BackupDisclosure);
        Assert.Contains("excludes AI API keys", DataBackupsViewModel.BackupDisclosure);
        Assert.Contains("four CSVs", DataBackupsViewModel.ExportDisclosure);
        Assert.Contains("metadata only", DataBackupsViewModel.ExportDisclosure);
    }
    [Theory]
    [InlineData(BackupValidationCode.IoFailure)]
    [InlineData(BackupValidationCode.Cancelled)]
    [InlineData(BackupValidationCode.SourceChanged)]
    [InlineData(BackupValidationCode.DatabaseAttachmentMismatch)]
    public async Task FailedBackupNeverClaimsPublication(BackupValidationCode failure)
    {
        var f = new BackupUiFixture { BackupResult = new(Guid.NewGuid(), BackupArchivePhase.Capture, failure, CleanupFailed: true) };
        var vm = f.ViewModel(); await vm.CreateBackupCommand.ExecuteAsync(null);
        Assert.Empty(vm.OutputLocation); Assert.DoesNotContain("complete and verified", vm.Status); Assert.Contains("Cleanup is incomplete", vm.Status);
    }
    [Theory]
    [InlineData(PortableExportStatus.Cancelled)]
    [InlineData(PortableExportStatus.SnapshotFailed)]
    [InlineData(PortableExportStatus.LimitExceeded)]
    [InlineData(PortableExportStatus.IoFailure)]
    public async Task FailedExportNeverClaimsPublication(PortableExportStatus failure)
    {
        var f = new BackupUiFixture { ExportResult = new(Guid.NewGuid(), failure, PortableExportPhase.Write) };
        var vm = f.ViewModel(); await vm.ExportCommand.ExecuteAsync(null);
        Assert.Empty(vm.OutputLocation); Assert.DoesNotContain("Export complete.", vm.Status);
    }
    [Fact]
    public async Task CancelledPickersWriteNothingAndClearOldPreview()
    {
        var f = new BackupUiFixture(); var vm = f.ViewModel(); await vm.InspectCommand.ExecuteAsync(null); Assert.True(vm.HasPreview);
        f.Target = null; await vm.InspectCommand.ExecuteAsync(null); await vm.CreateBackupCommand.ExecuteAsync(null); await vm.ExportCommand.ExecuteAsync(null);
        Assert.False(vm.HasPreview); Assert.False(vm.RestoreCommand.CanExecute(null)); Assert.Equal(0, f.Backups + f.Exports + f.Handoffs);
    }
    [Theory]
    [InlineData(BackupValidationCode.ContentMismatch)]
    [InlineData(BackupValidationCode.UnsupportedDatabaseSchema)]
    [InlineData(BackupValidationCode.Cancelled)]
    public async Task RejectedPreflightCannotAuthorizeReplacement(BackupValidationCode failure)
    {
        var f = new BackupUiFixture { PreflightResult = new(Guid.NewGuid(), RestorePreflightPhase.Database, failure) };
        var vm = f.ViewModel(); await vm.InspectCommand.ExecuteAsync(null); vm.RestoreCommand.Execute(null);
        Assert.False(vm.HasPreview); Assert.Equal(0, f.Handoffs); Assert.Null(f.Dialogs.ConfirmationRequest);
    }
    [Fact]
    public async Task PreflightCleanupFailureAndOlderSchemaCannotRestore()
    {
        var f = new BackupUiFixture(); f.PreflightResult = f.PreflightResult with { CleanupFailed = true };
        var vm = f.ViewModel(); await vm.InspectCommand.ExecuteAsync(null); Assert.False(vm.HasPreview); Assert.Contains("cleanup", vm.Status);
        f.PreflightResult = f.PreflightResult with { CleanupFailed = false, Compatibility = BackupSchemaCompatibility.RequiresStagedMigration };
        await vm.InspectCommand.ExecuteAsync(null); Assert.False(vm.RestoreCommand.CanExecute(null));
    }
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ConfirmationIsDestructiveAndHandoffIsNotRestoreSuccess(bool accept, bool launch)
    {
        var f = new BackupUiFixture { LaunchAccepted = launch }; f.Dialogs.ConfirmationResult = accept;
        var vm = f.ViewModel(); await vm.InspectCommand.ExecuteAsync(null);
        Assert.Contains("2 Accounts", vm.Preview); Assert.Contains("3 Trades", vm.Preview); Assert.Contains("UTC", vm.Preview);
        Assert.Contains("AI credentials excluded", vm.Preview);
        f.OnHandoff = () => Assert.True(vm.TryLeave());
        vm.RestoreCommand.Execute(null);
        Assert.True(f.Dialogs.ConfirmationRequest!.IsDestructive);
        Assert.Equal(accept ? 1 : 0, f.Handoffs);
        if (accept) { Assert.Equal(f.Target, f.Request!.ArchivePath); Assert.Equal(f.PreflightResult.Summary!.ArchiveSha256, f.Request.ConfirmedArchiveSha256); }
        Assert.DoesNotContain("Restore verified", vm.Status); Assert.False(vm.IsBusy);
    }
    [Fact]
    public async Task BusyPreventsDuplicateAndOtherActionsAndNavigationUntilCancellationFinishes()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var f = new BackupUiFixture { BackupWork = async token => { entered.SetResult(); await Task.Delay(Timeout.Infinite, token); return null!; } };
        var vm = f.ViewModel(); Task task = vm.CreateBackupCommand.ExecuteAsync(null); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(vm.IsBusy); Assert.True(vm.CanCancel); Assert.False(vm.TryLeave());
        await vm.CreateBackupCommand.ExecuteAsync(null); await vm.ExportCommand.ExecuteAsync(null); await vm.InspectCommand.ExecuteAsync(null);
        Assert.Equal(1, f.Calls); Assert.False(vm.ExportCommand.CanExecute(null));
        vm.CancelCommand.Execute(null); await task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(vm.IsBusy); Assert.Empty(vm.OutputLocation); Assert.True(vm.TryLeave());
    }
    [Fact]
    public async Task CancelledLatePreflightCannotPublishPreviewAndExceptionsAreSanitized()
    {
        var pending = new TaskCompletionSource<RestorePreflightResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var f = new BackupUiFixture { InspectWork = _ => pending.Task }; var vm = f.ViewModel();
        Task task = vm.InspectCommand.ExecuteAsync(null); vm.CancelCommand.Execute(null); pending.SetResult(f.PreflightResult); await task;
        Assert.False(vm.HasPreview); Assert.Contains("cancelled", vm.Status);
        f.BackupWork = _ => throw new IOException("sensitive local path and row content");
        await vm.CreateBackupCommand.ExecuteAsync(null); Assert.DoesNotContain("sensitive", vm.Status); Assert.False(vm.IsBusy);
    }
    [Theory]
    [InlineData(JournalRestoreStatus.Restored, true)]
    [InlineData(JournalRestoreStatus.RecoveredOriginal, true)]
    [InlineData(JournalRestoreStatus.NothingToRecover, true)]
    [InlineData(JournalRestoreStatus.RecoveryRequired, false)]
    [InlineData(JournalRestoreStatus.Blocked, false)]
    [InlineData(JournalRestoreStatus.RecoveryCaptureFailed, true)]
    [InlineData(JournalRestoreStatus.Cancelled, true)]
    public async Task MaintenanceReportsVerifiedOutcomeAndRetainedLocations(JournalRestoreStatus status, bool mayReopen)
    {
        var f = new BackupUiFixture { RestoreResult = new(status, Guid.NewGuid(), JournalRestorePhase.Finished, "recovery.ptjbackup", "retained") };
        bool waited = false, reopened = false;
        var vm = new MaintenanceViewModel(f, _ => { waited = true; return Task.CompletedTask; }, () => reopened = true);
        Assert.False(vm.MayReopen); await vm.RunAsync(new("backup", new string('a', 64)));
        Assert.True(waited); Assert.Equal(1, f.Restores); Assert.Equal(mayReopen, vm.ReopenCommand.CanExecute(null));
        Assert.Equal("recovery.ptjbackup", vm.RecoveryLocation); Assert.Equal("retained", vm.RetainedLocation);
        Assert.Equal(status == JournalRestoreStatus.Restored, vm.Status.StartsWith("Restore verified."));
        if (mayReopen) { vm.ReopenCommand.Execute(null); Assert.True(reopened); }
    }
    [Fact]
    public async Task MaintenanceWaitCancellationNeverInvokesRestoreAndCloseIsBlockedDuringWork()
    {
        var f = new BackupUiFixture(); var vm = new MaintenanceViewModel(f, token => Task.Delay(Timeout.Infinite, token), () => { });
        Task run = vm.RunAsync(new("archive", new string('a', 64))); Assert.False(vm.TryClose());
        await vm.RunAsync(null); Assert.Equal(0, f.Restores + f.Recoveries);
        vm.CancelCommand.Execute(null); await run; Assert.False(vm.MayReopen); Assert.True(vm.TryClose());
        Assert.Equal(0, f.Restores + f.Recoveries);
    }
    [Fact]
    public async Task RecoveryBlockedStartupRequiresExplicitRecoveryAndFailureKeepsNormalUseBlocked()
    {
        var f = new BackupUiFixture { RestoreWork = _ => throw new IOException("private contents") };
        var vm = new MaintenanceViewModel(f, _ => Task.CompletedTask, () => { });
        Assert.Equal(0, f.Calls); Assert.Contains("Normal journal use is blocked", vm.Status);
        await vm.RecoverCommand.ExecuteAsync(null); Assert.Equal(1, f.Recoveries); Assert.False(vm.MayReopen);
        Assert.DoesNotContain("private contents", vm.Status);
    }
    [Fact]
    public async Task ParentTimeoutAndCancellationDuringRestoreDoNotReportSuccess()
    {
        var f = new BackupUiFixture();
        var timeout = new MaintenanceViewModel(f, _ => throw new TimeoutException(), () => { });
        await timeout.RunAsync(new("archive", new string('a', 64)));
        Assert.Equal(0, f.Calls); Assert.False(timeout.MayReopen); Assert.Contains("not exited", timeout.Status);
        f.RestoreWork = async token => { await Task.Delay(Timeout.Infinite, token); return null!; };
        var cancel = new MaintenanceViewModel(f, _ => Task.CompletedTask, () => { });
        Task pending = cancel.RunAsync(new("archive", new string('a', 64)));
        cancel.CancelCommand.Execute(null); await pending;
        Assert.Equal(1, f.Restores); Assert.False(cancel.MayReopen); Assert.DoesNotContain("Restore verified", cancel.Status);
    }
    [Fact]
    public void HandoffKeepsIsolationAndFingerprintAndContainsNoSecretOrRequestBody()
    {
        string[] original = ["--isolated-data-root", Path.GetTempPath()];
        var request = new JournalRestoreRequest(Path.Combine(Path.GetTempPath(), "synthetic.ptjbackup"), new string('b', 64));
        var launch = MaintenanceLaunch.Parse(MaintenanceLaunch.RestoreArguments(original, request, 123));
        Assert.Equal(original, launch.NormalArguments); Assert.Equal(request, launch.Restore); Assert.Equal(123, launch.ParentId);
        Assert.DoesNotContain(request.ArchivePath, launch.ToString()); Assert.DoesNotContain(request.ArchivePath, request.ToString());
        Assert.Throws<ArgumentException>(() => MaintenanceLaunch.Parse(["--ptj-maintenance-restore", "123"]));
    }
}

internal sealed class BackupUiFixture : IBackupArchiveService, IPortableExportService, IRestorePreflightService, IDataBackupInteraction, IJournalRestoreService
{
    public string? Target = Path.Combine(Path.GetTempPath(), "synthetic-only.ptjbackup");
    public int Backups, Exports, Inspections, Handoffs, Restores, Recoveries;
    public int Calls => Backups + Exports + Inspections + Handoffs + Restores + Recoveries;
    public FakeDialogService Dialogs = new();
    public bool LaunchAccepted = true;
    public Action? OnHandoff;
    public JournalRestoreRequest? Request;
    public Func<CancellationToken, Task<BackupArchiveResult>>? BackupWork;
    public Func<CancellationToken, Task<RestorePreflightResult>>? InspectWork;
    public Func<CancellationToken, Task<JournalRestoreResult>>? RestoreWork;
    public BackupArchiveResult BackupResult = new(Guid.NewGuid(), BackupArchivePhase.Publish);
    public PortableExportResult ExportResult = new(Guid.NewGuid(), PortableExportStatus.Success, PortableExportPhase.Publish);
    public RestorePreflightResult PreflightResult = new(Guid.NewGuid(), RestorePreflightPhase.Summary, null, BackupSchemaCompatibility.Exact,
        new(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero), 1, "current-schema", 1, 2, 3, 4, 5, 6, 7, 7, 1000, 500,
            new string('a', 64), [RestorePreflightWarning.UnencryptedSensitiveData, RestorePreflightWarning.AiCredentialsExcluded, RestorePreflightWarning.RevalidationRequired]));
    public JournalRestoreResult RestoreResult = new(JournalRestoreStatus.Restored, Guid.NewGuid(), JournalRestorePhase.Finished);
    public DataBackupsViewModel ViewModel() => new(this, this, this, this, Dialogs, Path.GetTempPath());
    Task<BackupArchiveResult> IBackupArchiveService.CreateAsync(string target, CancellationToken token)
    { Backups++; return BackupWork?.Invoke(token) ?? Task.FromResult(BackupResult); }
    Task<PortableExportResult> IPortableExportService.CreateAsync(string target, CancellationToken token)
    { Exports++; return Task.FromResult(ExportResult); }
    public Task<RestorePreflightResult> InspectAsync(string target, string staging, CancellationToken token)
    { Inspections++; return InspectWork?.Invoke(token) ?? Task.FromResult(PreflightResult); }
    public string? ChooseBackupDestination() => Target;
    public string? ChooseExportDestination() => Target;
    public string? ChooseRestoreArchive() => Target;
    public bool BeginRestore(JournalRestoreRequest request) { Handoffs++; Request = request; OnHandoff?.Invoke(); return LaunchAccepted; }
    public Task<JournalRestoreResult> RestoreAsync(JournalRestoreRequest request, CancellationToken token)
    { Restores++; return RestoreWork?.Invoke(token) ?? Task.FromResult(RestoreResult); }
    public Task<JournalRestoreResult> RecoverInterruptedAsync(CancellationToken token)
    { Recoveries++; return RestoreWork?.Invoke(token) ?? Task.FromResult(RestoreResult); }
}
