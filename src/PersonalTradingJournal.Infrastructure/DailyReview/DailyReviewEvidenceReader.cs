using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.Journals;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Domain.Journals;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;

namespace PersonalTradingJournal.Infrastructure.DailyReview;

public sealed class DailyReviewEvidenceReader(IDbContextFactory<JournalDbContext> contextFactory)
    : IDailyReviewEvidenceReader
{
    public async Task<DailyReviewEvidence> GetAsync(DailyReviewQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.OpenConnectionAsync(cancellationToken);
        // A deferred read transaction establishes one snapshot at the first SELECT, without
        // taking SQLite's immediate writer reservation or allowing inter-query mixed revisions.
        await using var snapshot = ((SqliteConnection)context.Database.GetDbConnection()).BeginTransaction(deferred: true);
        await using var enlisted = await context.Database.UseTransactionAsync(snapshot, cancellationToken);

        var candidates = from trade in context.Trades.AsNoTracking()
                         join browse in context.TradeBrowse.AsNoTracking() on trade.Id equals browse.TradeId into projections
                         from browse in projections.DefaultIfEmpty()
                         where (query.TradingAccountId == null || trade.TradingAccountId == query.TradingAccountId) &&
                             ((browse != null && browse.Status == TradeStatus.Closed &&
                               browse.ClosedAtUtc >= query.FromUtc && browse.ClosedAtUtc < query.BeforeUtc) ||
                              ((browse == null || browse.Status != TradeStatus.Closed || browse.ClosedAtUtc == null) &&
                               context.TradeExecutions.Any(e => e.TradeId == trade.Id &&
                                   e.ExecutedAtUtc >= query.FromUtc && e.ExecutedAtUtc < query.BeforeUtc)))
                         select new { Trade = trade, Browse = browse };
        var selected = await (from row in candidates
            join account in context.TradingAccounts.AsNoTracking() on row.Trade.TradingAccountId equals account.Id into accounts
            from account in accounts.DefaultIfEmpty()
            join instrument in context.Instruments.AsNoTracking() on row.Trade.InstrumentId equals instrument.Id into instruments
            from instrument in instruments.DefaultIfEmpty()
            join setup in context.TradingSetups.AsNoTracking() on row.Trade.TradingSetupId equals (Guid?)setup.Id into setups
            from setup in setups.DefaultIfEmpty()
            orderby row.Browse == null ? null : row.Browse.ClosedAtUtc descending, row.Trade.Id
            select new
            {
                row.Trade.Id, row.Trade.PricingCurrency, row.Trade.PricingPointValue,
                row.Trade.CreatedAtUtc, row.Trade.UpdatedAtUtc,
                Account = new DailyReviewReference(row.Trade.TradingAccountId, account == null ? null : account.Name,
                    account == null ? (bool?)null : account.IsActive),
                Instrument = new DailyReviewReference(row.Trade.InstrumentId, instrument == null ? null : instrument.Symbol,
                    instrument == null ? (bool?)null : instrument.IsActive),
                Setup = row.Trade.TradingSetupId == null ? null : new DailyReviewReference(row.Trade.TradingSetupId!.Value,
                    setup == null ? null : setup.Name, setup == null ? (bool?)null : setup.IsActive),
                Facts = row.Browse == null ? null : new DailyReviewTradeFacts(row.Browse.ProjectionVersion,
                    row.Browse.Status, row.Browse.Direction, row.Browse.OpenedAtUtc, row.Browse.ClosedAtUtc,
                    row.Browse.OpenQuantity, row.Browse.AverageEntryPrice, row.Browse.AverageExitPrice,
                    row.Browse.TotalCosts, row.Browse.GrossPnL, row.Browse.NetPnL),
            }).ToListAsync(cancellationToken);
        // Subqueries, rather than one query per Trade or a limited browse page. Execution IDs
        // and nullable cost components retain source provenance even for partial exits.
        var ids = candidates.Select(row => row.Trade.Id);
        var executions = await context.TradeExecutions.AsNoTracking().Where(e => ids.Contains(e.TradeId))
            .OrderBy(e => e.TradeId).ThenBy(e => e.Sequence).ThenBy(e => e.Id)
            .Select(e => new { e.TradeId, Execution = new TradeExecutionDetailItem(e.Id, e.Sequence, e.ExecutedAtUtc,
                e.Side, e.Quantity, e.Price, e.Commission, e.Fees, e.Commission + e.Fees,
                e.BrokerSymbol, e.ExternalExecutionId, e.ExternalOrderId) }).ToListAsync(cancellationToken);
        var mistakes = await (from assignment in context.TradeMistakes.AsNoTracking()
            join mistake in context.TradingMistakes.AsNoTracking() on assignment.TradingMistakeId equals mistake.Id into matches
            from mistake in matches.DefaultIfEmpty()
            where ids.Contains(assignment.TradeId)
            orderby assignment.TradeId, assignment.TradingMistakeId, assignment.Id
            select new { assignment.TradeId, Evidence = new DailyReviewAssignedMistake(assignment.Id,
                new DailyReviewReference(assignment.TradingMistakeId, mistake == null ? null : mistake.Name,
                    mistake == null ? (bool?)null : mistake.IsActive), assignment.CreatedAtUtc, assignment.UpdatedAtUtc) })
            .ToListAsync(cancellationToken);
        var journals = await (from journal in context.DailyJournals.AsNoTracking()
            join account in context.TradingAccounts.AsNoTracking() on journal.TradingAccountId equals (Guid?)account.Id into accounts
            from account in accounts.DefaultIfEmpty()
            where journal.TradingDate == query.Date &&
                (query.TradingAccountId == null || journal.TradingAccountId == query.TradingAccountId)
            orderby journal.TradingAccountId != null, journal.TradingAccountId, journal.Id
            select new DailyReviewJournalEvidence(journal.Id, journal.TradingDate, journal.TradingAccountId,
                account == null ? null : account.Name,
                journal.TradingAccountId == null ? DailyJournalAccountState.AllAccounts :
                    account == null ? DailyJournalAccountState.Unavailable :
                    account.IsActive ? DailyJournalAccountState.Active : DailyJournalAccountState.Inactive,
                journal.Text, new DailyReviewAnswers(journal.WentWell, journal.NeedsImprovement, journal.NextTradingDay),
                journal.IsDraft, journal.Revision, journal.CreatedAtUtc, journal.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        var executionsByTrade = executions.ToLookup(e => e.TradeId, e => e.Execution);
        var mistakesByTrade = mistakes.ToLookup(m => m.TradeId, m => m.Evidence);
        var trades = new List<DailyReviewTradeEvidence>(selected.Count);
        foreach (var row in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var executionFacts = executionsByTrade[row.Id].ToArray();
            var assigned = mistakesByTrade[row.Id].ToArray();
            var quality = DailyReviewTradeQuality.None;
            if (row.Facts is null) quality |= DailyReviewTradeQuality.MissingProjection;
            else if (row.Facts.ProjectionVersion != TradeBrowsePersistenceMapper.CurrentProjectionVersion)
                quality |= DailyReviewTradeQuality.UnsupportedProjectionVersion;
            if (executionFacts.Length == 0) quality |= DailyReviewTradeQuality.MissingExecutions;
            if (executionFacts.Length == 0 || executionFacts.Any(e => e.Commission is null)) quality |= DailyReviewTradeQuality.UnknownCommission;
            if (executionFacts.Length == 0 || executionFacts.Any(e => e.Fees is null)) quality |= DailyReviewTradeQuality.UnknownFees;
            if (row.Facts?.GrossPnL is null) quality |= DailyReviewTradeQuality.UnknownGrossPnL;
            if (row.Facts?.NetPnL is null) quality |= DailyReviewTradeQuality.UnknownNetPnL;
            if (row.Facts is { Status: TradeStatus.Closed, ClosedAtUtc: null }) quality |= DailyReviewTradeQuality.MissingClosingTime;
            if (row.Account.IsActive is null) quality |= DailyReviewTradeQuality.UnavailableAccount;
            if (row.Instrument.IsActive is null) quality |= DailyReviewTradeQuality.UnavailableInstrument;
            if (row.Setup is { IsActive: null }) quality |= DailyReviewTradeQuality.UnavailableSetup;
            if (assigned.Any(m => m.Mistake.IsActive is null)) quality |= DailyReviewTradeQuality.UnavailableMistake;
            var inclusion = row.Facts is { Status: TradeStatus.Closed, ClosedAtUtc: not null }
                ? DailyReviewTradeInclusion.ClosedOnDate : row.Facts?.Status == TradeStatus.Open
                    ? DailyReviewTradeInclusion.OpenActivityOnDate : DailyReviewTradeInclusion.UnavailableLifecycleActivityOnDate;
            trades.Add(new(row.Id, row.Account, row.Instrument, row.Setup, row.PricingCurrency, row.PricingPointValue,
                row.CreatedAtUtc, row.UpdatedAtUtc, inclusion, row.Facts, Array.AsReadOnly(executionFacts),
                Array.AsReadOnly(assigned), quality));
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new(query, trades.AsReadOnly(), journals.AsReadOnly());
    }
}
