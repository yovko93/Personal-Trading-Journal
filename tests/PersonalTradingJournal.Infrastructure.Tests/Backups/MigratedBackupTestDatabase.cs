using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Initialization;
using PersonalTradingJournal.Infrastructure.Storage;
using PersonalTradingJournal.Infrastructure.Tests.Persistence;

namespace PersonalTradingJournal.Infrastructure.Tests.Backups;

// Backup/export/restore cases need independent current-schema databases, not a
// replay of every historical migration in every fixture. Dedicated migration
// fixtures still migrate from scratch. Only immutable, empty database bytes are
// shared: no connection, provider, filesystem root or mutable records are shared.
internal static class MigratedBackupTestDatabase
{
    private static readonly Lazy<Task<byte[]>> Template = new(BuildAsync);

    internal static async Task CreateAsync(string destination)
    {
        byte[] bytes = await Template.Value;
        using var timing = SyntheticFixtureTiming.Start("backup template clone");
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await output.WriteAsync(bytes);
    }

    private static async Task<byte[]> BuildAsync()
    {
        using var timing = SyntheticFixtureTiming.Start("backup template migration and snapshot");
        string root = Path.Combine(Path.GetTempPath(), "ptj-backup-template-" + Guid.NewGuid().ToString("N"));
        var paths = new LocalApplicationPaths(root);
        paths.EnsureDirectoriesExist();
        try
        {
            await using var services = new ServiceCollection().AddPersistence(paths).BuildServiceProvider();
            await services.GetRequiredService<JournalDatabaseInitializer>().InitializeAsync();
            await using var db = services.GetRequiredService<IDbContextFactory<JournalDbContext>>().CreateDbContext();
            await db.Database.OpenConnectionAsync();
            string snapshot = Path.Combine(root, "empty-snapshot.db");
            // SQLite backup includes committed WAL content. Never copy a live
            // database file and never retain the template's database connections.
            await using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = snapshot, Pooling = false }.ToString()))
            {
                await destination.OpenAsync();
                ((SqliteConnection)db.Database.GetDbConnection()).BackupDatabase(destination);
                using var command = destination.CreateCommand();
                command.CommandText = "PRAGMA journal_mode=DELETE;";
                await command.ExecuteScalarAsync();
            }
            return await File.ReadAllBytesAsync(snapshot);
        }
        finally
        {
            SqliteTestPoolCleanup.ClearPersistencePools(paths.DatabasePath);
            Directory.Delete(root, recursive: true);
        }
    }
}
