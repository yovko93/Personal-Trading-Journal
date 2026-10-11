using Microsoft.Data.Sqlite;
using PersonalTradingJournal.Application.Backups;

namespace PersonalTradingJournal.Infrastructure.Backups;

internal static class PlaybookImageInventory
{
    internal const string KeysSql = "SELECT StorageKey COLLATE BINARY AS StorageKey FROM TradeScreenshots UNION SELECT ImageStorageKey COLLATE BINARY FROM PlaybookExamples WHERE ImageStorageKey IS NOT NULL ORDER BY StorageKey COLLATE BINARY LIMIT 100001;";

    internal static bool Matches(SqliteConnection database, IEnumerable<BackupFileEntry> files, CancellationToken token)
    {
        var entries = files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        using var command = database.CreateCommand();
        command.CommandText = "SELECT ImageStorageKey, ImageSizeBytes, ImageSha256 FROM PlaybookExamples WHERE ImageStorageKey IS NOT NULL;";
        using var rows = command.ExecuteReader();
        while (rows.Read())
        {
            token.ThrowIfCancellationRequested();
            if (rows.IsDBNull(1) || rows.IsDBNull(2) ||
                !entries.TryGetValue(BackupArchiveContract.ScreenshotsPrefix + rows.GetString(0), out var file) ||
                file.SizeBytes != rows.GetInt64(1) || !string.Equals(file.Sha256, rows.GetString(2), StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }
}
