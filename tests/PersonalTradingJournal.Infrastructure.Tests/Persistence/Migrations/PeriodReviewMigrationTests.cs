using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Migrations;

public sealed partial class InitialMigrationTests
{
    [Fact]
    public void PeriodReviewMigrationAddsIndependentHistoryWithoutRewritingDailyOrSources()
    {
        const string previous = "20261013102243_AddTradingRuleOrder";
        RunWithMigratedDatabase((_, options) =>
        {
            using var db = new JournalDbContext(options);
            var daily = new CoachingAnalysisRecord { Id = Guid.NewGuid(), ReviewDate = new(2026, 10, 8), ScopeKind = 0,
                GeneratedAtUtc = CreatedAtUtc, Provider = "Old", Model = "historical", EvidenceContractVersion = "daily-coaching.v1",
                ResponseContractVersion = "daily-coaching.v1", PacketId = "original", EvidenceJson = "{\"historical\":true}", ResponseJson = "{}", MetadataJson = "{}" };
            db.CoachingAnalyses.Add(daily);
            db.DailyJournals.Add(new() { Id = Guid.NewGuid(), TradingDate = daily.ReviewDate, Text = "Preserved", IsDraft = true,
                Revision = 1, CreatedAtUtc = CreatedAtUtc, UpdatedAtUtc = CreatedAtUtc });
            db.SaveChanges(); db.ChangeTracker.Clear();
            var connection = (SqliteConnection)db.Database.GetDbConnection(); connection.Open(); var tables = ReadTableNames(connection);
            db.GetService<IMigrator>().Migrate(PeriodReviewsMigrationId);
            Assert.Equal(tables.Append("PeriodReviews").Order(StringComparer.Ordinal), ReadTableNames(connection));
            Assert.Empty(ReadForeignKeys(connection, "PeriodReviews")); Assert.Empty(db.PeriodReviews);
            Assert.Equal(daily.EvidenceJson, db.CoachingAnalyses.Single().EvidenceJson); Assert.Equal("Preserved", db.DailyJournals.Single().Text);
            db.PeriodReviews.Add(new() { Id = Guid.NewGuid(), Kind = 0, FirstDate = new(2026, 10, 5), LastDate = new(2026, 10, 11),
                FromUtc = CreatedAtUtc, BeforeUtc = CreatedAtUtc.AddDays(7), GeneratedAtUtc = CreatedAtUtc, ScopeKind = 1, AccountId = null });
            Assert.Throws<DbUpdateException>(() => db.SaveChanges()); db.ChangeTracker.Clear();
            db.GetService<IMigrator>().Migrate(previous); Assert.Equal(tables, ReadTableNames(connection));
            Assert.Equal(daily.EvidenceJson, db.CoachingAnalyses.Single().EvidenceJson);
            db.Database.Migrate(); Assert.False(db.Database.HasPendingModelChanges()); Assert.Empty(db.PeriodReviews);
        }, previous);
    }
}
