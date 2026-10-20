using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Infrastructure.Backups;
using System.Text.Json;
using Fixture = PersonalTradingJournal.Infrastructure.Tests.Persistence.TradingPlans.TradingPlanPersistenceTests.Fixture;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.TradingPlans;

public sealed class DailyTradingPrincipleTests
{
    [Fact]
    public async Task PreviousSchemaStartsEmptyAndPreservesPlanAndReadyThenRestartPreservesExactUnicodeOrder()
    {
        await using var f = await Fixture.Create("20261019160256_TombstoneTradingPlans");
        var plan = await f.Ready(await f.CreatePlan());
        await using (var db = f.Context()) await db.GetService<IMigrator>().MigrateAsync();
        var store = f.Get<IDailyTradingPrincipleStore>();
        Assert.Empty((await store.ReadAsync()).Entries);
        const string original = "  Следвай стратегия, не емоции\r\nНе бързай  ";
        var a = await store.AddAsync(original, 0); Assert.Equal(PrincipleWriteStatus.Success, a.Status);
        var b = await store.AddAsync("Втори", 1); var c = await store.AddAsync("Трети", 2);
        var removed = await store.RemoveAsync(b.Snapshot!.Entries[1].Id, 3);
        Assert.Equal(PrincipleWriteStatus.Success, removed.Status);
        Assert.Equal(new[] { original, "Трети" }, removed.Snapshot!.Entries.Select(x => x.Text));
        await f.Restart(); var loaded = await f.Get<IDailyTradingPrincipleStore>().ReadAsync();
        Assert.Equal(4, loaded.Version); Assert.Equal(removed.Snapshot.Entries, loaded.Entries);
        Assert.Equal(plan.Version, (await f.Store.LoadAsync(f.Key))!.Version);
        Assert.Equal(PersonalTradingJournal.Infrastructure.TradingPlans.TradingPlanJson.WriteReady(plan.LatestReadyRevision!),
            PersonalTradingJournal.Infrastructure.TradingPlans.TradingPlanJson.WriteReady((await f.Store.LoadAsync(f.Key))!.LatestReadyRevision!));
        Assert.Equal(f.Now, loaded.Entries[0].CreatedAtUtc);
    }

    [Theory]
    [InlineData("")] [InlineData(" \t\r\n")] [InlineData("bad\0text")]
    public async Task InvalidAndCancelledWritesLeaveNoChanges(string invalid)
    {
        await using var f = await Fixture.Create(); var store = f.Get<IDailyTradingPrincipleStore>();
        Assert.Equal(PrincipleWriteStatus.Invalid, (await store.AddAsync(invalid, 0)).Status);
        Assert.Equal(PrincipleWriteStatus.Invalid, (await store.AddAsync(new string('x', 501), 0)).Status);
        Assert.Equal(PrincipleWriteStatus.Cancelled, (await store.AddAsync("Valid", 0, new(true))).Status);
        Assert.Equal(0, (await store.ReadAsync()).Version); Assert.Empty((await store.ReadAsync()).Entries);
        var added = await store.AddAsync("Valid", 0);
        Assert.Equal(PrincipleWriteStatus.Cancelled, (await store.RemoveAsync(added.Snapshot!.Entries[0].Id, 1, new(true))).Status);
        Assert.Single((await store.ReadAsync()).Entries);
    }

