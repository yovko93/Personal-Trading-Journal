using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Application.Setups;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Setups;

internal sealed class SetupExampleStore(IDbContextFactory<JournalDbContext> factory, IPlaybookImageStorage files) : ISetupExampleStore
{
    public async Task<IReadOnlyList<SetupExample>> ListAsync(Guid setupId, CancellationToken token)
    {
        await using var db = await factory.CreateDbContextAsync(token);
        var rows = await db.SetupExamples.AsNoTracking().Where(e => e.SetupId == setupId)
            .OrderBy(e => e.Id).ToListAsync(token);
        return rows.Select(e => new SetupExample(e.Id, e.SetupId, e.Timeframe,
            new(e.StorageKey, e.FileName, e.SizeBytes, e.Sha256))).ToArray();
    }

    public async Task<SetupExample> AddAsync(Guid setupId, string fileName, byte[] bytes, string? timeframe, CancellationToken token)
    {
        await using var db = await factory.CreateDbContextAsync(token);
        var image = await PrepareAsync(db, setupId, new(fileName, bytes, timeframe), token);
        try
        {
            await db.SaveChangesAsync(token);
            return image;
        }
        catch
        {
            await files.DeleteUnreferencedAsync(image.File.StorageKey, CancellationToken.None);
            throw;
        }
    }

    public async Task CreateWithExamplesAsync(TradingSetup setup, IReadOnlyList<NewSetupExample> examples, CancellationToken token)
    {
        await using var db = await factory.CreateDbContextAsync(token);
        db.TradingSetups.Add(TradingSetupPersistenceMapper.ToRecord(setup));
        var prepared = new List<SetupExample>(examples.Count);
        try
        {
            foreach (var example in examples)
                prepared.Add(await PrepareAsync(db, setup.Id, example, token));

            // EF commits the Setup and all its image references in one transaction.
            await db.SaveChangesAsync(token);
        }
        catch (Exception saveError)
        {
            // Every deletion takes the same screenshot capture lease. Reclaim the batch in order.
            var errors = new List<Exception>();
            foreach (var image in prepared)
            {
                try { await files.DeleteUnreferencedAsync(image.File.StorageKey, CancellationToken.None); }
                catch (Exception cleanupError) { errors.Add(cleanupError); }
            }
            if (errors.Count > 0)
                throw new AggregateException("Setup creation and image cleanup failed.", [saveError, .. errors]);
            throw;
        }
    }

    private async Task<SetupExample> PrepareAsync(JournalDbContext db, Guid setupId, NewSetupExample example, CancellationToken token)
    {
        string? timeframe = string.IsNullOrWhiteSpace(example.Timeframe) ? null : example.Timeframe.Trim();
        if (timeframe?.Length > 80) throw new ArgumentException("Timeframe exceeds 80 characters.", nameof(example));
        var file = await files.StoreAsync(example.FileName, example.Bytes, token);
        try
        {
            var image = new SetupExample(Guid.CreateVersion7(), setupId, timeframe, file);
            db.SetupExamples.Add(new SetupExampleRecord
            {
                Id = image.Id, SetupId = setupId, Timeframe = timeframe, StorageKey = file.StorageKey,
                FileName = file.FileName, SizeBytes = file.SizeBytes, Sha256 = file.Sha256
            });
            return image;
        }
        catch
        {
            await files.DeleteUnreferencedAsync(file.StorageKey, CancellationToken.None);
            throw;
        }
    }

    public async Task RemoveAsync(Guid setupId, Guid imageId, CancellationToken token)
    {
        await using var db = await factory.CreateDbContextAsync(token);
        var row = await db.SetupExamples.SingleAsync(e => e.Id == imageId && e.SetupId == setupId, token);
        db.SetupExamples.Remove(row);
        await db.SaveChangesAsync(token);
        await files.DeleteUnreferencedAsync(row.StorageKey, CancellationToken.None);
    }
}
