using Microsoft.EntityFrameworkCore;

namespace PersonalTradingJournal.Infrastructure.Persistence.Initialization;

public sealed class JournalDatabaseInitializer
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;
    private readonly TradeBrowseProjectionReconciler _tradeBrowseReconciler;

    public JournalDatabaseInitializer(
        IDbContextFactory<JournalDbContext> contextFactory,
        TradeBrowseProjectionReconciler tradeBrowseReconciler)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(tradeBrowseReconciler);
        _contextFactory = contextFactory;
        _tradeBrowseReconciler = tradeBrowseReconciler;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        await context.Database.MigrateAsync(cancellationToken);
        await _tradeBrowseReconciler.ReconcileAsync(cancellationToken);
    }
}