    [Fact]
    public async Task CompetingAppendsAndStaleRemovesNeverOverwriteOrRenumberStoredIdentities()
    {
        await using var f = await Fixture.Create(); var store = f.Get<IDailyTradingPrincipleStore>();
        var attempts = await Task.WhenAll(Task.Run(() => store.AddAsync("First", 0)), Task.Run(() => store.AddAsync("Second", 0)));
        Assert.Single(attempts, x => x.Status == PrincipleWriteStatus.Success);
        Assert.Single(attempts, x => x.Status == PrincipleWriteStatus.Conflict);
        var winner = (await store.ReadAsync()).Entries.Single();
        Assert.Equal(PrincipleWriteStatus.Conflict, (await store.RemoveAsync(winner.Id, 0)).Status);
        Assert.Equal(PrincipleWriteStatus.Success, (await store.RemoveAsync(winner.Id, 1)).Status);
        Assert.Equal(PrincipleWriteStatus.Conflict, (await store.RemoveAsync(winner.Id, 2)).Status);
        var last = (await store.AddAsync("Last", 2)).Snapshot!.Entries.Single();
        Assert.NotEqual(winner.Id, last.Id); Assert.True(last.Position > winner.Position);
    }

    [Fact]
    public async Task FailedTokenWriteRollsBackAddedAndRemovedRows()
    {
        await using var f = await Fixture.Create(); var store = f.Get<IDailyTradingPrincipleStore>();
        var first = (await store.AddAsync("Retained", 0)).Snapshot!;
        await using (var db = f.Context())
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER PrincipleFail BEFORE UPDATE ON DailyTradingPrincipleState BEGIN SELECT RAISE(ABORT, 'synthetic failure'); END;");
        Assert.Equal(PrincipleWriteStatus.StorageFailure, (await store.AddAsync("Never published", 1)).Status);
        Assert.Equal(PrincipleWriteStatus.StorageFailure, (await store.RemoveAsync(first.Entries[0].Id, 1)).Status);
        var after = await store.ReadAsync(); Assert.Equal(first.Version, after.Version); Assert.Equal(first.Entries, after.Entries);
    }

    [Fact]
    public async Task PlanDeletionPrefillAndPortabilityDoNotOwnGlobalReminders()
    {
        await using var f = await Fixture.Create(); var store = f.Get<IDailyTradingPrincipleStore>();
        var expected = (await store.AddAsync("Не търгувай ако си уморен", 0)).Snapshot!;
        var plan = await f.Ready(await f.CreatePlan());
        var copy = await f.Store.WriteAsync(new(TradingPlanWriteOperation.CreateDraft, new(f.Account.Id, f.Key.NewYorkDate.AddDays(4)), new()));
        Assert.Equal(TradingPlanWriteStatus.Success, copy.Status);
        Assert.Equal(TradingPlanWriteStatus.Success, (await f.Store.WriteAsync(TradingPlanPersistenceTests.Command(TradingPlanWriteOperation.Delete, plan))).Status);
        Assert.Equal(expected.Entries, (await store.ReadAsync()).Entries);
        string archive = Path.Combine(f.Root, "principles.ptjbackup"), export = Path.Combine(f.Root, "export");
        Assert.True((await f.Get<IBackupArchiveService>().CreateAsync(archive)).Succeeded);
        var preflight = await new RestorePreflightService().InspectAsync(archive, f.Root); Assert.True(preflight.Validated);
        Assert.True((await f.Get<IPortableExportService>().CreateAsync(export)).Succeeded);
        using (var json = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(export, "journal.json"))))
        {
            Assert.Equal(17, json.RootElement.GetProperty("version").GetInt32());
            var row = Assert.Single(json.RootElement.GetProperty("tables").GetProperty("dailyTradingPrinciples").EnumerateArray());
            Assert.Equal(expected.Entries[0].Id, row.GetProperty("id").GetGuid()); Assert.Equal(expected.Entries[0].Text, row.GetProperty("text").GetString());
        }
        await using var target = await Fixture.Create(); await target.Stop();
        Assert.Equal(JournalRestoreStatus.Restored, (await new JournalRestoreService(target.Paths).RestoreAsync(new(archive, preflight.Summary!.ArchiveSha256))).Status);
        await target.Restart(); var restored = await target.Get<IDailyTradingPrincipleStore>().ReadAsync();
        Assert.Equal(expected.Version, restored.Version); Assert.Equal(expected.Entries, restored.Entries);
    }
}
