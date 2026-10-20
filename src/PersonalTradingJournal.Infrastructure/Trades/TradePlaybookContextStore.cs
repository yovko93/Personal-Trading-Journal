using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using PersonalTradingJournal.Infrastructure.Setups;

namespace PersonalTradingJournal.Infrastructure.Trades;

public sealed class TradePlaybookContextStore(IDbContextFactory<JournalDbContext> factory) : ITradePlaybookContextStore
{
    public async Task<TradePlaybookContext?> GetAsync(Guid tradeId, CancellationToken cancellationToken = default)
    {
        if (tradeId == Guid.Empty) throw new ArgumentException("A Trade identity is required.", nameof(tradeId));
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var record = await db.TradePlaybookContexts.AsNoTracking().SingleOrDefaultAsync(c => c.TradeId == tradeId, cancellationToken);
        if (record is null) return null;
        if (record.ContractVersion != TradePlaybookContext.ContractVersion) throw new InvalidDataException("Unsupported Trade context version.");
        var definition = PlaybookPersistenceMapper.Deserialize(record.DefinitionJson);
        if (definition.TradingSetupId != record.TradingSetupId || definition.Revision != record.Revision)
            throw new InvalidDataException("Trade context identity mismatch.");
        return TradePlaybookContext.Rehydrate(tradeId, record.TradeOpenedAtUtc, definition, record.CapturedAtUtc);
    }

    public async Task<bool> TryAddAsync(TradePlaybookContext context, DateTimeOffset expectedTradeUpdatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (expectedTradeUpdatedAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("UTC required.", nameof(expectedTradeUpdatedAtUtc));
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
        await db.Database.UseTransactionAsync(transaction, cancellationToken);
        if (await db.TradePlaybookContexts.AnyAsync(c => c.TradeId == context.TradeId, cancellationToken)) return false;
        var trade = await db.Trades.AsNoTracking().SingleOrDefaultAsync(t => t.Id == context.TradeId, cancellationToken);
        var setup = await db.TradingSetups.AsNoTracking().SingleOrDefaultAsync(s => s.Id == context.Definition.TradingSetupId, cancellationToken);
        if (trade is null || setup is null || !setup.IsActive || trade.TradingSetupId != setup.Id ||
            trade.UpdatedAtUtc != expectedTradeUpdatedAtUtc || context.CapturedAtUtc < trade.UpdatedAtUtc || context.CapturedAtUtc < setup.UpdatedAtUtc)
            return false;
        var opened = await db.TradeExecutions.Where(e => e.TradeId == trade.Id).OrderBy(e => e.Sequence)
            .Select(e => (DateTimeOffset?)e.ExecutedAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (opened != context.TradeOpenedAtUtc) return false;
        // Legacy capture callers may explicitly supply an already-retained historical definition.
        // The normal Trade association workflow always previews and rechecks current content.
        var definition = await TradingSetupPlaybookStore.ReadRevisionAsync(db, setup.Id, context.Definition.Revision, cancellationToken)
            ?? await TradingSetupPlaybookStore.ReadCurrentAsync(db, setup.Id, cancellationToken);
        string json = PlaybookPersistenceMapper.Serialize(context.Definition);
        if (definition is null || PlaybookPersistenceMapper.Serialize(definition) != json) return false;
        await TradingSetupPlaybookStore.RetainCaptureAsync(db, definition, cancellationToken);
        db.TradePlaybookContexts.Add(new TradePlaybookContextRecord
        {
            TradeId = context.TradeId, ContractVersion = TradePlaybookContext.ContractVersion,
            TradingSetupId = definition.TradingSetupId, Revision = definition.Revision,
            TradeOpenedAtUtc = context.TradeOpenedAtUtc, CapturedAtUtc = context.CapturedAtUtc, DefinitionJson = json,
        });
        await db.SaveChangesAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync(CancellationToken.None);
        return true;
    }
}
