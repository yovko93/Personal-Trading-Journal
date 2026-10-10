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
    public void CoachingMigrationIsAdditiveIndependentAndDowngradePreservesJournals()
    {
        RunWithMigratedDatabase((_, options) =>
        {
            using var context = new JournalDbContext(options);
            var journal = new DailyJournalRecord
            {
                Id = Guid.NewGuid(), TradingDate = new(2026, 10, 8), Text = "Existing private note",
                IsDraft = true, Revision = 1, CreatedAtUtc = CreatedAtUtc, UpdatedAtUtc = CreatedAtUtc,
            };
            context.DailyJournals.Add(journal);
            context.SaveChanges();
            var connection = (SqliteConnection)context.Database.GetDbConnection();
            connection.Open();
            var before = ReadTableNames(connection);
            context.Database.Migrate();
            Assert.Equal(CoachingAnalysisMigrationId, context.Database.GetAppliedMigrations().Last());
            Assert.Equal(before.Append("CoachingAnalyses").Order(StringComparer.Ordinal), ReadTableNames(connection));
            Assert.Empty(ReadForeignKeys(connection, "CoachingAnalyses"));
            Assert.Equal(0, ReadRowCount(connection, "CoachingAnalyses"));
            Assert.False(context.Database.HasPendingModelChanges());
            // The DB constraint rejects ambiguous scope even if a caller bypasses Application.
            context.CoachingAnalyses.Add(new()
            {
                Id = Guid.NewGuid(), ReviewDate = journal.TradingDate, ScopeKind = 0, AccountId = Guid.NewGuid(),
                GeneratedAtUtc = CreatedAtUtc,
            });
            Assert.Throws<DbUpdateException>(() => context.SaveChanges());
            context.ChangeTracker.Clear();
            context.GetService<IMigrator>().Migrate(DailyReviewMigrationId);
            Assert.Equal(before, ReadTableNames(connection));
            Assert.Equal("Existing private note", context.DailyJournals.AsNoTracking().Single().Text);
            context.Database.Migrate();
            Assert.Equal("Existing private note", context.DailyJournals.AsNoTracking().Single().Text);
        }, DailyReviewMigrationId);
    }
}
