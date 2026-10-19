using Microsoft.Data.Sqlite;
using PersonalTradingJournal.Domain.TradingPlans;
using PersonalTradingJournal.Infrastructure.TradingPlans;

namespace PersonalTradingJournal.Infrastructure.Backups;

internal static class TradingPlanAssetInventory
{
    // Read one bounded payload at a time. A matching binary inventory alone cannot prove
    // that a Ready/Draft's metadata agrees with its immutable owner ledger.
    internal static bool Matches(SqliteConnection database, CancellationToken token)
    {
        using var command = database.CreateCommand();
        command.CommandText = """
            SELECT Id, CASE WHEN length(DraftJson) <= 2000000 THEN DraftJson END, 0 FROM TradingPlans WHERE DraftJson IS NOT NULL
            UNION ALL SELECT PlanId, CASE WHEN length(SnapshotJson) <= 2000000 THEN SnapshotJson END, 1 FROM TradingPlanReadyRevisions;
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            token.ThrowIfCancellationRequested();
            if (reader.IsDBNull(1)) return false;
            var owner = reader.GetGuid(0);
            TradingPlanContent content = reader.GetInt32(2) == 0 ? TradingPlanJson.ReadDraft(reader.GetString(1)).Content :
                TradingPlanJson.ReadReady(reader.GetString(1)).Content;
            foreach (var shot in content.Screenshots)
            {
                using var asset = database.CreateCommand();
                asset.CommandText = "SELECT PlanId,InstrumentId,Timeframe,StorageKey,FileName,SizeBytes,Sha256 FROM TradingPlanAssets WHERE Id=$id;";
                asset.Parameters.AddWithValue("$id", shot.Id);
                using var value = asset.ExecuteReader();
                if (!value.Read() || value.GetGuid(0) != owner || value.GetGuid(1) != shot.InstrumentId ||
                    value.GetInt32(2) != (int)shot.Timeframe || value.GetString(3) != shot.Image.StorageKey ||
                    value.GetString(4) != shot.Image.FileName || value.GetInt64(5) != shot.Image.SizeBytes || value.GetString(6) != shot.Image.Sha256)
                    return false;
            }
        }
        return true;
    }
}
