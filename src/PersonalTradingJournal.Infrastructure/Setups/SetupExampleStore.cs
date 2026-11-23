using Microsoft.EntityFrameworkCore;
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
        timeframe = string.IsNullOrWhiteSpace(timeframe) ? null : timeframe.Trim();
        if (timeframe?.Length > 80) throw new ArgumentException("Timeframe exceeds 80 characters.", nameof(timeframe));
        await using var db = await factory.CreateDbContextAsync(token);
        var file = await files.StoreAsync(fileName, bytes, token);
        try
        {
            var image = new SetupExample(Guid.CreateVersion7(), setupId, timeframe, file);
            db.SetupExamples.Add(new SetupExampleRecord
            {
                Id = image.Id, SetupId = setupId, Timeframe = timeframe, StorageKey = file.StorageKey,
                FileName = file.FileName, SizeBytes = file.SizeBytes, Sha256 = file.Sha256
            });
            await db.SaveChangesAsync(token);
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
