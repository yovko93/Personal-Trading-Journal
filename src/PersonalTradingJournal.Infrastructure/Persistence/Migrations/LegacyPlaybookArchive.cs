using Microsoft.EntityFrameworkCore.Migrations;

namespace PersonalTradingJournal.Infrastructure.Persistence.Migrations;

/// <summary>Snapshot the retired schema inside the upgrade transaction, before dropping any foreign keys.</summary>
internal static class LegacyPlaybookArchive
{
    internal static void Capture(MigrationBuilder migration)
    {
        migration.Sql("""
            CREATE TABLE ArchivedPlaybookContent (
                Id INTEGER PRIMARY KEY, Source TEXT NOT NULL, ContentJson TEXT NOT NULL CHECK(json_valid(ContentJson)));
            CREATE TABLE ArchivedPlaybookFiles (
                StorageKey TEXT PRIMARY KEY NOT NULL, SizeBytes INTEGER, Sha256 TEXT,
                CHECK ((SizeBytes IS NULL) = (Sha256 IS NULL)));
            """);
        Copy("PlaybookRevisions", "TradingSetupId Revision ContractVersion SetupName SetupDescription MarketContext EntryConditions Confirmations Invalidation ExecutionGuidance RecordedAtUtc");
        Copy("PlaybookExamples", "TradingSetupId Revision ExampleId Position Annotation SourceTradeId SourceScreenshotId Kind ImageStorageKey ImageFileName ImageSizeBytes ImageSha256");
        Copy("CurrentPlaybooks", "TradingSetupId Version LegacyRevision DefinitionJson");
        Copy("CurrentPlaybookExamples", "TradingSetupId ExampleId SourceScreenshotId ImageStorageKey ImageSizeBytes ImageSha256");
        Copy("TradePlaybookContexts", "TradeId ContractVersion TradingSetupId Revision TradeOpenedAtUtc CapturedAtUtc DefinitionJson");
        Copy("TradePlaybookAssociations", "TradeId Sequence TradingSetupId Revision RecordedAtUtc TradeOpenedAtUtc DefinitionJson");

        // Preserve names for direct definitions, even if the independent Setup is subsequently deleted.
        Copy("TradingSetups", "Id Name Description", "WHERE Id IN (SELECT TradingSetupId FROM CurrentPlaybooks UNION SELECT TradingSetupId FROM PlaybookRevisions)");
        const string linkedScreenshots = "WHERE Id IN (SELECT SourceScreenshotId FROM PlaybookExamples UNION SELECT SourceScreenshotId FROM CurrentPlaybookExamples)";
        Copy("TradeScreenshots", "Id TradeId StorageKey FileName Type CapturedAtUtc Timeframe Description CreatedAtUtc UpdatedAtUtc", linkedScreenshots);

        Copy("TradingPlans", "Id DraftJson ReferenceStateJson", """
            WHERE DraftJson IS NOT NULL AND EXISTS
                (SELECT 1 FROM json_each(DraftJson, '$.Content.Scenarios') WHERE json_type(value, '$.Setup') = 'object')
            """);

        // Fail the upgrade before retiring ownership if the direct definition and its image index disagree.
        migration.Sql("""
            CREATE TEMP TABLE RetirementGuard (Valid INTEGER CONSTRAINT CheckPlaybookImageOwnership CHECK (Valid = 1));
            WITH expected AS (
                SELECT c.TradingSetupId, upper(json_extract(e.value, '$.Id')) AS ExampleId,
                       upper(json_extract(e.value, '$.SourceScreenshotId')) AS SourceScreenshotId,
                       json_extract(e.value, '$.Image.StorageKey') AS StorageKey,
                       json_extract(e.value, '$.Image.SizeBytes') AS SizeBytes,
                       upper(json_extract(e.value, '$.Image.Sha256')) AS Sha256
                FROM CurrentPlaybooks c, json_each(c.DefinitionJson, '$.Examples') e WHERE c.DefinitionJson IS NOT NULL
            ), indexed AS (
                SELECT e.TradingSetupId, upper(e.ExampleId), upper(e.SourceScreenshotId), e.ImageStorageKey,
                       e.ImageSizeBytes, upper(e.ImageSha256)
                FROM CurrentPlaybookExamples e JOIN CurrentPlaybooks c USING (TradingSetupId) WHERE c.DefinitionJson IS NOT NULL
            )
            INSERT INTO RetirementGuard SELECT
                NOT EXISTS (SELECT * FROM expected EXCEPT SELECT * FROM indexed)
                AND NOT EXISTS (SELECT * FROM indexed EXCEPT SELECT * FROM expected);
            DROP TABLE RetirementGuard;
            """);

        // Conflicting metadata for the same uploaded file fails the unique-key constraint and rolls back.
        migration.Sql("""
            INSERT INTO ArchivedPlaybookFiles
                SELECT ImageStorageKey, ImageSizeBytes, upper(ImageSha256) FROM CurrentPlaybookExamples WHERE ImageStorageKey IS NOT NULL
                UNION SELECT ImageStorageKey, ImageSizeBytes, upper(ImageSha256) FROM PlaybookExamples WHERE ImageStorageKey IS NOT NULL;
            """);
        migration.Sql($"""
            INSERT INTO ArchivedPlaybookFiles (StorageKey)
                SELECT DISTINCT StorageKey FROM TradeScreenshots {linkedScreenshots}
                AND StorageKey NOT IN (SELECT StorageKey FROM ArchivedPlaybookFiles);
            """);
        foreach (string table in new[] { "ArchivedPlaybookContent", "ArchivedPlaybookFiles" })
            foreach (string operation in new[] { "INSERT", "UPDATE", "DELETE" })
                migration.Sql($"CREATE TRIGGER {table}_No{operation} BEFORE {operation} ON {table} BEGIN SELECT RAISE(ABORT, 'Historical archive is read-only'); END;");

        void Copy(string table, string columns, string condition = "")
        {
            // All identifiers are constants from the preceding migration. Store SQL nulls and full text verbatim.
            string fields = string.Join(", ", columns.Split(' ').Select(c => $"'{c}', {c}"));
            migration.Sql($"INSERT INTO ArchivedPlaybookContent(Source, ContentJson) SELECT '{table}', json_object({fields}) FROM {table} {condition};");
        }
    }
}
