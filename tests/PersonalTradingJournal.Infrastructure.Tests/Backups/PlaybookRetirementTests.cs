using System.Collections.Immutable;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Infrastructure.Backups;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using Fixture = PersonalTradingJournal.Infrastructure.Tests.Persistence.TradingPlans.TradingPlanPersistenceTests.Fixture;

namespace PersonalTradingJournal.Infrastructure.Tests.Backups;

public sealed class PlaybookRetirementTests
{
    [Fact]
    public async Task UpgradeDetachesDraftLinksAndRetainsTheOriginalDraftAndFrozenReadyRevision()
    {
        await using var f = await Fixture.Create("20261114204255_SimplifyTradeWorkspace");
        var plan = await f.Ready(await f.CreatePlan());
        var ready = plan.LatestReadyRevision!;
        string frozen = PersonalTradingJournal.Infrastructure.TradingPlans.TradingPlanJson.WriteReady(ready);
        plan = await f.Success(PersonalTradingJournal.Application.TradingPlans.TradingPlanWriteOperation.StartDraft, plan, source: ready.Id);
        await using var db = f.Context();
        var row = await db.TradingPlans.SingleAsync();
        var draft = System.Text.Json.Nodes.JsonNode.Parse(row.DraftJson!)!;
        var scenario = draft["Content"]!["Scenarios"]![0]!;
        scenario["Setup"] = JsonSerializer.SerializeToNode(new PersonalTradingJournal.Domain.TradingPlans.TradingPlanSetupLink(f.Setup.Id, 1, f.Setup.UpdatedAtUtc));
        string linkedDraft = draft.ToJsonString();
        var tokens = System.Text.Json.Nodes.JsonNode.Parse(row.ReferenceStateJson!)!;
        tokens[$"3:{f.Setup.Id:D}"] = new string('0', 64);
        row.DraftJson = linkedDraft;
        row.ReferenceStateJson = tokens.ToJsonString();
        await db.SaveChangesAsync();
        await db.Database.MigrateAsync();
        var migrated = (await f.Store.LoadAsync(f.Key))!;
        Assert.Null(Assert.Single(migrated.WorkingDraft!.Content.Scenarios).Setup);
        Assert.Equal(plan.WorkingDraft!.Content.Focus, migrated.WorkingDraft.Content.Focus);
        Assert.Equal(plan.WorkingDraft.Content.Scenarios[0].Condition, migrated.WorkingDraft.Content.Scenarios[0].Condition);
        Assert.Equal(frozen, PersonalTradingJournal.Infrastructure.TradingPlans.TradingPlanJson.WriteReady(migrated.LatestReadyRevision!));
        var archived = await db.Database.SqlQueryRaw<string>("SELECT ContentJson AS Value FROM ArchivedPlaybookContent WHERE Source = 'TradingPlans'").SingleAsync();
        using var original = JsonDocument.Parse(archived);
        Assert.Equal(linkedDraft, original.RootElement.GetProperty("DraftJson").GetString());
    }

