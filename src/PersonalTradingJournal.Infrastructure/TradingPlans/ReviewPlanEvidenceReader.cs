using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.DailyReview;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Domain.TradingPlans;
using PersonalTradingJournal.Infrastructure.Persistence;

namespace PersonalTradingJournal.Infrastructure.TradingPlans;

/// <summary>Runs only inside the evidence reader's existing SQLite transaction. A single bounded
/// selection selects latest and entry-eligible revisions; never loads every historical revision.</summary>
internal static class ReviewPlanEvidenceReader
{
    internal static async Task<ReviewPlanEvidence> ReadAsync(JournalDbContext db, DateOnly first, DateOnly last,
        Guid? account, IReadOnlyList<DailyReviewTradeEvidence> trades, IReadOnlyList<DailyReviewJournalEvidence> journals, CancellationToken token)
    {
        if (trades.Count > ReviewPlanEvidence.MaximumTrades || last.DayNumber - first.DayNumber >= ReviewPlanEvidence.MaximumDays)
            throw new ReviewPlanLimitException();
        var entries = trades.Where(t => ReviewPlanEvidence.EntryTime(t).HasValue).Select(t => new
        {
            Account = t.Account.Id.ToString().ToUpperInvariant(),
            Date = ReviewPlanEvidence.EntryDate(ReviewPlanEvidence.EntryTime(t)!.Value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            // Matches EF's SQLite DateTime storage format, with invariant UTC and full fractional precision.
            Time = ReviewPlanEvidence.EntryTime(t)!.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture),
        }).Distinct().ToArray();
        string boundaries = JsonSerializer.Serialize(entries);
        // json_each keeps the bounded set a single parameter; no lookup per Trade and no SQL assembled from source text.
        var plansQuery = db.TradingPlans.FromSqlInterpolated($"""
            SELECT p.* FROM TradingPlans p
            WHERE ((p.NewYorkDate >= {first} AND p.NewYorkDate <= {last}) AND ({account} IS NULL OR p.AccountId IS NULL OR p.AccountId = {account}))
               OR EXISTS (SELECT 1 FROM json_each({boundaries}) b WHERE (p.AccountId IS NULL OR p.AccountId = json_extract(b.value, '$.Account')) AND p.NewYorkDate = json_extract(b.value, '$.Date'))
            """).AsNoTracking();
        var plans = await plansQuery.OrderBy(p => p.NewYorkDate).ThenBy(p => p.AccountId)
            .Select(p => new { p.Id, p.AccountId, p.NewYorkDate, p.LatestReadyRevision, HasDraft = p.DraftJson != null, p.UpdatedAtUtc,
                AccountName = db.TradingAccounts.Where(a => a.Id == p.AccountId).Select(a => a.Name).FirstOrDefault() })
            .Take(ReviewPlanEvidence.MaximumDays + 1).ToArrayAsync(token);
        if (plans.Length > ReviewPlanEvidence.MaximumDays) throw new ReviewPlanLimitException();
        var days = plans.Select(p => new ReviewPlanDay(new(p.AccountId, p.NewYorkDate), p.Id,
            p.LatestReadyRevision == 0 ? ReviewPlanState.DraftOnly : p.HasDraft ? ReviewPlanState.ReadyWithDraft : ReviewPlanState.Ready,
            p.LatestReadyRevision > 0 ? p.LatestReadyRevision : null, p.HasDraft ? p.UpdatedAtUtc : null, p.AccountName)).ToList();
        var keys = days.Select(d => d.Key).ToHashSet();
        var missing = entries.Select(e => new TradingPlanKey(Guid.Parse(e.Account), DateOnly.ParseExact(e.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture)))
            .Concat(journals.Where(j => j.TradingAccountId.HasValue).Select(j => new TradingPlanKey(j.TradingAccountId!.Value, j.TradingDate))).ToList();
        if (account.HasValue)
            for (var date = first; date <= last; date = date.AddDays(1))
            {
                missing.Add(new(account.Value, date));
                if (date == DateOnly.MaxValue) break;
            }
        foreach (var key in missing.Distinct()) if (keys.Add(key)) days.Add(new(key, null, ReviewPlanState.NoPlan, null, null,
            trades.FirstOrDefault(t => t.Account.Id == key.AccountId)?.Account.Name ?? journals.FirstOrDefault(j => j.TradingAccountId == key.AccountId)?.AccountName));
        if (days.Count > ReviewPlanEvidence.MaximumDays) throw new ReviewPlanLimitException();
        string planIds = JsonSerializer.Serialize(plans.Select(p => p.Id.ToString().ToUpperInvariant()));
        var selected = db.TradingPlanReadyRevisions.FromSqlInterpolated($"""
            SELECT r.* FROM TradingPlanReadyRevisions r JOIN TradingPlans p ON p.Id = r.PlanId
            WHERE r.PlanId IN (SELECT value FROM json_each({planIds})) AND
              (r.Revision = p.LatestReadyRevision OR EXISTS (
                SELECT 1 FROM json_each({boundaries}) b
                WHERE (p.AccountId IS NULL OR p.AccountId = json_extract(b.value, '$.Account')) AND p.NewYorkDate = json_extract(b.value, '$.Date')
                  AND r.Revision = (SELECT q.Revision FROM TradingPlanReadyRevisions q
                    WHERE q.PlanId = p.Id AND q.ReadyAtUtc <= json_extract(b.value, '$.Time')
                    ORDER BY q.ReadyAtUtc DESC, q.Revision DESC LIMIT 1)))
            """).AsNoTracking();
        var size = await selected.GroupBy(_ => 1).Select(g => new { Count = g.Count(), Characters = g.Sum(r => (long)r.SnapshotJson.Length) }).SingleOrDefaultAsync(token);
        if (size?.Count > ReviewPlanEvidence.MaximumRevisions || size?.Characters > ReviewPlanEvidence.MaximumCharacters) throw new ReviewPlanLimitException();
        var rows = await selected.OrderBy(r => r.PlanId).ThenBy(r => r.Revision).ToArrayAsync(token);
        var revisions = rows.Select(row =>
        {
            token.ThrowIfCancellationRequested();
            var r = TradingPlanJson.ReadReady(row.SnapshotJson);
            var owner = plans.Single(p => p.Id == row.PlanId);
            if (r.Id.PlanId != row.PlanId || r.Id.Revision != row.Revision || r.ReadyAtUtc != row.ReadyAtUtc ||
                r.Key.AccountId != owner.AccountId || r.Key.NewYorkDate != owner.NewYorkDate)
                throw new InvalidDataException("Inconsistent saved preparation identity.");
            return ReviewPlanRevision.From(r);
        }).ToArray();
        if (plans.Any(p => p.LatestReadyRevision > 0 && !revisions.Any(r => r.Id.PlanId == p.Id && r.Id.Revision == p.LatestReadyRevision)))
            throw new InvalidDataException("Missing latest Ready preparation.");
        token.ThrowIfCancellationRequested();
        return ReviewPlanEvidence.Build(first, last, account, days, revisions, trades);
    }
}
