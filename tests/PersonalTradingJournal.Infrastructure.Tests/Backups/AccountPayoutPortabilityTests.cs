using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Infrastructure.Backups;
using PersonalTradingJournal.Infrastructure.Persistence;
using Fixture = PersonalTradingJournal.Infrastructure.Tests.Backups.BackupArchiveTests.Fixture;

namespace PersonalTradingJournal.Infrastructure.Tests.Backups;

public sealed class AccountPayoutPortabilityTests
{
    [Fact]
    public async Task BackupRestoreAndPortableExportPreserveAmountsCurrencyOwnersAndCreationTimes()
    {
        await using var source = await Fixture.Create();
        var account = new TradingAccount("Synthetic payout", TradingAccountType.Personal, null, null, "EUR", 1000, DateTimeOffset.UtcNow);
        await source.Services.GetRequiredService<ITradingAccountStore>().AddAsync(account);
        var payouts = source.Services.GetRequiredService<IAccountPayoutStore>();
        await payouts.AddAsync(account.Id, "EUR", 12.34m); await payouts.AddAsync(account.Id, "EUR", 56.78m);
        var expected = await payouts.GetAsync(account.Id);
        Assert.True((await source.Service().CreateAsync(source.Output)).Succeeded);
        var preflight = await new RestorePreflightService().InspectAsync(source.Output, source.Root);
        Assert.True(preflight.Validated, preflight.ToString());
        string export = Path.Combine(source.Root, "payout-export");
        Assert.True((await source.Services.GetRequiredService<IPortableExportService>().CreateAsync(export)).Succeeded);
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(export, "journal.json")));
        var rows = json.RootElement.GetProperty("tables").GetProperty("accountPayouts");
        Assert.Equal(2, rows.GetArrayLength());
        foreach (var row in rows.EnumerateArray())
        {
            var payout = expected.Single(p => p.Id == row.GetProperty("id").GetGuid());
            Assert.Equal(payout.AccountId, row.GetProperty("accountId").GetGuid());
            Assert.Equal(payout.Amount, decimal.Parse(row.GetProperty("amount").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(payout.Currency, row.GetProperty("currency").GetString());
            Assert.Equal(payout.CreatedAtUtc, DateTimeOffset.Parse(row.GetProperty("createdAtUtc").GetString()!));
        }
        Assert.True(File.Exists(Path.Combine(export, "accountPayouts.csv")));
        await using var target = await Fixture.Create();
        await target.Services.DisposeAsync(); JournalDataSession.ClearOwnedPools(target.Paths);
        var restored = await new JournalRestoreService(target.Paths).RestoreAsync(new(source.Output, preflight.Summary!.ArchiveSha256));
        Assert.Equal(JournalRestoreStatus.Restored, restored.Status);
        await using var fresh = new ServiceCollection().AddPersistence(target.Paths).BuildServiceProvider();
        Assert.Equal(expected, await fresh.GetRequiredService<IAccountPayoutStore>().GetAsync(account.Id));
        Assert.Equal(930.88m, Assert.Single(await fresh.GetRequiredService<ITradingAccountBalanceReader>().GetAllWithBalancesAsync()).CurrentBalance!.Value);
    }
}
