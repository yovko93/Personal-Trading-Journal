using Microsoft.EntityFrameworkCore;

namespace PersonalTradingJournal.Infrastructure.Persistence;

public sealed class JournalDbContext : DbContext
{
    public JournalDbContext(DbContextOptions<JournalDbContext> options)
        : base(options)
    {
    }
}
