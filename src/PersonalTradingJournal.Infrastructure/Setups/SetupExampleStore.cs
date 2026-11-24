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
        await SaveBatchAsync(db, setup.Id, examples, null, token);
    }

    public async Task<bool> UpdateWithExamplesAsync(TradingSetup setup, bool detailsChanged, SetupImageChanges changes, CancellationToken token)
    {
        await using var db = await factory.CreateDbContextAsync(token);
        var removed = new List<string>();
        await SaveBatchAsync(db, setup.Id, changes.Added, async () =>
        {
            var owner = await db.TradingSetups.SingleAsync(row => row.Id == setup.Id, token);
            if (detailsChanged)
            {
                owner.Name = setup.Name;
                owner.Description = setup.Description;
                owner.UpdatedAtUtc = setup.UpdatedAtUtc;
            }
            var rows = await db.SetupExamples.Where(row => row.SetupId == setup.Id).ToDictionaryAsync(row => row.Id, token);
            foreach (var edit in changes.Edited)
            {
                if (!rows.Remove(edit.Id, out var row) || row.Timeframe != edit.OriginalTimeframe)
                    throw new InvalidOperationException("The saved example changed. Reopen the Setup before editing.");
                if (edit.Remove)
                {
                    db.SetupExamples.Remove(row);
                    removed.Add(row.StorageKey);
                }
                else
                    row.Timeframe = NormalizeTimeframe(edit.Timeframe);
            }
        }, token);
        // The database commit succeeded. Report any physical cleanup failure separately from Save.
        return (await ReclaimAsync(removed)).Count == 0;
    }

    private async Task SaveBatchAsync(JournalDbContext db, Guid setupId, IReadOnlyList<NewSetupExample> added,
        Func<Task>? applyEdits, CancellationToken token)
    {
        var prepared = new List<string>(added.Count);
        try
        {
            foreach (var example in added)
                prepared.Add((await PrepareAsync(db, setupId, example, token)).File.StorageKey);

            await using var transaction = await db.Database.BeginTransactionAsync(token);
            if (applyEdits is not null) await applyEdits();
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        }
        catch (Exception saveError)
        {
            var errors = await ReclaimAsync(prepared);
            if (errors.Count > 0)
                throw new AggregateException("Saving Setup examples and reclaiming files failed.", [saveError, .. errors]);
            throw;
        }
    }

    private async Task<List<Exception>> ReclaimAsync(IEnumerable<string> keys)
    {
        var errors = new List<Exception>();
        foreach (var key in keys.Distinct())
        {
            try { await files.DeleteUnreferencedAsync(key, CancellationToken.None); }
            catch (Exception error) { errors.Add(error); }
        }
        return errors;
    }

    private static string? NormalizeTimeframe(string? value)
    {
        string? timeframe = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (timeframe?.Length > 80) throw new ArgumentException("Timeframe exceeds 80 characters.", nameof(value));
        return timeframe;
    }

    private async Task<SetupExample> PrepareAsync(JournalDbContext db, Guid setupId, NewSetupExample example, CancellationToken token)
    {
        string? timeframe = NormalizeTimeframe(example.Timeframe);
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
