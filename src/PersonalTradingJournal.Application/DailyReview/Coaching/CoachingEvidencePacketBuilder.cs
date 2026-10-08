using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PersonalTradingJournal.Application.DailyReview.Coaching;

public static class CoachingEvidencePacketBuilder
{
    public static CoachingPacketBuildResult Build(DailyReviewEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (evidence.Trades.Count > CoachingContract.MaximumTrades ||
                evidence.Journals.Count > CoachingContract.MaximumJournals ||
                evidence.Trades.Sum(t => (long)t.Executions.Count) > CoachingContract.MaximumExecutions ||
                evidence.Trades.Sum(t => (long)t.AssignedMistakes.Count) > CoachingContract.MaximumMistakeAssignments)
                return TooLarge();

            var trades = evidence.Trades.OrderBy(t => t.TradeId).Select(t =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return t with
                {
                    Executions = Array.AsReadOnly(t.Executions.OrderBy(e => e.Sequence).ThenBy(e => e.Id).ToArray()),
                    AssignedMistakes = Array.AsReadOnly(t.AssignedMistakes.OrderBy(m => m.Mistake.Id)
                        .ThenBy(m => m.AssignmentId).ToArray()),
                };
            }).ToArray();
            var journals = evidence.Journals.OrderBy(j => j.TradingAccountId).ThenBy(j => j.JournalId).ToArray();
            var journalIds = new HashSet<Guid>();
            var journalScopes = new HashSet<Guid?>();
            foreach (var journal in journals)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (journal.JournalId == Guid.Empty || !journalIds.Add(journal.JournalId) ||
                    !journalScopes.Add(journal.TradingAccountId) || journal.Revision < 1 ||
                    journal.TradingAccountId == Guid.Empty || journal.TradingDate != evidence.Query.Date ||
                    evidence.Query.TradingAccountId is { } account && journal.TradingAccountId != account ||
                    journal.Text is null || journal.Answers is null)
                    return Invalid("Journal identities, revisions, dates and exact Account scopes must match the evidence request.");
            }
            var executionIds = new HashSet<Guid>();
            foreach (var trade in trades)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (trade.Executions.Any(e => e.Id == Guid.Empty || !executionIds.Add(e.Id)))
                    return Invalid("Execution identities must be nonempty and unique.");
            }
            var snapshot = new DailyReviewEvidence(evidence.Query, Array.AsReadOnly(trades), Array.AsReadOnly(journals));
            var statistics = DailyReviewStatisticsCalculator.Calculate(snapshot, cancellationToken);
            var sources = new List<CoachingSource> { new("calculated:day", CoachingSourceKind.DayStatistics,
                evidence.Query.TradingAccountId) };
            foreach (var currency in statistics.Currencies)
            {
                var prefix = "calculated:currency:" + Uri.EscapeDataString(currency.Currency);
                sources.Add(new(prefix, CoachingSourceKind.CurrencyStatistics, evidence.Query.TradingAccountId, currency.Currency));
                sources.AddRange(currency.Accounts.Select(a => new CoachingSource(prefix + ":account:" + a.Account.Id.ToString("N"),
                    CoachingSourceKind.AccountStatistics, a.Account.Id, currency.Currency)));
            }
            foreach (var trade in trades)
            {
                sources.Add(new("trade:" + trade.TradeId.ToString("N"), CoachingSourceKind.Trade,
                    trade.Account.Id, trade.PricingCurrency, trade.TradeId));
                sources.AddRange(trade.Executions.Select(e => new CoachingSource("execution:" + e.Id.ToString("N"),
                    CoachingSourceKind.Execution, trade.Account.Id, trade.PricingCurrency, trade.TradeId, e.Id)));
            }
            sources.AddRange(journals.Select(j => new CoachingSource(JournalSource(j), CoachingSourceKind.Journal,
                j.TradingAccountId, JournalId: j.JournalId, Revision: j.Revision)));
            var gaps = journals.Select(j => new CoachingJournalGaps(JournalSource(j), Array.AsReadOnly(
                new[] { ("text", j.Text), ("wentWell", j.Answers.WentWell),
                    ("needsImprovement", j.Answers.NeedsImprovement), ("nextTradingDay", j.Answers.NextTradingDay) }
                .Where(f => string.IsNullOrWhiteSpace(f.Item2)).Select(f => f.Item1).ToArray()))).ToArray();
            var content = new CoachingEvidenceContent(snapshot.Query, CoachingContract.ContentHandling, statistics,
                snapshot.Trades, snapshot.Journals,
                new(trades.Length == 0, statistics.Population.Closed.Count == 0, journals.Length == 0, false,
                    Array.AsReadOnly(trades.Select(t => (Guid?)t.Account.Id).Append(evidence.Query.TradingAccountId)
                        .Distinct().Where(id => !journalScopes.Contains(id)).Order().ToArray()), Array.AsReadOnly(gaps),
                    "Trades have no durable revision token. Audit timestamps and projection format versions do not recreate past evidence."),
                Array.AsReadOnly(sources.OrderBy(s => s.Id, StringComparer.Ordinal).ToArray()));
            cancellationToken.ThrowIfCancellationRequested();
            // Bound the actual escaped UTF-8 wire format, not characters or an estimated token count.
            var hashBytes = SerializeBounded(new { ContractVersion = CoachingContract.Version, Content = content }, cancellationToken);
            string packetId = Convert.ToHexStringLower(SHA256.HashData(hashBytes));
            var bytes = SerializeBounded(new { ContractVersion = CoachingContract.Version, PacketId = packetId, Content = content }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return new(CoachingPacketBuildStatus.Ready, new(content, packetId, Encoding.UTF8.GetString(bytes), bytes.Length), null);
        }
        catch (PacketLimitException) { return TooLarge(); }
        catch (OverflowException) { return new(CoachingPacketBuildStatus.CalculationOverflow, null, "Statistics exceed decimal capacity; no packet was produced."); }
        catch (ArgumentException) { return Invalid("Evidence has invalid identities, scope, currency or closure facts; no packet was produced."); }
    }

    private static string JournalSource(DailyReviewJournalEvidence journal) =>
        "journal:" + journal.JournalId.ToString("N") + ":revision:" + journal.Revision.ToString(CultureInfo.InvariantCulture);

    private static CoachingPacketBuildResult TooLarge() => new(CoachingPacketBuildStatus.TooLarge, null,
        "Complete evidence exceeds the v1 packet limits. No records, text or statistics were truncated; no packet was produced.");
    private static CoachingPacketBuildResult Invalid(string error) => new(CoachingPacketBuildStatus.InvalidEvidence, null, error);

    private static byte[] SerializeBounded<T>(T value, CancellationToken token)
    {
        using var stream = new BoundedStream(token);
        JsonSerializer.Serialize(stream, value, CoachingContract.JsonOptions);
        return stream.ToArray();
    }

    private sealed class PacketLimitException : Exception;
    private sealed class BoundedStream(CancellationToken token) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            Check(count);
            base.Write(buffer, offset, count);
        }
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Check(buffer.Length);
            // Use MemoryStream's byte[] implementation directly, avoiding Stream's span fallback
            // dispatching into this subclass again.
            base.Write(buffer.ToArray(), 0, buffer.Length);
        }
        private void Check(int count)
        {
            token.ThrowIfCancellationRequested();
            if (Length + count > CoachingContract.MaximumPacketBytes) throw new PacketLimitException();
        }
    }
}
