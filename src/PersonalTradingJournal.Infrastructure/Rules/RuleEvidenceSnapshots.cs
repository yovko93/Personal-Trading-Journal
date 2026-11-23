using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Analytics;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Domain.Rules;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Rules;

// Identity/provenance snapshots, not a second copy of source text or economic calculations.
internal sealed record RuleEvidenceSnapshot(TradingRuleEvidenceKind Kind, Guid SourceId, long? Revision,
    Guid? AccountId, string? AccountName, Guid? TradeId, DateOnly? NewYorkDate, string? Currency,
    string Fingerprint)
{
    [JsonIgnore] public TradingRuleEvidenceReference Reference => new(Kind, SourceId, Revision);
}

internal sealed class RuleEvidenceSnapshots
{
    // Rule evaluation consumes identity/economics, not optional Trade prose.
    internal static readonly Expression<Func<TradeRecord, TradeRecord>> TradeFacts = t => new TradeRecord
    {
        Id = t.Id, TradingAccountId = t.TradingAccountId, InstrumentId = t.InstrumentId,
        PricingPointValue = t.PricingPointValue, PricingCurrency = t.PricingCurrency,
        TradingSetupId = t.TradingSetupId, CreatedAtUtc = t.CreatedAtUtc, UpdatedAtUtc = t.UpdatedAtUtc
    };
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal Dictionary<TradingRuleEvidenceReference, RuleEvidenceSnapshot> Items { get; } = [];
    private readonly Dictionary<Guid, DateTimeOffset[]> activity = [];

    internal bool Matches(RuleEvidenceSnapshot source, TradingRuleAssessmentTarget target,
        RuleEvidenceSnapshot? targetTrade)
    {
        if (target.AccountScope.AccountId is Guid account && source.AccountId != account) return false;
        if (target.Kind == TradingRuleTargetKind.Trade)
            return source.TradeId == target.TradeId || (source.Kind == TradingRuleEvidenceKind.Journal &&
                source.NewYorkDate is not null && source.NewYorkDate == targetTrade?.NewYorkDate);
        if (source.NewYorkDate is not null) return source.NewYorkDate == target.NewYorkDate;
        // Open/partial activity is evidence only on dates with actual recorded executions.
        var bounds = new TradingCalendarDayQuery(target.NewYorkDate!.Value);
        return source.TradeId is Guid trade && activity.TryGetValue(trade, out var times) &&
            times.Any(time => time >= bounds.ClosedFromUtc && time < bounds.ClosedBeforeUtc);
    }

