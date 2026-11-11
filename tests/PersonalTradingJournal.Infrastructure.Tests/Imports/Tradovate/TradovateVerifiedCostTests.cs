using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.Common.Time;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Domain.Accounts;
using PersonalTradingJournal.Infrastructure.Imports.Tradovate;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;
using PersonalTradingJournal.Infrastructure.Tests.Persistence;

namespace PersonalTradingJournal.Infrastructure.Tests.Imports.Tradovate;

public sealed class TradovateVerifiedCostTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private const string Performance = "symbol,_priceFormat,_priceFormatType,_tickSize,buyFillId,sellFillId,qty,buyPrice,sellPrice,pnl,boughtTimestamp,soldTimestamp,duration\n" +
        "MNQZ6,-2,0,0.25,B1,CROSS,1,100,101,$2.00,11/02/2026 15:00:00,11/02/2026 15:01:00,1min\n" +
        "MNQZ6,-2,0,0.25,B2,CROSS,1,100,101,$2.00,11/02/2026 15:02:00,11/02/2026 15:01:00,1min";
    private const string Fills = "_id,Fill ID,Account,Contract,B/S,Quantity,Price,Timestamp,_timestamp,_tradeDate,commission,_active\n" +
        "B1,B1,SIM-COST,MNQZ6, Buy,1,100,11/02/2026 15:00:00,2026-11-02 13:00:00.123Z,2026-11-02,0.52,true\n" +
        "CROSS,CROSS,SIM-COST,MNQZ6, Sell,2,101,11/02/2026 15:01:00,2026-11-02 13:01:00.456Z,2026-11-02,0.05,true\n" +
        "B2,B2,SIM-COST,MNQZ6, Buy,1,100,11/02/2026 15:02:00,2026-11-02 13:02:00.789Z,2026-11-02,0.52,true";
    private const string Cash = "Account,Transaction ID,Timestamp,Date,Delta,Amount,Cash Change Type,Currency,Contract\n" +
        "SIM-COST,C1,11/02/2026 15:00:00,2026-11-02,-0.52,99.48, Commission,USD,MNQZ6\n" +
        "SIM-COST,C2,11/02/2026 15:01:00,2026-11-02,-0.05,99.43, Commission,USD,MNQZ6\n" +
        "SIM-COST,P1,11/02/2026 15:01:00,2026-11-02,2.00,101.43, Trade Paired,USD,MNQZ6\n" +
        "SIM-COST,C3,11/02/2026 15:02:00,2026-11-02,-0.52,100.91, Commission,USD,MNQZ6\n" +
        "SIM-COST,P2,11/02/2026 15:02:00,2026-11-02,2.00,102.91, Trade Paired,USD,MNQZ6";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReversalCostsAllocateEachFillOncePreserveGrossAndRefreshOnImportOrEnrichment(bool existing)
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var request = await Prepare(db);
        var verified = TradovateCostReconciler.Reconcile(request.Preparation, request.MatchedPairs!, request.CostSources!);
        Assert.Equal(4m, verified.Gross);
        Assert.Equal(1.09m, verified.Costs);
        Assert.Equal(2.91m, verified.Net);
        Assert.Equal(new[] { .03m, .02m }, verified.Allocations.Where(a => a.FillId == "CROSS").OrderBy(a => a.AllocationIndex).Select(a => a.Commission));
        var store = new TradovateImportStore(db.ContextFactory);
        Guid[]? oldIds = null;
        Guid[]? executionIds = null;
        if (existing)
        {
            Assert.Equal(2, (await store.ImportAsync(request with { CostSources = null })).ImportedTradeCount);
            await using var before = await db.ContextFactory.CreateDbContextAsync();
            oldIds = await before.Trades.Select(t => t.Id).ToArrayAsync();
            executionIds = await before.TradeExecutions.Select(t => t.Id).ToArrayAsync();
            Assert.All(await before.TradeBrowse.ToArrayAsync(), t => Assert.Null(t.NetPnL));
        }
        var result = await store.ImportAsync(request with { ImportedAtUtc = Now.AddMinutes(1) });
        Assert.Equal(TradovateImportStatus.Imported, result.Status);
        Assert.Equal(existing ? 0 : 2, result.ImportedTradeCount);
        Assert.Equal(existing ? 2 : 0, result.UpdatedCostTradeCount);
        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            Assert.Equal(2, await context.Trades.CountAsync());
            Assert.Equal(4, await context.TradeExecutions.CountAsync());
            var trades = await context.TradeBrowse.OrderBy(t => t.OpenedAtUtc).ToArrayAsync();
            Assert.Equal(new decimal?[] { 2m, 2m }, trades.Select(t => t.GrossPnL));
            Assert.Equal(new decimal?[] { 1.45m, 1.46m }, trades.Select(t => t.NetPnL));
            if (existing)
            {
                Assert.Equal(oldIds!.Order(), (await context.Trades.Select(t => t.Id).ToArrayAsync()).Order());
                Assert.Equal(executionIds!.Order(), (await context.TradeExecutions.Select(t => t.Id).ToArrayAsync()).Order());
            }
        }
        var metrics = Assert.Single((await db.ServiceProvider.GetRequiredService<IDashboardAnalyticsReader>()
            .GetAsync(new DashboardAnalyticsQuery(request.Preparation.AccountSnapshot!.TradingAccountId))).Currencies).Metrics;
        Assert.Equal(2.91m, metrics.Net.Total);
        Assert.False(metrics.EffectiveNet.IsEstimated);
        var balance = Assert.Single(await db.ServiceProvider.GetRequiredService<ITradingAccountBalanceReader>().GetAllWithBalancesAsync()).CurrentBalance!;
        Assert.Equal(2.91m, balance.Value);
        Assert.False(balance.IsEstimated);
        Assert.Equal(TradovateImportStatus.NoChanges, (await store.ImportAsync(request)).Status);
        Assert.Equal(0, (await store.ImportAsync(request)).UpdatedCostTradeCount);
    }

    [Theory]
    [InlineData("missing-fill")]
    [InlineData("duplicate-fill")]
    [InlineData("extra-fill")]
    [InlineData("partial-fill")]
    [InlineData("fill-account")]
    [InlineData("cash-account")]
    [InlineData("currency")]
    [InlineData("period")]
    [InlineData("fee")]
    [InlineData("gross")]
    [InlineData("cash-balance")]
    [InlineData("cash-duplicate")]
    [InlineData("cash-missing")]
    [InlineData("transfer")]
    [InlineData("timestamp")]
    public async Task AmbiguousOrIncompleteEvidenceBlocksWholeWrite(string fault)
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var request = await Prepare(db);
        var f = request.CostSources!.Fills.ToList();
        var c = request.CostSources.Cash.ToList();
        switch (fault)
        {
            case "missing-fill": f.RemoveAt(0); break;
            case "duplicate-fill": f.Add(f[0]); break;
            case "extra-fill": f.Add(f[0] with { Id = "EXTRA" }); break;
            case "partial-fill": f[1] = f[1] with { Quantity = 3m }; break;
            case "fill-account": f[0] = f[0] with { Account = "OTHER" }; break;
            case "cash-account": c[0] = c[0] with { Account = "OTHER" }; break;
            case "currency": c[0] = c[0] with { Currency = "EUR" }; break;
            case "period": c[0] = c[0] with { TradingDate = c[0].TradingDate.AddDays(1) }; break;
            case "fee": c[0] = c[0] with { Delta = -.51m }; break;
            case "gross": c[2] = c[2] with { Delta = 3m }; break;
            case "cash-balance": c[3] = c[3] with { Balance = 0m }; break;
            case "cash-duplicate": c.Add(c[0]); break;
            case "cash-missing": c.RemoveAt(2); break;
            case "transfer": c[0] = c[0] with { Type = "Deposit" }; break;
            case "timestamp": f[0] = f[0] with { UtcTimestamp = f[0].UtcTimestamp.AddHours(1) }; break;
        }
        var result = await new TradovateImportStore(db.ContextFactory).ImportAsync(request with { CostSources = new(f, c) });
        Assert.Equal(TradovateImportStatus.Blocked, result.Status);
        Assert.Equal("COST_RECONCILIATION_BLOCKED", result.ConflictCode);
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await context.Trades.ToArrayAsync());
        Assert.Empty(await context.Instruments.ToArrayAsync());
    }

    [Theory]
    [InlineData("commission")]
    [InlineData("fee")]
    [InlineData("price")]
    [InlineData("account")]
    [InlineData("cleared-costs")]
    public async Task ExistingManualEditsAreNotOverwrittenOrPartiallyEnriched(string edit)
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var request = await Prepare(db);
        var store = new TradovateImportStore(db.ContextFactory);
        await store.ImportAsync(request with { CostSources = null });
        await using (var context = await db.ContextFactory.CreateDbContextAsync())
        {
            var execution = await context.TradeExecutions.OrderBy(e => e.ExecutedAtUtc).LastAsync();
            if (edit == "commission") execution.Commission = 99m;
            if (edit == "fee") execution.Fees = 0m; // even known zero is a manual fact
            if (edit == "price") execution.Price += 1;
            if (edit == "account") (await context.TradingAccounts.SingleAsync()).ExternalAccountId = "CHANGED";
            if (edit == "cleared-costs") (await context.Trades.SingleAsync(t => t.Id == execution.TradeId)).UpdatedAtUtc = Now.AddMinutes(1);
            await context.SaveChangesAsync();
        }
        Assert.Equal(TradovateImportStatus.Blocked, (await store.ImportAsync(request)).Status);
        await using var after = await db.ContextFactory.CreateDbContextAsync();
        Assert.Equal(edit == "commission" ? 1 : 0, await after.TradeExecutions.CountAsync(e => e.Commission != null));
        Assert.All(await after.TradeBrowse.ToArrayAsync(), b => Assert.Null(b.NetPnL));
    }

    [Theory]
    [InlineData("0.05", "0.03", "0.02")]
    [InlineData("0.01", "0.01", "0.00")]
    [InlineData("0.00", "0.00", "0.00")]
    public async Task RemainderIsDeterministicWhenSourceOrderChanges(string amount, string first, string second)
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var request = await Prepare(db);
        var f = request.CostSources!.Fills.Select(f => f.Id == "CROSS" ? f with { Commission = decimal.Parse(amount, CultureInfo.InvariantCulture) } : f).Reverse().ToArray();
        decimal change = .05m - decimal.Parse(amount, CultureInfo.InvariantCulture);
        var c = request.CostSources.Cash.Select((c, i) => i == 0 ? c : c with { Balance = c.Balance + change, Delta = i == 1 ? -f.Single(f => f.Id == "CROSS").Commission : c.Delta }).ToArray();
        var verified = TradovateCostReconciler.Reconcile(request.Preparation, request.MatchedPairs!, new(f, c));
        Assert.Equal(new[] { decimal.Parse(first, CultureInfo.InvariantCulture), decimal.Parse(second, CultureInfo.InvariantCulture) },
            verified.Allocations.Where(a => a.FillId == "CROSS").OrderBy(a => a.AllocationIndex).Select(a => a.Commission));
        Assert.Equal(f.Sum(f => f.Commission), verified.Allocations.Sum(a => a.Commission));
    }

    [Fact]
    public async Task UnequalMatchedQuantitiesReceiveProportionalCentsAndPostingLagIsNotAFillJoin()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var request = await Prepare(db,
            Performance.Replace("B1,CROSS,1,100,101,$2.00", "B1,CROSS,2,100,101,$4.00"),
            Fills.Replace(" Buy,1,100,11/02/2026 15:00:00", " Buy,2,100,11/02/2026 15:00:00")
                .Replace(" Sell,2,101", " Sell,3,101").Replace("0.05,true", "0.04,true"),
            Cash.Replace("15:00:00,2026-11-02", "15:00:01,2026-11-02")
                .Replace("-0.05,99.43", "-0.04,99.44").Replace("2.00,101.43", "4.00,103.44")
                .Replace("-0.52,100.91", "-0.52,102.92").Replace("2.00,102.91", "2.00,104.92"));
        var costs = TradovateCostReconciler.Reconcile(request.Preparation, request.MatchedPairs!, request.CostSources!);
        Assert.Equal(new[] { .03m, .01m }, costs.Allocations.Where(a => a.FillId == "CROSS").OrderBy(a => a.AllocationIndex).Select(a => a.Commission));
        Assert.Equal(6m, costs.Gross);
        Assert.Equal(4.92m, costs.Net);
    }

    [Theory]
    [InlineData("header")]
    [InlineData("numeric")]
    [InlineData("inactive")]
    [InlineData("identity")]
    public async Task MalformedSupportingExportIsNotDefaultedToZero(string fault)
    {
        string csv = fault switch
        {
            "header" => Fills.Replace("commission", "missing"),
            "numeric" => Fills.Replace("0.05,true", "unknown,true"),
            "inactive" => Fills.Replace("true", "false"),
            _ => Fills.Replace("B1,B1,", "DIFFERENT,B1,"),
        };
        using var stream = Stream(csv);
        await Assert.ThrowsAsync<TradovateCostSourceException>(() => new TradovateCostCsvParser().ParseFillsAsync(stream));
    }

    private static async Task<TradovateImportRequest> Prepare(ReaderTestDatabase db,
        string performanceCsv = Performance, string fillsCsv = Fills, string cashCsv = Cash)
    {
        var account = new TradingAccount("Synthetic", TradingAccountType.Personal, "Tradovate", "SIM-COST", "USD", 0m, Now);
        await db.ServiceProvider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
        using var performance = Stream(performanceCsv);
        var parse = await new TradovateCsvParser().ParseAsync(performance);
        var reconstruction = new TradovateExecutionReconstructor().Reconstruct(parse);
        Assert.True(reconstruction.IsEligibleForAutomaticImport);
        var resolution = await new TradovateInstrumentResolver(db.ServiceProvider.GetRequiredService<IInstrumentReader>()).ResolveAsync(reconstruction);
        var preparation = await new TradovateImportPreparationService(db.ServiceProvider.GetRequiredService<ITradingAccountReader>())
            .PrepareAsync(reconstruction, resolution, account.Id);
        using var fills = Stream(fillsCsv);
        using var cash = Stream(cashCsv);
        var parser = new TradovateCostCsvParser();
        return new(preparation, Now, new(await parser.ParseFillsAsync(fills), await parser.ParseCashAsync(cash)), reconstruction.MatchedPairs);
    }

    private static MemoryStream Stream(string csv) => new(Encoding.UTF8.GetBytes(csv));
}
