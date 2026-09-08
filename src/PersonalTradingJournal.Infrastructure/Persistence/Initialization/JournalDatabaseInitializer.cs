using Microsoft.EntityFrameworkCore;

namespace PersonalTradingJournal.Infrastructure.Persistence.Initialization;

public sealed class JournalDatabaseInitializer
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;

    public JournalDatabaseInitializer(
        IDbContextFactory<JournalDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        await context.Database.MigrateAsync(cancellationToken);
    }
}
