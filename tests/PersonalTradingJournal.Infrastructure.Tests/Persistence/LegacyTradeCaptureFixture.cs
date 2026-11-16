using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Trades;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence;

/// <summary>
/// Synthetic historical evidence only. Current application registration does not expose
/// these retired writers. Compatibility fixtures must explicitly opt in; current-schema
/// retirement and migration tests never use this helper to validate write behavior.
/// </summary>
internal static class LegacyTradeCaptureFixture
{
    internal static async Task EnableAsync(JournalDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS TradePlaybookAssociations_Retired; DROP TRIGGER IF EXISTS TradePlaybookContexts_Retired;");
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER IF NOT EXISTS TradePlaybookAssociations_NoDelete BEFORE DELETE ON TradePlaybookAssociations WHEN EXISTS (SELECT 1 FROM Trades WHERE Id = OLD.TradeId) BEGIN SELECT RAISE(ABORT, 'Trade association history is retained'); END;");
    }

    internal static async Task<TradePlaybookContextStore> ContextsAsync(IDbContextFactory<JournalDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();
        await EnableAsync(db);
        return new(factory);
    }
}
