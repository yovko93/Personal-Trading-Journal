using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Calendar;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Domain.Rules;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Infrastructure.Rules;

public sealed partial class TradingRuleRepository : IManualRuleAssessmentReader
{
    private static int PageOffset(int page, int size)
    {
        ValidateLimit(size);
        if (page < 1 || (long)(page - 1) * size > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(page));
        return (page - 1) * size;
    }

    public async Task<RuleAssessmentTargetInfo> ReadTargetAsync(TradingRuleAssessmentTarget target, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        await using var db = await factory.CreateDbContextAsync(token);
        await using var snapshot = await ReadSnapshot(db, token);
        string scope = "All accounts (aggregate)";
        bool available = true;
        if (target.AccountScope.AccountId is Guid account)
        {
            var row = await db.TradingAccounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == account, token);
            available = row is not null;
            scope = row is null ? $"Unavailable Account ({account})" : row.Name + (row.IsActive ? "" : " (inactive)");
        }
        if (target.TradeId is Guid trade)
        {
            available &= await db.Trades.AnyAsync(t => t.Id == trade && t.TradingAccountId == target.AccountScope.AccountId, token);
            return new($"Trade {trade} · {scope}", available);
        }
        return new($"{target.NewYorkDate:dd MMM yyyy} · New York · {scope}", available);
    }

    public async Task<TradingRulePage> ReadRulesAsync(TradingRuleAssessmentTarget target, int page = 1, int pageSize = 20, CancellationToken token = default)
    {
        int offset = PageOffset(page, pageSize);
        string key = TargetKey(target);
        int applicability = target.Kind == TradingRuleTargetKind.Trade ? (int)TradingRuleApplicability.Trade : (int)TradingRuleApplicability.NewYorkDay;
        await using var db = await factory.CreateDbContextAsync(token);
        await using var snapshot = await ReadSnapshot(db, token);
        var query = db.TradingRuleRevisions.AsNoTracking().Where(r =>
            !db.TradingRuleRevisions.Any(n => n.RuleId == r.RuleId && n.Revision > r.Revision) &&
            ((r.Applicability == applicability && (r.AccountId == null || r.AccountId == target.AccountScope.AccountId)) ||
             db.TradingRuleAssessments.Any(a => a.RuleId == r.RuleId && a.TargetKey == key)));
        int total = await query.CountAsync(token);
        var rows = await query.OrderBy(r => r.RuleId).Skip(offset).Take(pageSize).ToArrayAsync(token);
        return new(await RuleDetails(db, rows, token), total, page, pageSize);
    }

    public async Task<IReadOnlyList<TradingRuleAssessmentDetails>> ReadRuleHistoryAsync(Guid ruleId, int page = 1, int pageSize = 10, CancellationToken token = default)
    {
        ValidateId(ruleId); int offset = PageOffset(page, pageSize);
        await using var db = await factory.CreateDbContextAsync(token);
        await using var snapshot = await ReadSnapshot(db, token);
        var rows = await db.TradingRuleAssessments.AsNoTracking().Where(a => a.RuleId == ruleId)
            .OrderByDescending(a => a.RecordedAtUtc).ThenByDescending(a => a.Id).Skip(offset).Take(pageSize + 1).ToArrayAsync(token);
        return await AssessmentDetails(db, rows, token);
    }

    public async Task<IReadOnlyList<RuleEvidenceOption>> ReadEvidenceAsync(TradingRuleAssessmentTarget target,
        TradingRuleEvidenceKind kind, int page = 1, int pageSize = 20, CancellationToken token = default)
    {
        int offset = PageOffset(page, pageSize);
        if (kind is not (TradingRuleEvidenceKind.Trade or TradingRuleEvidenceKind.Journal)) throw new ArgumentOutOfRangeException(nameof(kind));
        await using var db = await factory.CreateDbContextAsync(token);
        await using var snapshot = await ReadSnapshot(db, token);
        if (kind == TradingRuleEvidenceKind.Journal)
        {
            // A Trade supplies its complete lifecycle as a single source. Day assessments
            // additionally expose Journals; no implicit null scope is used for an exact Account.
            if (target.NewYorkDate is not DateOnly date) return [];
            var journals = await (from j in db.DailyJournals.AsNoTracking()
                join a in db.TradingAccounts on j.TradingAccountId equals (Guid?)a.Id into accounts
                from a in accounts.DefaultIfEmpty()
                where j.TradingDate == date && (target.AccountScope.AccountId == null || j.TradingAccountId == target.AccountScope.AccountId)
                orderby j.Id
                select new { j.Id, j.Revision, j.IsDraft, j.Text, j.WentWell, j.NeedsImprovement, j.NextTradingDay,
                    Account = j.TradingAccountId == null ? "All accounts" : a == null ? "Unavailable Account" : a.Name })
                .Skip(offset).Take(pageSize + 1).ToArrayAsync(token);
            return journals.Select(j => new RuleEvidenceOption(new(TradingRuleEvidenceKind.Journal, j.Id, j.Revision),
                $"Journal · {j.Account} · {(j.IsDraft ? "Draft" : "Completed")} · revision {j.Revision}",
                $"Journal text: {j.Text}\nWent well: {j.WentWell}\nNeeds improvement: {j.NeedsImprovement}\nNext day: {j.NextTradingDay}")).ToArray();
        }
        // Same closed-day / open-activity inclusion as DailyReviewEvidenceReader. The writer
        // independently reconstructs and rechecks date, ownership and source identity.
        var day = target.NewYorkDate is DateOnly selected ? new TradingCalendarDayQuery(selected) : null;
        DateTimeOffset fromUtc = day?.ClosedFromUtc ?? DateTimeOffset.MinValue, before = day?.ClosedBeforeUtc ?? DateTimeOffset.MaxValue;
        var trades = await (from t in db.Trades.AsNoTracking()
            join b in db.TradeBrowse on t.Id equals b.TradeId into browse
            from b in browse.DefaultIfEmpty()
            join a in db.TradingAccounts on t.TradingAccountId equals a.Id
            join i in db.Instruments on t.InstrumentId equals i.Id
            where (target.AccountScope.AccountId == null || t.TradingAccountId == target.AccountScope.AccountId) &&
                (target.TradeId != null ? t.Id == target.TradeId :
                 ((b != null && b.Status == TradeStatus.Closed && b.ClosedAtUtc >= fromUtc && b.ClosedAtUtc < before) ||
                  ((b == null || b.Status != TradeStatus.Closed || b.ClosedAtUtc == null) &&
                   db.TradeExecutions.Any(e => e.TradeId == t.Id && e.ExecutedAtUtc >= fromUtc && e.ExecutedAtUtc < before))))
            orderby t.Id
            select new { t.Id, t.PricingCurrency, a.Name, i.Symbol, Gross = b == null ? null : b.GrossPnL, Net = b == null ? null : b.NetPnL })
            .Skip(offset).Take(pageSize + 1).ToArrayAsync(token);
        return trades.Select(t => new RuleEvidenceOption(new(TradingRuleEvidenceKind.Trade, t.Id),
            $"{t.Symbol} · {t.Name} · {t.PricingCurrency} · Trade {t.Id}",
            $"Gross: {t.Gross?.ToString() ?? "Unavailable"}; Net: {t.Net?.ToString() ?? "Unavailable (open activity or incomplete costs)"}. This reference includes the recorded execution lifecycle; linking it does not establish sufficiency or adherence.")).ToArray();
    }
}
