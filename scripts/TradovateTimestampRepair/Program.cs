using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Infrastructure.Imports.Tradovate;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Storage;

var json = new JsonSerializerOptions { WriteIndented = true, NumberHandling = JsonNumberHandling.AllowReadingFromString };
try
{
    bool apply = args.Length == 8 && args[0] == "apply";
    if (!apply && !(args.Length == 5 && args[0] == "dry-run"))
        throw new ArgumentException("dry-run <database> <baseline.json> <Fills.csv> <report.json> OR apply <database> <baseline.json> <Fills.csv> <approved-report.json> <report-sha256> <backup.ptjbackup> <result.json>");
    string database = Path.GetFullPath(args[1]);
    if (!File.Exists(database)) throw new FileNotFoundException("Database must already exist.");
    var scope = JsonSerializer.Deserialize<TradovateTimestampRepairScope>(await File.ReadAllBytesAsync(args[2]), json)
        ?? throw new InvalidDataException("Baseline is empty.");
    byte[] source = await File.ReadAllBytesAsync(args[3]);
    var options = new DbContextOptionsBuilder<JournalDbContext>().UseSqlite(new SqliteConnectionStringBuilder
    {
        DataSource = database, Mode = apply ? SqliteOpenMode.ReadWrite : SqliteOpenMode.ReadOnly,
        Pooling = false, ForeignKeys = true
    }.ToString()).Options;
    var service = new TradovateTimestampRepairService(new RepairContextFactory(options));
    string output = Path.GetFullPath(apply ? args[7] : args[4]);
    // Refuse output aliases with any input, and reserve it before a possible write.
    await using var resultFile = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    if (!apply)
    {
        var plan = await service.PreviewAsync(scope, source);
        await JsonSerializer.SerializeAsync(resultFile, plan, json);
        Console.WriteLine($"Dry run. {plan.Changes.Count} field changes, {plan.AffectedTrades} Trades, {plan.UnchangedExecutions} unaffected executions. Already applied = {plan.AlreadyApplied}.");
    }
    else
    {
        byte[] approvedBytes = await File.ReadAllBytesAsync(args[4]);
        string hash = Convert.ToHexStringLower(SHA256.HashData(approvedBytes));
        if (!hash.Equals(args[5], StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Approved report hash differs.");
        var approved = JsonSerializer.Deserialize<TradovateTimestampRepairPlan>(approvedBytes, json)
            ?? throw new InvalidDataException("Approved report is empty.");
        string parent = Path.GetDirectoryName(database)!;
        var paths = new LocalApplicationPaths(Path.GetDirectoryName(parent)!);
        if (!Path.GetFullPath(paths.DatabasePath).Equals(database, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Apply requires the normal PersonalTradingJournal directory layout for complete backup.");
        using var services = new ServiceCollection().AddPersistence(paths).BuildServiceProvider();
        var result = await service.ApplyAsync(scope, source, approved,
            services.GetRequiredService<IBackupArchiveService>(), Path.GetFullPath(args[6]));
        await JsonSerializer.SerializeAsync(resultFile, result, json);
        Console.WriteLine($"Repair committed. {result.ChangedExecutions} executions. Already applied = {result.AlreadyApplied}.");
    }
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}

sealed class RepairContextFactory(DbContextOptions<JournalDbContext> options) : IDbContextFactory<JournalDbContext>
{
    public JournalDbContext CreateDbContext() => new(options);
}