    internal static string Hash<T>(T value) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, Json)));

    internal static async Task<RuleEvidenceSnapshots> LoadAsync(JournalDbContext db,
        IEnumerable<TradingRuleEvidenceReference> references, CancellationToken token)
    {
        var result = new RuleEvidenceSnapshots();
        // Bound each SQL batch, including pair predicates, regardless of a history page's size.
        foreach (var chunk in references.Distinct().Chunk(200))
        {
            token.ThrowIfCancellationRequested();
            Guid[] executionIds = chunk.Where(r => r.Kind == TradingRuleEvidenceKind.Execution).Select(r => r.SourceId).ToArray();
            var selectedExecutions = await db.TradeExecutions.AsNoTracking().Where(e => executionIds.Contains(e.Id)).ToArrayAsync(token);
            Guid[] tradeIds = chunk.Where(r => r.Kind == TradingRuleEvidenceKind.Trade)
                .Select(r => r.SourceId).Concat(selectedExecutions.Select(e => e.TradeId)).Distinct().ToArray();
            var trades = await db.Trades.AsNoTracking().Where(t => tradeIds.Contains(t.Id))
                .Select(TradeFacts).ToDictionaryAsync(t => t.Id, token);
            var executions = await db.TradeExecutions.AsNoTracking().Where(e => tradeIds.Contains(e.TradeId))
                .OrderBy(e => e.TradeId).ThenBy(e => e.Sequence).ThenBy(e => e.Id).ToArrayAsync(token);
            var byTrade = executions.ToLookup(e => e.TradeId);
            Guid[] journalIds = chunk.Where(r => r.Kind == TradingRuleEvidenceKind.Journal).Select(r => r.SourceId).ToArray();
            var journals = await db.DailyJournals.AsNoTracking().Where(j => journalIds.Contains(j.Id)).ToDictionaryAsync(j => j.Id, token);
            var journalRevisions = await db.DailyJournalRevisions.AsNoTracking()
                .Where(Pairs<DailyJournalRevisionRecord>(chunk.Where(r => r.Kind == TradingRuleEvidenceKind.Journal), "JournalId", "Revision"))
                .ToDictionaryAsync(r => (r.JournalId, r.Revision), token);
            Guid[] accountIds = trades.Values.Select(t => t.TradingAccountId).Concat(journals.Values
                .Where(j => j.TradingAccountId.HasValue).Select(j => j.TradingAccountId!.Value)).Distinct().ToArray();
            var accounts = await db.TradingAccounts.AsNoTracking().Where(a => accountIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Name, token);
            var tradeSnapshots = new Dictionary<Guid, RuleEvidenceSnapshot>();
            foreach (var trade in trades.Values)
            {
                var rows = byTrade[trade.Id].ToArray();
                DateOnly? day = null;
                if (rows.Length > 0)
                {
                    var domain = TradePersistenceMapper.ToDomain(trade, rows);
                    if (domain.ClosedAtUtc is DateTimeOffset closed) day = DashboardMetricCalculator.GetNewYorkCloseDate(closed);
                }
                result.activity[trade.Id] = rows.Select(e => e.ExecutedAtUtc).ToArray();
                tradeSnapshots[trade.Id] = new(TradingRuleEvidenceKind.Trade, trade.Id, null, trade.TradingAccountId,
                    accounts.GetValueOrDefault(trade.TradingAccountId), trade.Id, day, trade.PricingCurrency,
                    // Keep the established fingerprint shape when adding optional prose.
                    // A null Description introduced by migration must not invalidate history.
                    Hash(new { Trade = new { trade.Id, trade.TradingAccountId, trade.InstrumentId,
                        trade.PricingPointValue, trade.PricingCurrency, trade.TradingSetupId,
                        trade.CreatedAtUtc, trade.UpdatedAtUtc }, Executions = rows }));
            }
            foreach (var reference in chunk)
            {
                RuleEvidenceSnapshot? snapshot = null;
                if (reference.Kind == TradingRuleEvidenceKind.Trade && reference.Revision is null)
                    tradeSnapshots.TryGetValue(reference.SourceId, out snapshot);
                else if (reference.Kind == TradingRuleEvidenceKind.Execution && reference.Revision is null)
                {
                    var execution = selectedExecutions.SingleOrDefault(e => e.Id == reference.SourceId);
                    if (execution is not null && tradeSnapshots.TryGetValue(execution.TradeId, out var trade))
                        snapshot = trade with { Kind = reference.Kind, SourceId = execution.Id,
                            NewYorkDate = trade.NewYorkDate ?? DashboardMetricCalculator.GetNewYorkCloseDate(execution.ExecutedAtUtc),
                            Fingerprint = Hash(new { Execution = execution, trade.AccountId, trade.Currency, trade.NewYorkDate }) };
                }
                else if (reference.Kind == TradingRuleEvidenceKind.Journal && reference.Revision is long revision &&
                    journals.TryGetValue(reference.SourceId, out var journal) && journalRevisions.TryGetValue((journal.Id, revision), out var saved))
                    snapshot = new(reference.Kind, journal.Id, revision, journal.TradingAccountId,
                        journal.TradingAccountId is Guid account ? accounts.GetValueOrDefault(account) : null,
                        null, journal.TradingDate, null, Hash(new { journal.Id, journal.TradingAccountId, journal.TradingDate, Revision = saved }));
                if (snapshot is not null) result.Items[reference] = snapshot;
            }
        }
        return result;
    }

    private static Expression<Func<T, bool>> Pairs<T>(IEnumerable<TradingRuleEvidenceReference> references, string idName, string revisionName)
    {
        var row = Expression.Parameter(typeof(T), "row");
        Expression predicate = Expression.Constant(false);
        foreach (var reference in references.Where(r => r.Revision.HasValue))
        {
            var revision = Expression.Property(row, revisionName);
            predicate = Expression.OrElse(predicate, Expression.AndAlso(
                Expression.Equal(Expression.Property(row, idName), Expression.Constant(reference.SourceId)),
                Expression.Equal(revision, Expression.Constant(Convert.ChangeType(reference.Revision!.Value, revision.Type), revision.Type))));
        }
        return Expression.Lambda<Func<T, bool>>(predicate, row);
    }
}
