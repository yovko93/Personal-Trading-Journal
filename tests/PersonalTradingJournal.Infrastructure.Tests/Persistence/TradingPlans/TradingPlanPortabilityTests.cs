using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Infrastructure.Backups;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.TradingPlans;
using Fixture = PersonalTradingJournal.Infrastructure.Tests.Persistence.TradingPlans.TradingPlanPersistenceTests.Fixture;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.TradingPlans;

public sealed class TradingPlanPortabilityTests
{
    [Fact]
    public async Task BackupPreflightOfflineRestoreAndLogicalExportPreserveDraftAndReadyEvidence()
    {
        await using var source = await Fixture.Create();
        var plan = await source.Ready(await source.CreatePlan());
        string ready = TradingPlanJson.WriteReady(plan.LatestReadyRevision!);
        plan = await source.Success(TradingPlanWriteOperation.StartDraft, plan, source: plan.LatestReadyRevision!.Id);
        var draft = TradingPlanJson.WriteDraft(plan.WorkingDraft!);
        string archive = Path.Combine(source.Root, "plan.ptjbackup"), export = Path.Combine(source.Root, "export");
        var backup = await source.Get<IBackupArchiveService>().CreateAsync(archive);
        Assert.True(backup.Succeeded, backup.ToString());
        var check = await new RestorePreflightService().InspectAsync(archive, source.Root);
        Assert.True(check.Validated, check.ToString());
        Assert.True((await source.Get<IPortableExportService>().CreateAsync(export)).Succeeded);
        using (var json = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(export, "journal.json"))))
        {
            Assert.Equal(15, json.RootElement.GetProperty("version").GetInt32());
            var tables = json.RootElement.GetProperty("tables");
            Assert.Equal(draft, tables.GetProperty("tradingPlans")[0].GetProperty("draftJson").GetString());
            Assert.Equal(ready, tables.GetProperty("tradingPlanReadyRevisions")[0].GetProperty("snapshotJson").GetString());
        }
        Assert.Equal(4, Directory.GetFiles(export, "*.csv").Length);
        await using var target = await Fixture.Create(); var original = await target.Ready(await target.CreatePlan());
        await target.Stop();
        var outcome = await new JournalRestoreService(target.Paths).RestoreAsync(new(archive, check.Summary!.ArchiveSha256));
        Assert.Equal(JournalRestoreStatus.Restored, outcome.Status);
        Assert.NotNull(outcome.RecoveryArchivePath);
        Assert.True((await new RestorePreflightService().InspectAsync(outcome.RecoveryArchivePath!, target.Root)).Validated);
        await target.Restart();
        var restored = (await target.Store.LoadAsync(source.Key))!;
        Assert.Equal(plan.Version, restored.Version);
        Assert.Equal(ready, TradingPlanJson.WriteReady(restored.LatestReadyRevision!));
        Assert.Equal(draft, TradingPlanJson.WriteDraft(restored.WorkingDraft!));
        Assert.Null(await target.Store.LoadAsync(original.Key));
        Assert.NotNull(target.Get<WriteTradingPlanUseCase>());
    }
}
