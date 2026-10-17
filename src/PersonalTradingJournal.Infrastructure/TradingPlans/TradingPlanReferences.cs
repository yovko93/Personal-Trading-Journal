using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Domain.TradingPlans;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Rules;

namespace PersonalTradingJournal.Infrastructure.TradingPlans;

internal sealed class PlanReferenceException(TradingPlanReferenceIssue issue) : Exception("A Trading Plan reference needs attention.")
{
    internal TradingPlanReferenceIssue Issue { get; } = issue;
}

internal sealed record TradingPlanReferences(TradingPlanReferenceSnapshot Snapshot, Dictionary<string, string> Tokens)
{
    internal static void ValidateStored(Guid accountId, TradingPlanContent content, string json)
    {
        var tokens = TradingPlanJson.Deserialize<Dictionary<string, string>>(json);
        var expected = new[] { $"1:{accountId:D}" }
            .Concat(content.Instruments.Select(x => $"2:{x.InstrumentId:D}"))
            .Concat(content.Scenarios.Where(x => x.Setup is not null).Select(x => $"3:{x.Setup!.SetupId:D}").Distinct())
            .Concat(content.Rules.Select(x => $"4:{x.RuleId:D}"));
        if (!expected.Order(StringComparer.Ordinal).SequenceEqual(tokens.Keys.Order(StringComparer.Ordinal)) ||
            tokens.Values.Any(x => x is not { Length: 64 } || x.Any(c => !char.IsAsciiHexDigit(c))))
            throw new InvalidDataException("Invalid stored Plan reference tokens.");
    }
    internal static async Task<TradingPlanReferences> Resolve(JournalDbContext db, TradingPlanKey key,
        TradingPlanContent content, DateTimeOffset now, CancellationToken ct)
    {
        var tokens = new Dictionary<string, string>();
        void Token(TradingPlanReferenceKind kind, Guid id, object record) => tokens.Add($"{(int)kind}:{id:D}",
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(TradingPlanJson.Write(record)))));
        static void Require(bool condition, TradingPlanReferenceKind kind, Guid id, TradingPlanReferenceState state)
        {
            if (!condition) throw new PlanReferenceException(new(kind, id, state,
                "Reload the exact source selection; unavailable or changed references cannot be confirmed."));
        }
        var a = await db.TradingAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == key.AccountId, ct);
        Require(a is not null, TradingPlanReferenceKind.Account, key.AccountId, TradingPlanReferenceState.Unavailable);
        Require(a!.IsActive, TradingPlanReferenceKind.Account, a.Id, TradingPlanReferenceState.Inactive);
        // Deliberately conservative: labels and audit changes also require explicit acknowledgement.
        Token(TradingPlanReferenceKind.Account, a.Id, a);
        var ids = content.Instruments.Select(x => x.InstrumentId).ToArray();
        var instruments = await db.Instruments.AsNoTracking().Where(x => ids.Contains(x.Id)).ToArrayAsync(ct);
        foreach (var id in ids)
        {
            var i = instruments.SingleOrDefault(x => x.Id == id);
            Require(i is not null, TradingPlanReferenceKind.Instrument, id, TradingPlanReferenceState.Unavailable);
            Require(i!.IsActive, TradingPlanReferenceKind.Instrument, id, TradingPlanReferenceState.Inactive);
            Token(TradingPlanReferenceKind.Instrument, id, i);
        }
        var setupIds = content.Scenarios.Where(x => x.Setup is not null).Select(x => x.Setup!.SetupId).Distinct().ToArray();
        var setups = await db.TradingSetups.AsNoTracking().Where(x => setupIds.Contains(x.Id)).ToArrayAsync(ct);
        var playbooks = await db.PlaybookRevisions.AsNoTracking().Where(x => setupIds.Contains(x.TradingSetupId) &&
            !db.PlaybookRevisions.Any(n => n.TradingSetupId == x.TradingSetupId && n.Revision > x.Revision)).ToArrayAsync(ct);
        var sources = new List<TradingPlanPlaybookSource>();
        foreach (var id in setupIds)
        {
            var s = setups.SingleOrDefault(x => x.Id == id); var p = playbooks.SingleOrDefault(x => x.TradingSetupId == id);
            Require(s is not null && p is not null, TradingPlanReferenceKind.Playbook, id, TradingPlanReferenceState.Unavailable);
            Require(s!.IsActive, TradingPlanReferenceKind.Playbook, id, TradingPlanReferenceState.Inactive);
            Require(content.Scenarios.Where(x => x.Setup?.SetupId == id).All(x => x.Setup!.ExpectedPlaybookRevision == p!.Revision &&
                x.Setup.ExpectedUpdatedAtUtc == s.UpdatedAtUtc), TradingPlanReferenceKind.Playbook, id, TradingPlanReferenceState.Changed);
            Token(TradingPlanReferenceKind.Playbook, id, new { Setup = s, Playbook = p });
            // The Plan contract captures five guidance sections, not image/example content.
            sources.Add(new(TradingSetupPersistenceMapper.ToDomain(s), PlaybookPersistenceMapper.ToDomain(p!, [])));
        }
        var ruleIds = content.Rules.Select(x => x.RuleId).ToArray();
        var rules = await db.TradingRuleRevisions.AsNoTracking().Where(x => ruleIds.Contains(x.RuleId) &&
            !db.TradingRuleRevisions.Any(n => n.RuleId == x.RuleId && n.Revision > x.Revision)).ToArrayAsync(ct);
        foreach (var link in content.Rules)
        {
            var r = rules.SingleOrDefault(x => x.RuleId == link.RuleId);
            Require(r is not null, TradingPlanReferenceKind.Rule, link.RuleId, TradingPlanReferenceState.Unavailable);
            Require(r!.IsActive, TradingPlanReferenceKind.Rule, link.RuleId, TradingPlanReferenceState.Inactive);
            Require(r.IsGlobal, TradingPlanReferenceKind.Rule, link.RuleId, TradingPlanReferenceState.LegacyScoped);
            Require(r.Revision == link.ExpectedRevision, TradingPlanReferenceKind.Rule, link.RuleId, TradingPlanReferenceState.Changed);
            Token(TradingPlanReferenceKind.Rule, link.RuleId, r);
        }
        return new(TradingPlanReferenceSnapshot.Capture(key, content, TradingAccountPersistenceMapper.ToDomain(a),
            instruments.Select(InstrumentPersistenceMapper.ToDomain), sources, rules.Select(TradingRuleRepository.ToDomain), now), tokens);
    }

    internal void CheckPrevious(string previous)
    {
        var old = TradingPlanJson.Deserialize<Dictionary<string, string>>(previous);
        foreach (var (key, value) in Tokens)
            if (old.TryGetValue(key, out var prior) && prior != value)
            {
                var parts = key.Split(':');
                throw new PlanReferenceException(new((TradingPlanReferenceKind)int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                    Guid.Parse(parts[1]), TradingPlanReferenceState.Changed,
                    "A saved reference changed. Explicitly refresh references and reconfirm preparation."));
            }
    }
}
