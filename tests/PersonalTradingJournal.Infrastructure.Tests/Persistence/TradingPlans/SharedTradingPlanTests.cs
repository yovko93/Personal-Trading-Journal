using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Domain.TradingPlans;
using PersonalTradingJournal.Infrastructure.Backups;
using PersonalTradingJournal.Infrastructure.TradingPlans;
using Fixture = PersonalTradingJournal.Infrastructure.Tests.Persistence.TradingPlans.TradingPlanPersistenceTests.Fixture;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.TradingPlans;

public sealed class SharedTradingPlanTests
{
    [Fact]
    public async Task LegacyV1ReadyPayloadReconstructsWithoutInventingSharedScopeOrAttachments()
    {
        await using var f = await Fixture.Create(); var p = await f.Ready(await f.CreatePlan());
        var node = JsonNode.Parse(TradingPlanJson.WriteReady(p.LatestReadyRevision!))!;
        node["Version"] = 1; node["Draft"]!["Version"] = 1;
        var legacy = node.ToJsonString(); var loaded = TradingPlanJson.ReadReady(legacy);
        Assert.Equal(p.LatestReadyRevision!.Id, loaded.Id); Assert.Equal(f.Key, loaded.Key);
        Assert.Null(loaded.Content.PlanCurrency); Assert.Empty(loaded.Content.Screenshots);
        Assert.Equal(p.LatestReadyRevision.ReadyAtUtc, loaded.ReadyAtUtc);
        var original = p.LatestReadyRevision.References.Account; var restored = loaded.References.Account;
        Assert.Equal(original.AccountId, restored.AccountId); Assert.Equal(original.Name, restored.Name);
        Assert.Equal(original.Currency, restored.Currency); Assert.Equal(original.UpdatedAtUtc, restored.UpdatedAtUtc);
    }

    private static TradingPlanContent Shared(Fixture f, string currency = "USD", IEnumerable<TradingPlanScreenshot>? images = null) =>
        new(TradingPlanSession.NewYork, "Synthetic shared preparation", instruments: f.Content.Instruments,
            scenarios: [new(Guid.NewGuid(), f.Instrument.Id, direction: TradeDirection.Short)],
            standAside: "Wait when no confirmation is recorded", riskLimits: new(25, 100), planCurrency: currency, screenshots: images);
    private static async Task<TradingPlan> Save(ITradingPlanStore store, TradingPlanKey key, TradingPlanContent content, TradingPlan? prior = null)
    {
        var r = await store.WriteAsync(new(prior is null ? TradingPlanWriteOperation.CreateDraft : TradingPlanWriteOperation.SaveDraft,
            key, content, prior is null ? null : new(prior.Id, prior.Version)));
        Assert.Equal(TradingPlanWriteStatus.Success, r.Status); return r.Plan!;
    }
    private static async Task<TradingPlan> Ready(ITradingPlanStore store, TradingPlan p)
    {
        p = await Save(store, p.Key, p.WorkingDraft!.Content, p);
        var r = await store.WriteAsync(new(TradingPlanWriteOperation.MarkReady, p.Key, p.WorkingDraft!.Content, new(p.Id, p.Version)));
        Assert.Equal(TradingPlanWriteStatus.Success, r.Status); return r.Plan!;
    }

