using Microsoft.Data.Sqlite;
using PersonalTradingJournal.Application.Backups;

namespace PersonalTradingJournal.Infrastructure.Backups;

internal static class PlaybookImageInventory
{
    internal const string KeysSql = "SELECT StorageKey COLLATE BINARY AS StorageKey FROM TradeScreenshots UNION SELECT ImageStorageKey COLLATE BINARY FROM PlaybookExamples WHERE ImageStorageKey IS NOT NULL UNION SELECT ImageStorageKey COLLATE BINARY FROM CurrentPlaybookExamples WHERE ImageStorageKey IS NOT NULL UNION SELECT StorageKey COLLATE BINARY FROM TradingPlanAssets UNION SELECT StorageKey COLLATE BINARY FROM NotebookImages WHERE Ownership = 1 UNION SELECT StorageKey COLLATE BINARY FROM NotebookOwnedImageFiles WHERE CleanupPending = 0 ORDER BY StorageKey COLLATE BINARY LIMIT 100001;";

    internal static bool Matches(SqliteConnection database, IEnumerable<BackupFileEntry> files, CancellationToken token)
    {
        var entries = files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        using var command = database.CreateCommand();
        command.CommandText = "SELECT ImageStorageKey, ImageSizeBytes, ImageSha256 FROM PlaybookExamples WHERE ImageStorageKey IS NOT NULL UNION ALL SELECT ImageStorageKey, ImageSizeBytes, ImageSha256 FROM CurrentPlaybookExamples WHERE ImageStorageKey IS NOT NULL UNION ALL SELECT StorageKey, SizeBytes, Sha256 FROM TradingPlanAssets UNION ALL SELECT StorageKey, SizeBytes, Sha256 FROM NotebookImages WHERE Ownership = 1 UNION ALL SELECT StorageKey, SizeBytes, Sha256 FROM NotebookOwnedImageFiles WHERE CleanupPending = 0;";
        using var rows = command.ExecuteReader();
        while (rows.Read())
        {
            token.ThrowIfCancellationRequested();
            if (rows.IsDBNull(1) || rows.IsDBNull(2) ||
                !entries.TryGetValue(BackupArchiveContract.ScreenshotsPrefix + rows.GetString(0), out var file) ||
                file.SizeBytes != rows.GetInt64(1) || !string.Equals(file.Sha256, rows.GetString(2), StringComparison.OrdinalIgnoreCase)) return false;
        }
        rows.Close();
        return CurrentDefinitionsMatch(database, token) && TradingPlanAssetInventory.Matches(database, token);
    }

    private static bool CurrentDefinitionsMatch(SqliteConnection database, CancellationToken token)
    {
        using var command = database.CreateCommand();
        command.CommandText = "SELECT TradingSetupId, Version, DefinitionJson FROM CurrentPlaybooks WHERE DefinitionJson IS NOT NULL LIMIT 100001;";
        var expected = new Dictionary<(Guid, Guid), (Guid?, string?, long?, string?)>();
        using (var rows = command.ExecuteReader())
        {
            int count = 0;
            while (rows.Read())
            {
                token.ThrowIfCancellationRequested();
                if (++count > 100000 || rows.GetString(2).Length > 1000000) return false;
                var value = Persistence.Mapping.PlaybookPersistenceMapper.Deserialize(rows.GetString(2));
                if (value.TradingSetupId != rows.GetGuid(0) || value.Revision != rows.GetInt32(1)) return false;
                foreach (var e in value.Guidance.Examples)
                {
                    if (expected.Count >= 100000) return false;
                    expected.Add((value.TradingSetupId, e.Id), (e.SourceScreenshotId, e.Image?.StorageKey, e.Image?.SizeBytes, e.Image?.Sha256));
                }
            }
        }
        command.CommandText = "SELECT e.TradingSetupId,e.ExampleId,e.SourceScreenshotId,e.ImageStorageKey,e.ImageSizeBytes,e.ImageSha256 FROM CurrentPlaybookExamples e JOIN CurrentPlaybooks c ON c.TradingSetupId=e.TradingSetupId WHERE c.DefinitionJson IS NOT NULL LIMIT 100001;";
        using var entries = command.ExecuteReader();
        int found = 0;
        while (entries.Read())
        {
            token.ThrowIfCancellationRequested();
            if (++found > 100000 || !expected.Remove((entries.GetGuid(0), entries.GetGuid(1)), out var value) ||
                value != (entries.IsDBNull(2) ? (Guid?)null : entries.GetGuid(2),
                    entries.IsDBNull(3) ? null : entries.GetString(3), entries.IsDBNull(4) ? (long?)null : entries.GetInt64(4),
                    entries.IsDBNull(5) ? null : entries.GetString(5))) return false;
        }
        return expected.Count == 0;
    }
}
