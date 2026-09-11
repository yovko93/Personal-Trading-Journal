using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Common.Storage;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Infrastructure.Accounts;
using PersonalTradingJournal.Infrastructure.Instruments;
using PersonalTradingJournal.Infrastructure.Persistence.Initialization;
using PersonalTradingJournal.Infrastructure.Trades;

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
        services.AddTransient<JournalDatabaseInitializer>();
        services.AddTransient<ITradingAccountReader, TradingAccountReader>();
        services.AddTransient<ITradingAccountStore, TradingAccountStore>();
        services.AddTransient<IInstrumentReader, InstrumentReader>();
        services.AddTransient<IInstrumentStore, InstrumentStore>();
        services.AddTransient<
            IManualTradeReferenceDataReader,
            ManualTradeReferenceDataReader>();
        services.AddTransient<ITradeStore, TradeStore>();
        services.AddTransient<ITradeListReader, TradeListReader>();
        services.AddTransient<ITradeDetailReader, TradeDetailReader>();

        return services;
    }
}
