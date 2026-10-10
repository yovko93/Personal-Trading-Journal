using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Desktop.ViewModels.Settings;
using PersonalTradingJournal.Infrastructure.Backups;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Initialization;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using PersonalTradingJournal.Infrastructure.Storage;
using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Desktop.Tests.Settings;

public sealed class DataBackupsFlowTests
{
    [Fact]
    public async Task IsolatedSettingsWorkflowUsesRealSnapshotPreflightAndExclusiveRecoveryThenFreshRead()
    {
        string root = Path.Combine(Path.GetTempPath(), "ptj-ui-" + Guid.NewGuid().ToString("N"));
        var paths = new LocalApplicationPaths(root); paths.EnsureDirectoriesExist();
        using var services = new ServiceCollection().AddPersistence(paths).BuildServiceProvider();
        string archive = Path.Combine(root, "synthetic.ptjbackup");
        var f = new BackupUiFixture { Target = archive }; f.Dialogs.ConfirmationResult = true;
        try
        {
            await services.GetRequiredService<JournalDatabaseInitializer>().InitializeAsync();
            var factory = services.GetRequiredService<IDbContextFactory<JournalDbContext>>();
            Guid id = Guid.NewGuid();
            await using (var db = await factory.CreateDbContextAsync())
            {
                db.TradingAccounts.Add(new TradingAccountRecord { Id = id, Name = "Synthetic saved Account", Currency = "USD",
                    AccountType = TradingAccountType.Personal, IsActive = true, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow });
                await db.SaveChangesAsync();
            }
            var vm = new DataBackupsViewModel(services.GetRequiredService<IBackupArchiveService>(), services.GetRequiredService<IPortableExportService>(),
                services.GetRequiredService<IRestorePreflightService>(), f, f.Dialogs, root);
            await vm.CreateBackupCommand.ExecuteAsync(null); Assert.Equal(archive, vm.OutputLocation); Assert.True(File.Exists(archive));
            f.Target = Path.Combine(root, "export"); await vm.ExportCommand.ExecuteAsync(null);
            Assert.Equal(6, Directory.GetFiles(f.Target).Length); Assert.Contains("Not a restorable backup", vm.Status);
            f.Target = archive; await vm.InspectCommand.ExecuteAsync(null); Assert.True(vm.HasPreview); vm.RestoreCommand.Execute(null);
            var request = Assert.IsType<JournalRestoreRequest>(f.Request);
            await using (var db = await factory.CreateDbContextAsync())
            { (await db.TradingAccounts.SingleAsync()).Name = "Later change"; await db.SaveChangesAsync(); }
            var restore = new JournalRestoreService(paths);
            Assert.Equal(JournalRestoreStatus.Blocked, (await restore.RestoreAsync(request)).Status);
            services.Dispose(); // Mirrors process teardown: every normal session/context is gone first.
            var maintenance = new MaintenanceViewModel(restore, _ => Task.CompletedTask, () => { });
            await maintenance.RunAsync(request);
            Assert.StartsWith("Restore verified.", maintenance.Status); Assert.True(maintenance.MayReopen);
            Assert.True(File.Exists(maintenance.RecoveryLocation)); Assert.True(Directory.Exists(maintenance.RetainedLocation));
            await using var fresh = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = paths.DatabasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            await fresh.OpenAsync(); using var query = fresh.CreateCommand(); query.CommandText = "SELECT Name FROM TradingAccounts";
            Assert.Equal("Synthetic saved Account", await query.ExecuteScalarAsync());
        }
        finally
        {
            services.Dispose();
            using (var pool = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = paths.DatabasePath, ForeignKeys = true }.ToString())) SqliteConnection.ClearPool(pool);
            Directory.Delete(root, true);
        }
    }
}
