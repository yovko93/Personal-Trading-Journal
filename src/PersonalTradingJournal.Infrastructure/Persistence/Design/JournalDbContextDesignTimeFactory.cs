using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PersonalTradingJournal.Infrastructure.Persistence.Design;

public sealed class JournalDbContextDesignTimeFactory :
    IDesignTimeDbContextFactory<JournalDbContext>
{
    public JournalDbContext CreateDbContext(string[] args)
    {
        string connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = ":memory:",
            ForeignKeys = true,
        }.ToString();

        var options = new DbContextOptionsBuilder<JournalDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new JournalDbContext(options);
    }
}