    [Fact]
    public async Task RestorePreviousBackupThenUpgradePreservesGuidanceAndSharedImageInExportAndBackup()
    {
        await using var old = await Fixture.Create("20261114204255_SimplifyTradeWorkspace");
        byte[] bytes = [1, 4, 9, 16];
        var image = await old.Get<IPlaybookImageStorage>().StoreAsync("legacy.png", bytes);
        var example = new PlaybookExample(Guid.NewGuid(), "Original annotation", kind: PlaybookExampleKind.Valid, image: image);
        var revision = TradingSetupPlaybookRevision.Create(old.Setup, new("Context", "Entry", "Confirm", "Invalidate", "Execute", [example]), old.Now);
        string definition = PlaybookPersistenceMapper.Serialize(revision);
        ImmutableArray<string> migrations;
        await using (var db = old.Context())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO CurrentPlaybooks (TradingSetupId, Version, DefinitionJson) VALUES ({old.Setup.Id}, 1, {definition});
                INSERT INTO CurrentPlaybookExamples (TradingSetupId, ExampleId, ImageStorageKey, ImageSizeBytes, ImageSha256)
                    VALUES ({old.Setup.Id}, {example.Id}, {image.StorageKey}, {image.SizeBytes}, {image.Sha256});
                """);
            // A Trade owns the same original bytes as the direct example.
            var trade = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO Trades(Id,TradingAccountId,InstrumentId,PricingPointValue,PricingCurrency,CreatedAtUtc,UpdatedAtUtc)
                    VALUES ({trade},{old.Account.Id},{old.Instrument.Id},1,'USD',{old.Now},{old.Now});
                INSERT INTO TradeScreenshots(Id,TradeId,Type,StorageKey,FileName,CreatedAtUtc,UpdatedAtUtc)
                    VALUES ({Guid.NewGuid()},{trade},1,{image.StorageKey},'shared.png',{old.Now},{old.Now});
                """);
            migrations = [.. await db.Database.GetAppliedMigrationsAsync()];
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=DELETE");
        }
        await old.Stop();
        string archive = Path.Combine(old.Root, "original.ptjbackup");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            BackupFileEntry Payload(string name, BackupFileKind kind, byte[] content)
            {
                var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
                entry.ExternalAttributes = 0;
                using var stream = entry.Open(); stream.Write(content);
                return new(name, kind, content.Length, Convert.ToHexStringLower(SHA256.HashData(content)));
            }
            var database = Payload(BackupArchiveContract.DatabasePath, BackupFileKind.Database, await File.ReadAllBytesAsync(old.Paths.DatabasePath));
            var file = Payload(BackupArchiveContract.ScreenshotsPrefix + image.StorageKey, BackupFileKind.Screenshot, bytes);
            var manifest = new BackupManifest(BackupArchiveContract.Format, 1, "1.0.0", old.Now, new("sqlite", migrations),
                BackupArchiveContract.HashAlgorithm, [database, file]);
            var entry = zip.CreateEntry(BackupArchiveContract.ManifestPath); entry.ExternalAttributes = 0;
            using var stream = entry.Open(); JsonSerializer.Serialize(stream, manifest, BackupArchiveContract.JsonOptions);
        }

        await using var target = await Fixture.Create();
        var check = await new RestorePreflightService().InspectAsync(archive, target.Root);
        Assert.True(check.Validated, check.ToString());
        Assert.Equal(BackupSchemaCompatibility.VerifiedPreRetirement, check.Compatibility);
        await target.Stop();
        var restore = await new JournalRestoreService(target.Paths).RestoreAsync(new(archive, check.Summary!.ArchiveSha256));
        Assert.Equal(JournalRestoreStatus.Restored, restore.Status);
        await target.Restart();
        await using (var upgraded = target.Context())
        {
            await upgraded.Database.MigrateAsync();
            Assert.Equal(old.Setup.Id, (await upgraded.TradingSetups.SingleAsync()).Id);
            Assert.Equal(image.StorageKey, (await upgraded.ArchivedPlaybookFiles.SingleAsync()).StorageKey);
            var screenshot = await upgraded.TradeScreenshots.SingleAsync();
            Assert.Equal(image.StorageKey, screenshot.StorageKey);
            upgraded.TradeScreenshots.Remove(screenshot);
            await upgraded.SaveChangesAsync();
            await Assert.ThrowsAsync<SqliteException>(() => upgraded.Database.ExecuteSqlRawAsync("DELETE FROM ArchivedPlaybookContent"));
        }
        await target.Get<IPlaybookImageStorage>().DeleteUnreferencedAsync(image.StorageKey);
        Assert.Equal(bytes, await target.Get<IPlaybookImageStorage>().ReadAsync(image));
        string export = Path.Combine(target.Root, "portable");
        Assert.True((await target.Get<IPortableExportService>().CreateAsync(export)).Succeeded);
        using var journal = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(export, "journal.json")));
        var tables = journal.RootElement.GetProperty("tables");
        var archived = Assert.Single(tables.GetProperty("archivedPlaybookContent").EnumerateArray(),
            r => r.GetProperty("source").GetString() == "CurrentPlaybooks");
        using var preserved = JsonDocument.Parse(archived.GetProperty("contentJson").GetString()!);
        Assert.Equal(definition, preserved.RootElement.GetProperty("DefinitionJson").GetString());
        Assert.False(tables.TryGetProperty("currentPlaybooks", out _));
        string backup = Path.Combine(target.Root, "upgraded.ptjbackup");
        Assert.True((await target.Get<IBackupArchiveService>().CreateAsync(backup)).Succeeded);
        using var saved = ZipFile.OpenRead(backup);
        using var archivedFile = saved.GetEntry(BackupArchiveContract.ScreenshotsPrefix + image.StorageKey)!.Open();
        using var output = new MemoryStream(); await archivedFile.CopyToAsync(output);
        Assert.Equal(bytes, output.ToArray());
    }
}
