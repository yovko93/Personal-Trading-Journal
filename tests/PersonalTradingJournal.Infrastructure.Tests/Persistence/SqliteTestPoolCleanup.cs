using Microsoft.Data.Sqlite;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence;

internal static class SqliteTestPoolCleanup
{
    public static void ClearPersistencePools(string databasePath)
    {
        // Call after this fixture's contexts/provider have been disposed. Pool identity
        // is the exact connection string: these are the production persistence string
        // and the explicit ReadOnly variant used by the reader tests. Register another
        // owned variant here if a fixture adds one; never clear unrelated fixtures' pools.
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
        };
        ClearPool(connectionString);
        connectionString.Mode = SqliteOpenMode.ReadOnly;
        ClearPool(connectionString);
    }

    private static void ClearPool(SqliteConnectionStringBuilder connectionString)
    {
        using var connection = new SqliteConnection(connectionString.ToString());
        SqliteConnection.ClearPool(connection);
    }
}
