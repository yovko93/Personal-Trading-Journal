using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using PersonalTradingJournal.Infrastructure.Setups;

namespace PersonalTradingJournal.Infrastructure.Trades;

public sealed class TradePlaybookAssociationStore(IDbContextFactory<JournalDbContext> factory, TimeProvider clock)
    : ITradePlaybookAssociationStore
{
    public async Task<TradePlaybookAssociationPreview> PreviewAsync(Guid tradeId, Guid? setupId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        return await ReadPreview(db, tradeId, setupId, cancellationToken);
    }

    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));

    private static async Task<TradePlaybookAssociationPreview> ReadPreview(JournalDbContext db, Guid tradeId, Guid? setupId, CancellationToken ct)
    {
        var trade = await db.Trades.AsNoTracking().SingleOrDefaultAsync(t => t.Id == tradeId, ct)
            ?? throw new KeyNotFoundException("The Trade is unavailable. Refresh before associating a Playbook.");
        var executions = await db.TradeExecutions.AsNoTracking().Where(e => e.TradeId == tradeId).OrderBy(e => e.Sequence).ToArrayAsync(ct);
        var last = await db.TradePlaybookAssociations.AsNoTracking().Where(a => a.TradeId == tradeId).OrderByDescending(a => a.Sequence).FirstOrDefaultAsync(ct);
        var legacy = await db.TradePlaybookContexts.AsNoTracking().SingleOrDefaultAsync(a => a.TradeId == tradeId, ct);
        var mistakes = await db.TradeMistakes.AsNoTracking().Where(m => m.TradeId == tradeId).OrderBy(m => m.Id).ToArrayAsync(ct);
        var screenshots = await db.TradeScreenshots.AsNoTracking().Where(m => m.TradeId == tradeId).OrderBy(m => m.Id).ToArrayAsync(ct);
        var setup = setupId is Guid id ? await db.TradingSetups.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct) : null;
        if (setupId.HasValue && (setup is null || !setup.IsActive))
            throw new InvalidOperationException("The selected Setup is unavailable or archived. Choose an active Setup.");
        var number = setupId.HasValue ? await db.PlaybookRevisions.Where(r => r.TradingSetupId == setupId).MaxAsync(r => (int?)r.Revision, ct) : null;
        var definition = number.HasValue ? await TradingSetupPlaybookStore.ReadRevisionAsync(db, setupId!.Value, number.Value, ct) : null;
        return new(tradeId, setupId, setup?.Name, definition, Hash(new { trade, executions, last, legacy, mistakes, screenshots }),
            Hash(new { setup, Definition = definition is null ? null : PlaybookPersistenceMapper.Serialize(definition) }));
    }

    public async Task<bool> TryApplyAsync(TradePlaybookAssociationPreview preview, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preview);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await db.Database.UseTransactionAsync(transaction, cancellationToken);
        TradePlaybookAssociationPreview current;
        try { current = await ReadPreview(db, preview.TradeId, preview.SetupId, cancellationToken); }
        catch (KeyNotFoundException) { return false; }
        catch (InvalidOperationException) { return false; }
        if (current.TradeToken != preview.TradeToken || current.SelectionToken != preview.SelectionToken ||
            JsonSerializer.Serialize(current.Definition) != JsonSerializer.Serialize(preview.Definition)) return false;
        var trade = await db.Trades.SingleAsync(t => t.Id == preview.TradeId, cancellationToken);
        var recorded = clock.GetUtcNow();
        if (recorded < trade.UpdatedAtUtc) recorded = trade.UpdatedAtUtc;
        if (current.Definition is { } definition && recorded < definition.RecordedAtUtc) recorded = definition.RecordedAtUtc;
        var last = await db.TradePlaybookAssociations.Where(a => a.TradeId == trade.Id).MaxAsync(a => (int?)a.Sequence, cancellationToken) ?? 0;
        var openedAt = await db.TradeExecutions.Where(e => e.TradeId == trade.Id).OrderBy(e => e.Sequence).Select(e => e.ExecutedAtUtc).FirstAsync(cancellationToken);
        db.TradePlaybookAssociations.Add(new()
        {
            TradeId = trade.Id, Sequence = checked(last + 1), TradingSetupId = current.SetupId,
            Revision = current.Definition?.Revision, RecordedAtUtc = recorded,
            TradeOpenedAtUtc = openedAt,
            DefinitionJson = current.Definition is null ? null : PlaybookPersistenceMapper.Serialize(current.Definition),
        });
        trade.TradingSetupId = current.SetupId;
        trade.UpdatedAtUtc = recorded;
        await db.SaveChangesAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync(CancellationToken.None);
        return true;
    }

    public async Task<IReadOnlyList<TradePlaybookAssociation>> ReadHistoryAsync(Guid tradeId, int? beforeSequence = null,
        int limit = 20, CancellationToken cancellationToken = default)
    {
        if (tradeId == Guid.Empty || limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var rows = await db.TradePlaybookAssociations.AsNoTracking().Where(a => a.TradeId == tradeId &&
            (!beforeSequence.HasValue || a.Sequence < beforeSequence.Value)).OrderByDescending(a => a.Sequence).Take(limit).ToArrayAsync(cancellationToken);
        return rows.Select(a =>
        {
            var definition = a.DefinitionJson is null ? null : PlaybookPersistenceMapper.Deserialize(a.DefinitionJson);
            if (definition is not null && (definition.TradingSetupId != a.TradingSetupId || definition.Revision != a.Revision))
                throw new InvalidDataException("Frozen association identity mismatch.");
            return new TradePlaybookAssociation(a.Sequence, a.TradingSetupId, a.RecordedAtUtc, definition, a.TradeOpenedAtUtc);
        }).ToArray();
    }
}
