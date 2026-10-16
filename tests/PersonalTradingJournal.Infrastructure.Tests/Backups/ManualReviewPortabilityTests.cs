using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.PeriodReviews;
using PersonalTradingJournal.Application.PeriodReviews.Coaching;
using PersonalTradingJournal.Infrastructure.Backups;
using PersonalTradingJournal.Infrastructure.DailyReview.Coaching;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Tests.Persistence;
using PersonalTradingJournal.Infrastructure.Tests.Persistence.DailyReview;
using Fixture = PersonalTradingJournal.Infrastructure.Tests.Backups.BackupArchiveTests.Fixture;

namespace PersonalTradingJournal.Infrastructure.Tests.Backups;

public sealed class ManualReviewPortabilityTests
{
    [Fact]
    public async Task CompletedManualSnapshotsRoundTripWhileWorkingDraftsAreExplicitlyExcluded()
    {
        await using var source = await Fixture.Create();
        var services = source.Services; var day = new DateOnly(2026, 10, 2);
        await services.GetRequiredService<IDailyJournalRepository>().CreateAsync(new(day, null, "Synthetic portable observation"));
        var manual = new ManualReviewService(source.Paths, services.GetRequiredService<IDailyReviewEvidenceReader>(), services.GetRequiredService<IPeriodComparisonEvidenceReader>(),
            services.GetRequiredService<ICoachingAnalysisRepository>(), services.GetRequiredService<IPeriodReviewRepository>(), TimeProvider.System);
        var saved = new List<ManualReviewDraft>();
        foreach (var kind in Enum.GetValues<ManualReviewKind>())
        {
            var draft = await manual.PrepareAsync(new(kind, day, null, "All accounts"));
            draft = await manual.KeepResponseAsync(draft, ManualReviewTests.Response(draft)); saved.Add(await manual.SaveAsync(draft));
        }
        Assert.True((await source.Service().CreateAsync(source.Output)).Succeeded);
        using (var zip = ZipFile.OpenRead(source.Output)) Assert.DoesNotContain(zip.Entries, e => e.FullName.Contains("pending-review"));
        var preview = await new RestorePreflightService().InspectAsync(source.Output, source.Root);
        Assert.True(preview.Validated); Assert.Equal(1, preview.Summary!.DailyAnalysisCount); Assert.Equal(2, preview.Summary.PeriodAnalysisCount);
        string export = Path.Combine(source.Root, "manual-export");
        Assert.True((await services.GetRequiredService<IPortableExportService>().CreateAsync(export)).Succeeded);
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(export, "journal.json")));
        Assert.DoesNotContain("pending-review", json.RootElement.GetRawText());
        Assert.Contains("external-paste", json.RootElement.GetRawText());
        await using var target = await Fixture.Create(); await target.Services.DisposeAsync(); JournalDataSession.ClearOwnedPools(target.Paths);
        var restored = await new JournalRestoreService(target.Paths).RestoreAsync(new(source.Output, preview.Summary.ArchiveSha256));
        Assert.Equal(JournalRestoreStatus.Restored, restored.Status);
        await using var fresh = new ServiceCollection().AddPersistence(target.Paths).BuildServiceProvider();
        foreach (var draft in saved)
        {
            string? evidence = draft.Request.Kind == ManualReviewKind.Daily
                ? (await fresh.GetRequiredService<ICoachingAnalysisRepository>().GetAsync(draft.Id))?.EvidenceJson
                : (await fresh.GetRequiredService<IPeriodReviewRepository>().GetAsync(draft.Id))?.EvidenceJson;
            Assert.Equal(draft.EvidenceJson, evidence);
        }
        Assert.False(Directory.Exists(Path.Combine(target.Paths.DataDirectory, "pending-review-prompts")));
        await fresh.DisposeAsync(); SqliteTestPoolCleanup.ClearPersistencePools(target.Paths.DatabasePath);
    }
}
