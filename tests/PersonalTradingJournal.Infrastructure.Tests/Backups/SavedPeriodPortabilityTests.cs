using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Application.PeriodReviews.Coaching;
using PersonalTradingJournal.Infrastructure.Backups;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Tests.Persistence;
using PersonalTradingJournal.Infrastructure.Tests.Persistence.DailyReview;
using Fixture = PersonalTradingJournal.Infrastructure.Tests.Backups.BackupArchiveTests.Fixture;

namespace PersonalTradingJournal.Infrastructure.Tests.Backups;

public sealed class SavedPeriodPortabilityTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task PeriodBackupPreflightOfflineRestoreRecoveryAndExportPreserveExactSnapshots(bool interrupted)
    {
        await using var source = await Fixture.Create(); await source.Add("synthetic.png");
        var store = source.Services.GetRequiredService<IPeriodReviewRepository>(); var account = Guid.NewGuid();
        var week = await store.SaveAsync(PeriodReviewRepositoryTests.Snapshot(PeriodReviewRepositoryTests.Query(account)));
        var month = await store.SaveAsync(PeriodReviewRepositoryTests.Snapshot(PeriodReviewRepositoryTests.Query(null, ReviewPeriodKind.Month)));
        Assert.True((await source.Service().CreateAsync(source.Output)).Succeeded);
        var preflight = await new RestorePreflightService().InspectAsync(source.Output, source.Root);
        Assert.True(preflight.Validated, preflight.ToString()); Assert.Equal(2, preflight.Summary!.PeriodAnalysisCount);
        Assert.Equal(2, preflight.Summary.AnalysisCount); Assert.Equal(0, preflight.Summary.DailyAnalysisCount);
        string export = Path.Combine(source.Root, "synthetic-export");
        Assert.True((await source.Services.GetRequiredService<IPortableExportService>().CreateAsync(export)).Succeeded);
        using (var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(export, "journal.json"))))
        {
            var root = document.RootElement; Assert.Equal(11, root.GetProperty("version").GetInt32());
            Assert.False(root.GetProperty("restorableBackup").GetBoolean()); Assert.Equal("metadata-only", root.GetProperty("screenshotContent").GetString());
            var rows = root.GetProperty("tables").GetProperty("periodReviews"); Assert.Equal(2, rows.GetArrayLength());
            foreach (var r in rows.EnumerateArray())
            {
                var expected = r.GetProperty("id").GetGuid() == week.Summary.Id ? week : month;
                Assert.Equal(expected.EvidenceJson, r.GetProperty("evidenceJson").GetString()); Assert.Equal(expected.ResponseJson, r.GetProperty("responseJson").GetString());
                Assert.Equal(expected.Summary.FirstDate.ToString("yyyy-MM-dd"), r.GetProperty("firstDateNewYork").GetString());
                Assert.Equal(expected.Summary.FromUtc, DateTimeOffset.Parse(r.GetProperty("fromUtc").GetString()!));
                Assert.Equal(expected.Summary.Scope.AccountId?.ToString(), r.GetProperty("accountId").ValueKind == JsonValueKind.Null ? null : r.GetProperty("accountId").GetString()!.ToLowerInvariant());
            }
        }
        Assert.Equal(4, Directory.GetFiles(export, "*.csv").Length);
        await using var target = await Fixture.Create();
        var original = await target.Services.GetRequiredService<IPeriodReviewRepository>().SaveAsync(PeriodReviewRepositoryTests.Snapshot());
        await target.Services.DisposeAsync(); JournalDataSession.ClearOwnedPools(target.Paths);
        var request = new JournalRestoreRequest(source.Output, preflight.Summary.ArchiveSha256);
        JournalRestoreResult outcome;
        if (interrupted)
        {
            await Assert.ThrowsAsync<RestoreInterruption>(() => new JournalRestoreService(target.Paths, (phase, _) =>
            { if (phase == JournalRestorePhase.IncomingInstalled) throw new RestoreInterruption(); }).RestoreAsync(request));
            outcome = await new JournalRestoreService(target.Paths).RecoverInterruptedAsync();
            Assert.Equal(JournalRestoreStatus.RecoveredOriginal, outcome.Status);
        }
        else
        {
            outcome = await new JournalRestoreService(target.Paths).RestoreAsync(request);
            Assert.Equal(JournalRestoreStatus.Restored, outcome.Status);
        }
        Assert.NotNull(outcome.RecoveryArchivePath);
        var recovery = await new RestorePreflightService().InspectAsync(outcome.RecoveryArchivePath!, target.Root);
        Assert.True(recovery.Validated, recovery.ToString()); Assert.Equal(1, recovery.Summary!.PeriodAnalysisCount);
        await using var fresh = new ServiceCollection().AddPersistence(target.Paths).BuildServiceProvider();
        var restored = fresh.GetRequiredService<IPeriodReviewRepository>();
        if (interrupted)
        {
            Assert.Equal(original, await restored.GetAsync(original.Summary.Id)); Assert.Null(await restored.GetAsync(week.Summary.Id));
        }
        else
        {
            Assert.Equal(week, await restored.GetAsync(week.Summary.Id)); Assert.Equal(month, await restored.GetAsync(month.Summary.Id));
            Assert.Null(await restored.GetAsync(original.Summary.Id));
            Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(Path.Combine(target.Paths.ScreenshotsDirectory, "synthetic.png")));
        }
        Assert.False(File.Exists(JournalDataSession.StatePath(target.Paths)));
        await fresh.DisposeAsync(); SqliteTestPoolCleanup.ClearPersistencePools(target.Paths.DatabasePath);
    }
}
