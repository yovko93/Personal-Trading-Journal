using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.Accounts;

public sealed class TradingAccountReader : ITradingAccountReader, ITradingAccountBalanceReader
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;

    public TradingAccountReader(IDbContextFactory<JournalDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);

        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<AccountListItem>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await ReadAccountsAsync(context, cancellationToken);
    }

    public async Task<IReadOnlyList<AccountListItem>> GetAllWithBalancesAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.OpenConnectionAsync(cancellationToken);
        // One coherent read snapshot across three batched queries, never one query per Account.
        await using var transaction = ((SqliteConnection)context.Database.GetDbConnection()).BeginTransaction(deferred: true);
        await context.Database.UseTransactionAsync(transaction, cancellationToken);
        var accounts = await ReadAccountsAsync(context, cancellationToken);
        var trades = await (from trade in context.Trades.AsNoTracking()
            join browse in context.TradeBrowse.AsNoTracking() on trade.Id equals browse.TradeId into projections
            from browse in projections.DefaultIfEmpty()
            select new { trade.Id, trade.TradingAccountId, trade.PricingCurrency,
                Status = browse == null ? (TradeStatus?)null : browse.Status, Gross = browse == null ? null : browse.GrossPnL })
            .ToArrayAsync(cancellationToken);
        var costs = await (from execution in context.TradeExecutions.AsNoTracking()
            join trade in context.Trades.AsNoTracking() on execution.TradeId equals trade.Id
            join browse in context.TradeBrowse.AsNoTracking() on trade.Id equals browse.TradeId
            join account in context.TradingAccounts.AsNoTracking() on trade.TradingAccountId equals account.Id
            where browse.Status == TradeStatus.Closed && trade.PricingCurrency == account.Currency
            orderby execution.TradeId, execution.Sequence, execution.Id
            select new { execution.TradeId, execution.Commission, execution.Fees }).ToArrayAsync(cancellationToken);
        var costsByTrade = costs.ToLookup(e => e.TradeId, e => new AccountBalanceCost(e.Commission, e.Fees));
        var tradesByAccount = trades.ToLookup(t => t.TradingAccountId);
        return accounts.Select(account => account with
        {
            CurrentBalance = AccountCurrentBalanceCalculator.Calculate(account.StartingBalance, account.Currency,
                tradesByAccount[account.Id].Select(t => new AccountBalanceTrade(t.Id, t.PricingCurrency, t.Status,
                    t.Gross, costsByTrade[t.Id].ToArray())), cancellationToken)
        }).ToArray();
    }

    private static Task<List<AccountListItem>> ReadAccountsAsync(JournalDbContext context, CancellationToken cancellationToken) =>
        context.TradingAccounts
            .AsNoTracking()
            .OrderBy(record => EF.Functions.Collate(record.Name, "NOCASE"))
            .ThenBy(record => record.Id)
            .Select(record => new AccountListItem(
                record.Id,
                record.Name,
                record.AccountType,
                record.ProviderName,
                record.ExternalAccountId,
                record.Currency,
                record.StartingBalance,
                record.IsActive))
            .ToListAsync(cancellationToken);
    public async Task<TradingAccountDetails?> GetByIdAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.TradingAccounts
            .AsNoTracking()
            .Where(record => record.Id == accountId)
            .Select(record => new TradingAccountDetails(
                record.Id,
                record.Name,
                record.AccountType,
                record.ProviderName,
                record.ExternalAccountId,
                record.Currency,
                record.StartingBalance,
                record.IsActive,
                record.CreatedAtUtc,
                record.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
