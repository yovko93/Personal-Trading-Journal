using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Screenshots;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using PersonalTradingJournal.Infrastructure.Tests.Persistence.TradingPlans;

namespace PersonalTradingJournal.Infrastructure.Tests.Notebook;

public sealed class NotebookMigrationTests
{
    [Fact]
    public async Task PreviousSchemaUpgradePreservesJournalTradeSetupScreenshotsAndResolvesDiAfterRestart()
    {
        await using var f = await TradingPlanPersistenceTests.Fixture.Create("20261019171746_AddDailyTradingPrinciples");
        var tradeId = Guid.NewGuid(); var journalId = Guid.NewGuid(); var screenshotId = Guid.NewGuid();
        await using (var db = f.Context())
        {
            db.Trades.Add(new TradeRecord { Id = tradeId, TradingAccountId = f.Account.Id, InstrumentId = f.Instrument.Id,
                TradingSetupId = f.Setup.Id, PricingCurrency = "USD", PricingPointValue = 20, CreatedAtUtc = f.Now, UpdatedAtUtc = f.Now });
            db.TradeScreenshots.Add(new TradeScreenshotRecord { Id = screenshotId, TradeId = tradeId, Type = TradeScreenshotType.Entry,
                StorageKey = "synthetic.png", FileName = "synthetic.png", Description = "Synthetic chart metadata only", CreatedAtUtc = f.Now, UpdatedAtUtc = f.Now });
            db.DailyJournals.Add(new DailyJournalRecord { Id = journalId, TradingAccountId = f.Account.Id, TradingDate = new(2026, 10, 20),
                Text = "Synthetic journal", WentWell = "Recorded", IsDraft = true, Revision = 1, CreatedAtUtc = f.Now, UpdatedAtUtc = f.Now });
            await db.SaveChangesAsync();
            await db.Database.MigrateAsync();
            Assert.Empty(await db.NotebookNotes.ToArrayAsync());
            Assert.Empty(await db.NotebookFolders.ToArrayAsync());
            Assert.Empty(await db.NotebookTags.ToArrayAsync());
            var trade = await db.Trades.AsNoTracking().SingleAsync();
            Assert.Equal(tradeId, trade.Id); Assert.Equal(f.Setup.Id, trade.TradingSetupId);
            Assert.Equal(20, trade.PricingPointValue); Assert.Equal("USD", trade.PricingCurrency);
            var journal = await db.DailyJournals.AsNoTracking().SingleAsync();
            Assert.Equal(journalId, journal.Id); Assert.Equal("Synthetic journal", journal.Text); Assert.Equal(1, journal.Revision);
            Assert.Equal(f.Now, journal.CreatedAtUtc); Assert.Equal("Recorded", journal.WentWell);
            var screenshot = await db.TradeScreenshots.AsNoTracking().SingleAsync();
            Assert.Equal(screenshotId, screenshot.Id); Assert.Equal("synthetic.png", screenshot.StorageKey);
            Assert.Equal("Synthetic chart metadata only", screenshot.Description);
            Assert.Equal(f.Setup.Name, (await db.TradingSetups.AsNoTracking().SingleAsync()).Name);
            Assert.Single(await db.PlaybookRevisions.ToArrayAsync()); Assert.Single(await db.TradingRuleRevisions.ToArrayAsync());
        }
        var note = NotebookFixture.Success(await f.Get<INotebookService>().ExecuteAsync(new CreateNotebookNote(Title: "Restart")));
        await f.Restart();
        Assert.Equal(note.Title, (await f.Get<INotebookService>().LoadAsync(note.Id)).Note!.Title);
        Assert.NotNull(f.Get<INotebookContentCodec>());
    }

    [Fact]
    public async Task RelationalConstraintsRejectDuplicateAssignmentsAndDestructiveCatalogDeletes()
    {
        await using var f = await NotebookFixture.Create();
        var folder = await f.Folder(); var tag = await f.Tag(); var note = await f.CreateNote(folder: folder.Id);
        note = NotebookFixture.Success(await f.Service().ExecuteAsync(new AssignNotebookTag(note.Id, note.Version, tag.Id)));
        await using (var db = f.Context())
        {
            db.NotebookTagAssignments.Add(new() { NoteId = note.Id, TagId = tag.Id });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using (var db = f.Context())
        {
            await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.NotebookTags.ExecuteDeleteAsync());
            await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.NotebookFolders.ExecuteDeleteAsync());
            await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.NotebookNotes.ExecuteDeleteAsync());
        }
        Assert.Equal(note.Version, (await f.Service().LoadAsync(note.Id)).Note!.Version);
    }
}
