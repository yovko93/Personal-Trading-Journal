using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Common.Time;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Application.Trades;
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
            var performanceOnly = await Prepare(db, useFillsTimes: false, accountId: request.Preparation.AccountSnapshot!.TradingAccountId);
            Assert.Equal(2, (await store.ImportAsync(performanceOnly with { CostSources = null })).ImportedTradeCount);
            await using var before = await db.ContextFactory.CreateDbContextAsync();
            oldIds = await before.Trades.Select(t => t.Id).ToArrayAsync();
            executionIds = await before.TradeExecutions.Select(t => t.Id).ToArrayAsync();
            Assert.All(await before.TradeBrowse.ToArrayAsync(), t => Assert.Null(t.NetPnL));
            var descriptions = db.ServiceProvider.GetRequiredService<IImportedTradeDescriptionStore>();
            var details = db.ServiceProvider.GetRequiredService<ITradeDetailReader>();
            foreach (Guid id in oldIds)
            {
                var original = (await details.GetByIdAsync(id))!;
                Assert.True(original.IsImported);
                Assert.Equal("Imported annotation", await descriptions.SaveDescriptionAsync(id, "  Imported annotation  "));
                var annotated = (await details.GetByIdAsync(id))!;
                Assert.Equal(original.Executions, annotated.Executions);
                Assert.Equal(original with { Description = "Imported annotation", Executions = annotated.Executions }, annotated);
            }
            Assert.Equal(TradovateImportStatus.NoChanges, (await store.ImportAsync(performanceOnly with { CostSources = null })).Status);
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
        if (existing)
        {
            await using var context = await db.ContextFactory.CreateDbContextAsync();
            Assert.All(await context.Trades.ToArrayAsync(), trade => Assert.Equal("Imported annotation", trade.Description));
            var descriptions = db.ServiceProvider.GetRequiredService<IImportedTradeDescriptionStore>();
            Assert.Null(await descriptions.SaveDescriptionAsync(oldIds![0], " "));
            Assert.Equal(TradovateImportStatus.NoChanges, (await store.ImportAsync(request)).Status);
            Assert.Null(await context.Trades.Where(t => t.Id == oldIds[0]).Select(t => t.Description).SingleAsync());
        }
    }

    [Theory]
    [InlineData("account")]
    [InlineData("currency")]
    [InlineData("period")]
    [InlineData("balance")]
    public async Task AdjustmentMustBelongToTheVerifiedStatement(string fault)
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var request = await Prepare(db);
        var cash = request.CostSources!.Cash;
        var adjustment = cash[^1] with
        {
            Id = "A1", Type = "Manual Adjustment", Symbol = "", Delta = -7m, Balance = cash[^1].Balance - 7m
        };
        adjustment = fault switch
        {
            "account" => adjustment with { Account = "OTHER" },
            "currency" => adjustment with { Currency = "EUR" },
            "period" => adjustment with { TradingDate = adjustment.TradingDate.AddDays(1) },
            "balance" => adjustment with { Balance = cash[^1].Balance },
            _ => throw new ArgumentOutOfRangeException(nameof(fault))
        };
        var result = await new TradovateImportStore(db.ContextFactory).ImportAsync(request with
        {
            CostSources = new(request.CostSources.Fills, cash.Append(adjustment))
        });
        Assert.Equal(TradovateImportStatus.Blocked, result.Status);
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await context.Trades.ToArrayAsync());
    }

    [Theory]
    [InlineData("Commission", "", "-0.52", "requires a Contract")]
    [InlineData("Trade Paired", "", "2", "requires a Contract")]
    [InlineData("Manual Adjustment", "MNQZ6", "-7", "empty Contract")]
    [InlineData("Manual Adjustment", "", "-0.001", "whole-cent Delta")]
    [InlineData("Manual Adjustment", "", "bad", "Invalid")]
    [InlineData("Transfer", "", "7", "Unsupported Cash History movement 'Transfer'")]
    public async Task InvalidCashRowReportsItsFailedRule(string type, string contract, string delta, string expected)
    {
        using var stream = Stream(Cash + $"\nSIM-COST,A1,11/02/2026 15:03:00,2026-11-02,{delta},95.91,{type},USD,{contract}");
        var error = await Assert.ThrowsAsync<TradovateCostSourceException>(() => new TradovateCostCsvParser().ParseCashAsync(stream));
        Assert.Contains(expected, error.Message);
    }

    private const string IsolatedFills =
        "\nX1,X1,SIM-COST,MNQZ6, Buy,1,99,10/30/2026 15:00:00,2026-10-30 13:00:00Z,2026-10-30,0.70,true" +
        "\nX2,X2,SIM-COST,MNQZ6, Sell,1,101,10/30/2026 15:01:00,2026-10-30 13:01:00Z,2026-10-30,0.80,true";

    [Fact]
    public async Task VerifiedImportExcludesIsolatedFlatFillsFromCostsAndPersistence()
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var request = await Prepare(db, fillsCsv: Fills + IsolatedFills);
        var costs = TradovateCostReconciler.Reconcile(request.Preparation, request.MatchedPairs!, request.CostSources!);
        Assert.Equal(new TradovateExcludedFillsDay(new(2026, 10, 30), 2, 1.50m), Assert.Single(costs.ExcludedFills));
        Assert.Equal(new DateOnly(2026, 11, 2), costs.Start);
        Assert.Equal(costs.Start, costs.End);
        Assert.Equal(4m, costs.Gross);
        Assert.Equal(1.09m, costs.Costs);
        Assert.Equal(2.91m, costs.Net);

        var store = new TradovateImportStore(db.ContextFactory);
        Assert.Equal(2, (await store.ImportAsync(request)).ImportedTradeCount);
        Assert.Equal(TradovateImportStatus.NoChanges, (await store.ImportAsync(request)).Status);
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        var identities = await context.TradovateImportedExecutions.Select(e => e.ExternalExecutionId).ToArrayAsync();
        Assert.Equal(new[] { "B1", "B2", "CROSS", "CROSS" }, identities.Order(StringComparer.Ordinal));
        Assert.Equal(1.09m, (await context.TradeExecutions.ToArrayAsync()).Sum(e => e.Commission));
    }

    [Theory]
    [InlineData("cash-on-excluded-day", "date with Performance or Cash History")]
    [InlineData("cash-display-day", "date with Performance or Cash History")]
    [InlineData("fill-display-day", "date with Performance or Cash History")]
    [InlineData("unexplained-cash", "amounts and counts")]
    [InlineData("other-account", "mismatched Accounts")]
    [InlineData("other-currency", "matching USD")]
    [InlineData("missing-and-extra", "1 missing, 2 extra")]
    [InlineData("imbalance", "nonzero position")]
    [InlineData("overlap", "overlaps a reported position")]
    public async Task AnExclusionCannotHideAmbiguousOrUnreconciledActivity(string fault, string expected)
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var request = fault == "overlap"
            ? await Prepare(db,
                CrossSeasonPerformance.Replace("10/24/2026 00:00:00", "10/26/2026 00:00:00"),
                CrossSeasonFills.Replace("10/24/2026 00:00:00,2026-10-23 22:", "10/26/2026 00:00:00,2026-10-25 22:"),
                CrossSeasonCash.Replace("10/24/2026 00:00:01", "10/26/2026 00:00:01"))
            : await Prepare(db);
        using var stream = Stream(Fills + IsolatedFills);
        var extra = (await new TradovateCostCsvParser().ParseFillsAsync(stream)).TakeLast(2).ToArray();
        var fills = request.CostSources!.Fills.ToList();
        var cash = request.CostSources.Cash.ToList();
        switch (fault)
        {
            case "cash-on-excluded-day":
                cash[0] = cash[0] with { TradingDate = extra[0].TradingDate };
                break;
            case "cash-display-day":
                cash[0] = cash[0] with { LocalTimestamp = extra[0].LocalTimestamp };
                break;
            case "fill-display-day":
                extra[0] = extra[0] with { LocalTimestamp = fills[0].LocalTimestamp, UtcTimestamp = fills[0].UtcTimestamp };
                break;
            case "unexplained-cash":
                cash.Add(cash[^1] with { Id = "C-EXTRA", Type = "Commission", Delta = -.70m, Balance = cash[^1].Balance - .70m });
                break;
            case "other-account":
                extra[0] = extra[0] with { Account = "OTHER" };
                break;
            case "other-currency":
                cash[0] = cash[0] with { Currency = "EUR" };
                break;
            case "missing-and-extra":
                fills.RemoveAt(0);
                break;
            case "imbalance":
                extra[0] = extra[0] with { Quantity = 2 };
                break;
            case "overlap":
                extra = extra.Select(f => f with { TradingDate = f.TradingDate.AddDays(-5),
                    LocalTimestamp = f.LocalTimestamp.AddDays(-5), UtcTimestamp = f.UtcTimestamp.AddDays(-5) }).ToArray();
                break;
        }

        var result = await new TradovateImportStore(db.ContextFactory)
            .ImportAsync(request with { CostSources = new(fills.Concat(extra), cash) });
        Assert.Equal(TradovateImportStatus.Blocked, result.Status);
        Assert.Contains(expected, result.Message);
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        Assert.Empty(await context.Trades.ToArrayAsync());
        Assert.Empty(await context.TradovateImportedExecutions.ToArrayAsync());
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

    private const string CrossSeasonPerformance =
        "symbol,_priceFormat,_priceFormatType,_tickSize,buyFillId,sellFillId,qty,buyPrice,sellPrice,pnl,boughtTimestamp,soldTimestamp,duration\n" +
        "MNQZ6,-2,0,0.25,B1,S1,1,100,101,$2.00,10/23/2026 23:59:59,10/24/2026 00:00:00,1sec\n" +
        "MNQZ6,-2,0,0.25,B2,S2,1,100,101,$2.00,10/26/2026 23:59:58,10/26/2026 23:59:59,1sec";
    private const string CrossSeasonFills =
        "_id,Fill ID,Account,Contract,B/S,Quantity,Price,Timestamp,_timestamp,_tradeDate,commission,_active\n" +
        "B1,B1,SIM-COST,MNQZ6,Buy,1,100,10/23/2026 23:59:59,2026-10-23 21:59:59.123Z,2026-10-23,0.52,true\n" +
        "S1,S1,SIM-COST,MNQZ6,Sell,1,101,10/24/2026 00:00:00,2026-10-23 22:00:00.9999999Z,2026-10-26,0.52,true\n" +
        "B2,B2,SIM-COST,MNQZ6,Buy,1,100,10/26/2026 23:59:58,2026-10-26 21:59:58.345Z,2026-10-26,0.52,true\n" +
        "S2,S2,SIM-COST,MNQZ6,Sell,1,101,10/26/2026 23:59:59,2026-10-26 21:59:59.456Z,2026-10-26,0.52,true";
    private const string CrossSeasonCash =
        "Account,Transaction ID,Timestamp,Date,Delta,Amount,Cash Change Type,Currency,Contract\n" +
        "SIM-COST,C1,10/23/2026 23:59:59,2026-10-23,-0.52,99.48,Commission,USD,MNQZ6\n" +
        "SIM-COST,C2,10/24/2026 00:00:01,2026-10-26,-0.52,98.96,Commission,USD,MNQZ6\n" +
        "SIM-COST,P1,10/24/2026 00:00:01,2026-10-26,2.00,100.96,Trade Paired,USD,MNQZ6\n" +
        "SIM-COST,C3,10/26/2026 23:59:58,2026-10-26,-0.52,100.44,Commission,USD,MNQZ6\n" +
        "SIM-COST,C4,10/26/2026 23:59:59,2026-10-26,-0.52,99.92,Commission,USD,MNQZ6\n" +
        "SIM-COST,P2,10/26/2026 23:59:59,2026-10-26,2.00,101.92,Trade Paired,USD,MNQZ6";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FixedDisplayClockPersistsMatchedUtcAndCalendarUsesEachNewYorkClose(bool singleSeason)
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (performance, fills, cash) = FixedClockExports(singleSeason);
        var request = await Prepare(db, performance, fills, cash);
        var candidates = request.Preparation.PreparedCandidates.OrderBy(c => c.OpenedAtUtc).ToArray();
        int month = singleSeason ? 9 : 10;
        int secondDay = singleSeason ? 25 : 26;
        var firstClose = new DateTimeOffset(2026, month, 23, 22, 0, 0, TimeSpan.Zero).AddTicks(9_999_999);
        var secondClose = new DateTimeOffset(2026, month, secondDay, 21, 59, 59, TimeSpan.Zero).AddMilliseconds(456);
        Assert.Equal(firstClose, candidates[0].ClosedAtUtc);
        Assert.Equal(secondClose, candidates[1].ClosedAtUtc);
        Assert.Equal(new TimeSpan(18, 0, 0).Add(TimeSpan.FromTicks(9_999_999)), candidates[0].ClosedAtNewYork!.Value.TimeOfDay);
        Assert.Equal(new TimeSpan(17, 59, 59).Add(TimeSpan.FromMilliseconds(456)), candidates[1].ClosedAtNewYork!.Value.TimeOfDay);
        Assert.All(request.Preparation.PreparedExecutions, execution =>
            Assert.Equal(request.CostSources!.Fills.Single(f => f.Id == execution.ExternalFillId).UtcTimestamp, execution.ExecutedAtUtc));

        var costs = TradovateCostReconciler.Reconcile(request.Preparation, request.MatchedPairs!, request.CostSources!);
        Assert.True(costs.UsesFixedOffsetDisplay);
        Assert.Equal(1.92m, costs.Net);
        var store = new TradovateImportStore(db.ContextFactory);
        Assert.Null(await store.FindVerifiedImportConflictAsync(request.Preparation));
        Assert.Equal(2, (await store.ImportAsync(request)).ImportedTradeCount);
        Assert.Null(await store.FindVerifiedImportConflictAsync(request.Preparation));
        Assert.Equal(TradovateImportStatus.NoChanges, (await store.ImportAsync(request)).Status);

        Guid accountId = request.Preparation.AccountSnapshot!.TradingAccountId;
        var calendar = db.ServiceProvider.GetRequiredService<ITradingCalendarDayReader>();
        Assert.Empty((await calendar.GetAsync(TradingCalendarDayQuery.ForTradingDate(new(2026, month, 23), accountId))).Trades);
        foreach (var (day, close) in new[] { (24, firstClose), (secondDay, secondClose) })
        {
            var result = await calendar.GetAsync(TradingCalendarDayQuery.ForTradingDate(new(2026, month, day), accountId));
            Assert.Equal(close, Assert.Single(result.Trades).ClosedAtUtc);
            Assert.Equal(.96m, Assert.Single(result.Currencies).Metrics.EffectiveNet.Total);
        }
        Assert.Empty((await calendar.GetAsync(TradingCalendarDayQuery.ForTradingDate(new(2026, month, secondDay + 1), accountId))).Trades);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShiftedPerformanceOnlyImportBlocksVerifiedPreviewAndWriteWithoutRewriting(bool singleSeason)
    {
        await using var db = await ReaderTestDatabase.CreateAsync();
        var (performance, fills, cash) = FixedClockExports(singleSeason);
        var original = await Prepare(db, performance, fills, cash, useFillsTimes: false);
        var store = new TradovateImportStore(db.ContextFactory);
        Assert.Equal(2, (await store.ImportAsync(original with { CostSources = null })).ImportedTradeCount);
        Guid accountId = original.Preparation.AccountSnapshot!.TradingAccountId;
        var details = db.ServiceProvider.GetRequiredService<ITradeDetailReader>();
        await using var context = await db.ContextFactory.CreateDbContextAsync();
        var originalTrades = await context.TradeBrowse.AsNoTracking().OrderBy(t => t.OpenedAtUtc).ToArrayAsync();
        var descriptions = db.ServiceProvider.GetRequiredService<IImportedTradeDescriptionStore>();
        await descriptions.SaveDescriptionAsync(originalTrades[0].TradeId, "Manual note");

        var verified = await Prepare(db, performance, fills, cash, accountId: accountId);
        string? conflict = await store.FindVerifiedImportConflictAsync(verified.Preparation);
        Assert.Contains("different timestamp for the same fill ID", conflict);
        var result = await store.ImportAsync(verified);
        Assert.Equal(TradovateImportStatus.Blocked, result.Status);
        Assert.Equal(conflict, result.Message);
        var after = await context.TradeBrowse.AsNoTracking().OrderBy(t => t.OpenedAtUtc).ToArrayAsync();
        Assert.Equal(originalTrades.Select(t => (t.TradeId, t.ClosedAtUtc, t.NetPnL)),
            after.Select(t => (t.TradeId, t.ClosedAtUtc, t.NetPnL)));
        Assert.Equal("Manual note", (await details.GetByIdAsync(originalTrades[0].TradeId))!.Description);
        Assert.All(await context.TradeExecutions.ToArrayAsync(), execution => Assert.Null(execution.Commission));
    }

    internal static (string Performance, string Fills, string Cash) FixedClockExports(bool singleSeason)
    {
        static string InSeptember(string csv) => csv.Replace("10/26/2026", "09/25/2026")
            .Replace("2026-10-26", "2026-09-25").Replace("10/23/2026", "09/23/2026")
            .Replace("10/24/2026", "09/24/2026").Replace("2026-10-23", "2026-09-23");
        return singleSeason
            ? (InSeptember(CrossSeasonPerformance), InSeptember(CrossSeasonFills), InSeptember(CrossSeasonCash))
            : (CrossSeasonPerformance, CrossSeasonFills, CrossSeasonCash);
    }

    internal static async Task<TradovateImportRequest> Prepare(ReaderTestDatabase db,
        string performanceCsv = Performance, string fillsCsv = Fills, string cashCsv = Cash,
        bool useFillsTimes = true, Guid? accountId = null)
    {
        if (accountId is null)
        {
            var account = new TradingAccount("Synthetic", TradingAccountType.Personal, "Tradovate", "SIM-COST", "USD", 0m, Now);
            await db.ServiceProvider.GetRequiredService<ITradingAccountStore>().AddAsync(account);
            accountId = account.Id;
        }
        using var performance = Stream(performanceCsv);
        var parse = await new TradovateCsvParser().ParseAsync(performance);
        var reconstruction = new TradovateExecutionReconstructor().Reconstruct(parse);
        Assert.True(reconstruction.IsEligibleForAutomaticImport);
        var resolution = await new TradovateInstrumentResolver(db.ServiceProvider.GetRequiredService<IInstrumentReader>()).ResolveAsync(reconstruction);
        using var fills = Stream(fillsCsv);
        using var cash = Stream(cashCsv);
        var parser = new TradovateCostCsvParser();
        var sources = new TradovateCostSources(await parser.ParseFillsAsync(fills), await parser.ParseCashAsync(cash));
        var preparation = await new TradovateImportPreparationService(db.ServiceProvider.GetRequiredService<ITradingAccountReader>())
            .PrepareAsync(reconstruction, resolution, accountId.Value, costSources: useFillsTimes ? sources : null);
        return new(preparation, Now, sources, reconstruction.MatchedPairs);
    }

    private static MemoryStream Stream(string csv) => new(Encoding.UTF8.GetBytes(csv));
}
