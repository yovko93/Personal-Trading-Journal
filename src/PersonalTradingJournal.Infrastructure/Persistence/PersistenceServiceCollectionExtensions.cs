using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Common.Storage;

namespace PersonalTradingJournal.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IApplicationPaths applicationPaths)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(applicationPaths);

        string connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = applicationPaths.DatabasePath,
            ForeignKeys = true,
        }.ToString();

        services.AddDbContextFactory<JournalDbContext>(options =>
            options.UseSqlite(connectionString));

        return services;
    }
}
