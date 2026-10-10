using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonalTradingJournal.Domain.Journals;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Migrations;

public sealed partial class InitialMigrationTests
{
    [Fact]
    public void ReviewMigrationBackfillsCurrentAndHistoryWithoutChangingLegacyContentStateOrScope()
    {
        RunWithMigratedDatabase((_, options) =>
        {
            Guid draftId = Guid.NewGuid();
            Guid completedId = Guid.NewGuid();
            using var context = new JournalDbContext(options);
            // Raw inserts use the actual M14.1 schema; the current model has the new answer columns.
            foreach ((Guid id, DateOnly date, bool draft) in new[]
            {
                (draftId, new DateOnly(2026, 10, 4), true),
                (completedId, new DateOnly(2026, 10, 5), false),
            })
            {
                string dateText = date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                context.Database.ExecuteSqlInterpolated($"""
                    INSERT INTO DailyJournals (Id, TradingDate, TradingAccountId, Text, IsDraft, Revision, CreatedAtUtc, UpdatedAtUtc)
                    VALUES ({id}, {dateText}, NULL, {"  Legacy\r\ntext  "}, {draft}, 2, {CreatedAtUtc.UtcDateTime}, {CreatedAtUtc.AddMinutes(1).UtcDateTime});
                    """);
                context.Database.ExecuteSqlInterpolated($"""
                    INSERT INTO DailyJournalRevisions (JournalId, Revision, Text, IsDraft, SavedAtUtc)
                    VALUES ({id}, 1, {"First draft"}, 1, {CreatedAtUtc.UtcDateTime});
                    """);
                context.Database.ExecuteSqlInterpolated($"""
                    INSERT INTO DailyJournalRevisions (JournalId, Revision, Text, IsDraft, SavedAtUtc)
                    VALUES ({id}, 2, {"  Legacy\r\ntext  "}, {draft}, {CreatedAtUtc.AddMinutes(1).UtcDateTime});
                    """);
            }
            var connection = (SqliteConnection)context.Database.GetDbConnection();
            connection.Open();
            string[] priorSchema = ReadNonJournalSchema(connection);

            context.GetService<IMigrator>().Migrate(DailyReviewMigrationId);

            Assert.Equal(DailyReviewMigrationId, context.Database.GetAppliedMigrations().Last());
            Assert.Equal(priorSchema, ReadNonJournalSchema(connection));
            foreach (string table in new[] { "DailyJournals", "DailyJournalRevisions" })
            foreach (string answer in new[] { "WentWell", "NeedsImprovement", "NextTradingDay" })
            {
                ColumnDefinition column = FindColumn(connection, table, answer);
                Assert.True(column.IsRequired);
                Assert.Equal("TEXT", column.StoreType);
                Assert.Equal("''", column.DefaultValue);
            }
            var current = context.DailyJournals.AsNoTracking().OrderBy(r => r.TradingDate).ToArray();
            Assert.Equal(2, current.Length);
            Assert.Equal(new[] { true, false }, current.Select(r => r.IsDraft));
            Assert.Equal(new[] { draftId, completedId }, current.Select(r => r.Id));
            Assert.All(current, record =>
            {
                Assert.Equal("  Legacy\r\ntext  ", record.Text);
                Assert.Null(record.TradingAccountId);
                Assert.Equal(2L, record.Revision);
                Assert.Equal(CreatedAtUtc, record.CreatedAtUtc);
                Assert.Equal(CreatedAtUtc.AddMinutes(1), record.UpdatedAtUtc);
                // Even a historical completed row with no structured answers remains readable.
                Assert.Equal(DailyReviewAnswers.Empty, DailyJournalPersistenceMapper.ToDomain(record).Review);
            });
            var history = context.DailyJournalRevisions.AsNoTracking().ToArray();
            Assert.Equal(4, history.Length);
            Assert.All(history, record =>
            {
                Assert.Equal("", record.WentWell);
                Assert.Equal("", record.NeedsImprovement);
                Assert.Equal("", record.NextTradingDay);
            });
            Assert.Equal(2, history.Count(r => r.Text == "First draft" && r.IsDraft && r.Revision == 1));
            Assert.Equal(1, history.Count(r => !r.IsDraft && r.Revision == 2 && r.JournalId == completedId));

            context.GetService<IMigrator>().Migrate(DailyJournalsMigrationId);

            Assert.Equal(DailyJournalsMigrationId, context.Database.GetAppliedMigrations().Last());
            Assert.Equal(priorSchema, ReadNonJournalSchema(connection));
            Assert.Equal(2L, ReadRowCount(connection, "DailyJournals"));
            Assert.Equal(4L, ReadRowCount(connection, "DailyJournalRevisions"));
            foreach (string table in new[] { "DailyJournals", "DailyJournalRevisions" })
                Assert.DoesNotContain(ReadColumns(connection, table), column =>
                    column.Name is "WentWell" or "NeedsImprovement" or "NextTradingDay");
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT Text, IsDraft, Revision, TradingAccountId FROM DailyJournals ORDER BY TradingDate";
            using SqliteDataReader reader = command.ExecuteReader();
            foreach (bool expectedDraft in new[] { true, false })
            {
                Assert.True(reader.Read());
                Assert.Equal("  Legacy\r\ntext  ", reader.GetString(0));
                Assert.Equal(expectedDraft, reader.GetBoolean(1));
                Assert.Equal(2L, reader.GetInt64(2));
                Assert.True(reader.IsDBNull(3));
            }
            Assert.False(reader.Read());
        }, DailyJournalsMigrationId);
    }
}