    [Fact]
    public async Task SharedAndSpecificIdentityUniquenessHistoryAndCopySurviveRestart()
    {
        await using var f = await Fixture.Create(); var key = TradingPlanKey.AllAccounts(f.Key.NewYorkDate);
        var shared = await Ready(f.Store, await Save(f.Store, key, Shared(f)));
        var specific = await f.CreatePlan();
        var duplicate = await f.Store.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, key, Shared(f)));
        Assert.Equal(TradingPlanWriteStatus.TargetAlreadyExists, duplicate.Status);
        await using (var db = f.Context())
        {
            var row = await db.TradingPlans.AsNoTracking().SingleAsync(p => p.Id == shared.Id);
            row.Id = Guid.NewGuid(); db.TradingPlans.Add(row);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await f.Restart();
        var loaded = (await f.Store.LoadAsync(key))!; var ready = loaded.LatestReadyRevision!;
        await using (var db = f.Context())
            await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.GetService<IMigrator>().MigrateAsync("20261017152354_AddTradingPlans"));
        Assert.Equal(loaded.Version, (await f.Store.LoadAsync(key))!.Version);
        Assert.Null(loaded.Key.AccountId); Assert.Equal(TradingPlanScope.AllAccounts, loaded.Key.Scope);
        Assert.Null(ready.References.Account.AccountId); Assert.Equal("USD", ready.References.Account.Currency);
        Assert.Equal(100, ready.Content.RiskLimits.DailyLossLimit);
        Assert.Equal("", ready.Content.Scenarios[0].Condition); Assert.Equal("", ready.Content.Scenarios[0].Name);
        Assert.Null(ready.Content.Scenarios[0].Setup);
        Assert.Single(await f.Store.HistoryAsync(new(null, key.NewYorkDate, key.NewYorkDate)));
        Assert.Equal(specific.Id, (await f.Store.LoadAsync(f.Key))!.Id);
        var copy = await f.Store.WriteAsync(new(TradingPlanWriteOperation.CopyForward, TradingPlanKey.AllAccounts(key.NewYorkDate.AddDays(1)), ready.Content, sourceRevision: ready.Id));
        Assert.Equal(TradingPlanWriteStatus.Success, copy.Status); Assert.Null(copy.Plan!.Key.AccountId);
        Assert.Equal("USD", copy.Plan.WorkingDraft!.Content.PlanCurrency); Assert.Empty(copy.Plan.WorkingDraft.Content.Screenshots);
    }

    [Fact]
    public async Task SharedRequiresExplicitCurrencyAndStaleWritesLeaveOriginalUnchanged()
    {
        await using var f = await Fixture.Create(); var key = TradingPlanKey.AllAccounts(f.Key.NewYorkDate);
        var invalid = await f.Store.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, key, f.Content));
        Assert.Equal(TradingPlanWriteStatus.Invalid, invalid.Status); Assert.Null(await f.Store.LoadAsync(key));
        var p = await Save(f.Store, key, Shared(f)); var next = await Save(f.Store, key, Shared(f, "EUR"), p);
        var stale = await f.Store.WriteAsync(new(TradingPlanWriteOperation.SaveDraft, key, Shared(f), new(p.Id, p.Version)));
        Assert.Equal(TradingPlanWriteStatus.Conflict, stale.Status);
        Assert.Equal("EUR", (await f.Store.LoadAsync(key))!.WorkingDraft!.Content.PlanCurrency);
        Assert.Equal(next.Version, (await f.Store.LoadAsync(key))!.Version);
    }

    [Fact]
    public async Task PreviousSchemaMigratesWithoutChangingAccountReadyPayload()
    {
        await using var f = await Fixture.Create("20261017152354_AddTradingPlans");
        // No attachments: the new store does not query the new ledger for this legacy content.
        var legacyContent = new TradingPlanContent(f.Content.Session, f.Content.Focus, instruments: f.Content.Instruments,
            riskLimits: f.Content.RiskLimits, intent: TradingPlanIntent.StandAside, standAside: "Synthetic stand-aside preparation");
        // Seed the previous schema using its columns, not today's EF Plan mapping.
        var p = new TradingPlan(f.Key, legacyContent, f.Now);
        p.SetConfirmations(p.Version, TradingPlanChecklist.All, f.Now);
        p.MarkReady(p.Version, TradingPlanReferenceSnapshot.Capture(f.Key, p.WorkingDraft!.Content,
            f.Account, [f.Instrument], [], [], f.Now), f.Now);
        string payload = TradingPlanJson.WriteReady(p.LatestReadyRevision!);
        await using (var db = f.Context())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO TradingPlans (Id,AccountId,NewYorkDate,Version,LatestReadyRevision,DraftJson,ReferenceStateJson,CreatedAtUtc,UpdatedAtUtc) VALUES ({p.Id},{p.Key.AccountId},{p.Key.NewYorkDate},{p.Version},1,NULL,NULL,{p.CreatedAtUtc.UtcDateTime},{p.UpdatedAtUtc.UtcDateTime})");
            db.TradingPlanReadyRevisions.Add(new() { PlanId = p.Id, Revision = 1, ReadyAtUtc = f.Now, SnapshotJson = payload });
            await db.SaveChangesAsync();
            await db.GetService<IMigrator>().MigrateAsync();
        }
        await f.Restart(); var loaded = (await f.Store.LoadAsync(f.Key))!;
        Assert.Equal(p.Id, loaded.Id); Assert.Equal(p.CreatedAtUtc, loaded.CreatedAtUtc); Assert.Equal(p.Version, loaded.Version);
        await using var check = f.Context(); Assert.Equal(payload, (await check.TradingPlanReadyRevisions.SingleAsync()).SnapshotJson);
        Assert.Equal(f.Account.Id, loaded.LatestReadyRevision!.Key.AccountId);
    }

    [Fact]
    public async Task SharedScreenshotReadyRetentionCopyForwardAndBackupRestoreExportPreserveOriginalBytes()
    {
        await using var f = await Fixture.Create();
        // Accept the combined archive, not only each feature's separate round trip:
        // exact-Account Ready + working Draft, legacy Playbook evidence, global reminders,
        // and the deleted global Plan with a retained image below.
        var accountPlan = await f.Ready(await f.CreatePlan());
        string accountReady = TradingPlanJson.WriteReady(accountPlan.LatestReadyRevision!);
        accountPlan = await f.Success(TradingPlanWriteOperation.StartDraft, accountPlan, source: accountPlan.LatestReadyRevision!.Id);
        string accountDraft = TradingPlanJson.WriteDraft(accountPlan.WorkingDraft!);
        var principles = (await f.Get<IDailyTradingPrincipleStore>().AddAsync("  Следвай плана\r\nБез прибързване  ", 0)).Snapshot!;
        byte[] bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jF9sAAAAASUVORK5CYII=");
        var storage = f.Get<IPlaybookImageStorage>(); var image = await storage.StoreAsync("chart.png", bytes);
        var shot = new TradingPlanScreenshot(Guid.NewGuid(), f.Instrument.Id, TradingPlanTimeframe.H1, image);
        var key = TradingPlanKey.AllAccounts(f.Key.NewYorkDate);
        var p = await Ready(f.Store, await Save(f.Store, key, Shared(f, images: [shot])));
        var ready = p.LatestReadyRevision!;
        var withoutImages = await f.Store.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, TradingPlanKey.AllAccounts(key.NewYorkDate.AddDays(4)), new()));
        Assert.Equal(TradingPlanWriteStatus.Success, withoutImages.Status);
        Assert.Empty(withoutImages.Plan!.WorkingDraft!.Content.Screenshots);
        Assert.Single((await f.Store.LoadReadyAsync(ready.Id))!.Content.Screenshots);
        p = (await f.Store.WriteAsync(new(TradingPlanWriteOperation.StartDraft, key, ready.Content, new(p.Id, p.Version), ready.Id))).Plan!;
        p = await Save(f.Store, key, Shared(f), p);
        await storage.DeleteUnreferencedAsync(image.StorageKey);
        Assert.Equal(bytes, await storage.ReadAsync(image));
        Assert.Single((await f.Store.LoadReadyAsync(ready.Id))!.Content.Screenshots);
        await using (var db = f.Context())
        {
            await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.Database.ExecuteSqlRawAsync("UPDATE TradingPlanAssets SET Timeframe=1"));
            await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM TradingPlanAssets"));
        }
        Assert.Empty(p.WorkingDraft!.Content.Screenshots);
        var copy = await f.Store.WriteAsync(new(TradingPlanWriteOperation.CopyForward, TradingPlanKey.AllAccounts(key.NewYorkDate.AddDays(1)), ready.Content, sourceRevision: ready.Id));
        Assert.Equal(TradingPlanWriteStatus.Success, copy.Status); Assert.Empty(copy.Plan!.WorkingDraft!.Content.Screenshots);
        var prefill = await f.Store.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, TradingPlanKey.AllAccounts(key.NewYorkDate.AddDays(3)), new()));
        Assert.Equal(TradingPlanWriteStatus.Success, prefill.Status); Assert.Empty(prefill.Plan!.WorkingDraft!.Content.Screenshots);
        Assert.Equal(TradingPlanWriteStatus.Success, (await f.Store.WriteAsync(TradingPlanPersistenceTests.Command(TradingPlanWriteOperation.Delete, p))).Status);
        Assert.Null(await f.Store.LoadAsync(key));
        await storage.DeleteUnreferencedAsync(image.StorageKey);
        Assert.Equal(bytes, await storage.ReadAsync(image));
        string archive = Path.Combine(f.Root, "shared.ptjbackup"), export = Path.Combine(f.Root, "export");
        Assert.True((await f.Get<IBackupArchiveService>().CreateAsync(archive)).Succeeded);
        var preflight = await new RestorePreflightService().InspectAsync(archive, f.Root); Assert.True(preflight.Validated, preflight.ToString());
        Assert.True((await f.Get<IPortableExportService>().CreateAsync(export)).Succeeded);
        using (var json = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(export, "journal.json"))))
        {
            Assert.Equal(17, json.RootElement.GetProperty("version").GetInt32());
            var tables = json.RootElement.GetProperty("tables");
            Assert.Single(tables.GetProperty("tradingPlanAssets").EnumerateArray());
            Assert.Contains(tables.GetProperty("tradingPlans").EnumerateArray(), row => row.GetProperty("draftJson").GetString() == accountDraft);
            Assert.Contains(tables.GetProperty("tradingPlanReadyRevisions").EnumerateArray(), row => row.GetProperty("snapshotJson").GetString() == accountReady);
            Assert.Equal(principles.Entries[0].Text, Assert.Single(tables.GetProperty("dailyTradingPrinciples").EnumerateArray()).GetProperty("text").GetString());
        }
        Assert.Empty(Directory.GetFiles(export, "*.png", SearchOption.AllDirectories));
        await using var target = await Fixture.Create(); await target.Stop();
        var restored = await new JournalRestoreService(target.Paths).RestoreAsync(new(archive, preflight.Summary!.ArchiveSha256));
        Assert.Equal(JournalRestoreStatus.Restored, restored.Status);
        await target.Restart();
        Assert.Null(await target.Store.LoadAsync(key));
        Assert.Equal(bytes, await target.Get<IPlaybookImageStorage>().ReadAsync(image));
        Assert.Equal(shot, Assert.Single((await target.Store.LoadReadyAsync(ready.Id))!.Content.Screenshots));
        var restoredAccount = (await target.Store.LoadAsync(f.Key))!;
        Assert.Equal(accountPlan.Id, restoredAccount.Id);
        Assert.Equal(accountPlan.Version, restoredAccount.Version);
        Assert.Equal(accountReady, TradingPlanJson.WriteReady(restoredAccount.LatestReadyRevision!));
        Assert.Equal(accountDraft, TradingPlanJson.WriteDraft(restoredAccount.WorkingDraft!));
        var restoredPrinciples = await target.Get<IDailyTradingPrincipleStore>().ReadAsync();
        Assert.Equal(principles.Version, restoredPrinciples.Version);
        Assert.Equal(principles.Entries, restoredPrinciples.Entries);
        Assert.Equal(JsonSerializer.Serialize(f.Playbook), JsonSerializer.Serialize(await target.Get<ITradingSetupPlaybookStore>().GetLatestAsync(f.Setup.Id)));
    }
}
