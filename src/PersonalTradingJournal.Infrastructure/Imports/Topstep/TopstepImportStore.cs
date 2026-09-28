using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Application.Imports.Topstep;
using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Imports.Topstep;

public sealed class TopstepImportStore(IDbContextFactory<JournalDbContext> contextFactory) : ITopstepImportStore
{
    public async Task<TopstepImportResult> ImportAsync(TopstepImportRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Preview);
        ArgumentNullException.ThrowIfNull(request.Review);
        cancellationToken.ThrowIfCancellationRequested();
        TopstepImportPreview expected = request.Preview;
        var review = new TopstepPreviewReview(request.Review.SnapshotFingerprint,
            Array.AsReadOnly((request.Review.AcceptedRequirementKeys ?? []).ToArray()));
        if (!expected.MeetsReviewRequirements(review)) return Block(TopstepImportConflictCodes.ReviewRequired,
            "A valid preview and explicit review of every snapshot-bound warning and Instrument proposal are required.");
        if (request.ImportedAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Import audit time must be UTC.", nameof(request));

        await using JournalDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        // Microsoft.Data.Sqlite's non-deferred write transaction serializes confirmation before reads.
        // Unique source indexes additionally protect identity even outside this import entry point.
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        Guid accountId = expected.SelectedAccountId!.Value;
        TradingAccountRecord? account = await context.TradingAccounts.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == accountId, cancellationToken).ConfigureAwait(false);
        InstrumentRecord[] instruments = await context.Instruments.AsNoTracking().ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var readers = new TransactionReferences(account, instruments);
        var builder = new TopstepImportPreviewBuilder(new TopstepCsvParser(), new TopstepTradeCandidateReconstructor(), new(readers, readers));
        TopstepImportPreview current = await builder.BuildAsync(request.FileName, request.Source, accountId,
            expected.Candidates[0].Economics.CostInterpretation, expected.VerifiedExistingInstruments, cancellationToken).ConfigureAwait(false);
        if (current.SourceIdentity != expected.SourceIdentity) return Block(TopstepImportConflictCodes.SourceChanged,
            "The source file differs from the reviewed snapshot. Select the current file, rebuild preview and review it again.");
        if (current.SnapshotFingerprint != expected.SnapshotFingerprint || !current.MeetsReviewRequirements(review))
            return Block(TopstepImportConflictCodes.ReferenceDataChanged,
                "Account, Instrument resolution, pricing or review policy changed. Rebuild and review the current preview; no Instrument is substituted.");

        TopstepImportedRowRecord[] ledger = await context.TopstepImportedRows.AsNoTracking()
            .Where(r => r.TradingAccountIdAtImport == accountId).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var existing = ledger.ToDictionary(r => r.SourceId, StringComparer.Ordinal);
        var newRows = new List<(TopstepPreviewCandidate Candidate, string Fingerprint)>();
        var duplicates = new List<Guid>();
        foreach (TopstepPreviewCandidate candidate in current.Candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fingerprint = EconomicFingerprint(candidate);
            if (!existing.TryGetValue(candidate.SourceRow.Id, out TopstepImportedRowRecord? previous))
                newRows.Add((candidate, fingerprint));
            else if (previous.EconomicFingerprint != fingerprint)
                return Block(TopstepImportConflictCodes.SourceIdentityConflict,
                    $"Source record {candidate.SourceRecordIndex} (line {candidate.SourceLineNumber}) conflicts with a previously imported Topstep row in this account. Correct the source/account selection; existing Trades are never overwritten.");
            else duplicates.Add(previous.TradeId);
        }
        if (newRows.Count == 0) return new(TopstepImportStatus.NoChanges, 0, duplicates.Count, 0, [], duplicates.AsReadOnly(), []);

        var resolved = new Dictionary<string, InstrumentRecord>(StringComparer.Ordinal);
        var created = new List<Guid>();
        var imported = new List<Guid>();
        foreach ((TopstepPreviewCandidate candidate, string fingerprint) in newRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string canonical = candidate.Instrument.Identity!.CanonicalSymbol;
            if (!resolved.TryGetValue(canonical, out InstrumentRecord? instrument))
            {
                if (candidate.Instrument.ExistingInstrument is { } selected)
                    instrument = instruments.Single(r => r.Id == selected.Id);
                else
                {
                    TopstepInstrumentCreationProposal proposal = candidate.Instrument.CreationProposal!;
                    instrument = InstrumentPersistenceMapper.ToRecord(new Instrument(proposal.CanonicalSymbol, proposal.DisplayName,
                        proposal.AssetClass, proposal.Exchange, proposal.Currency, proposal.TickSize, proposal.TickValue, request.ImportedAtUtc));
                    context.Instruments.Add(instrument);
                    created.Add(instrument.Id);
                }
                resolved.Add(canonical, instrument);
            }
            Trade trade = CreateTrade(candidate, accountId, instrument.Id, request.ImportedAtUtc);
            if (trade.GrossPnL != candidate.CalculatedGross || trade.NetPnL != candidate.CalculatedNet)
                return Block(TopstepImportConflictCodes.EconomicsMismatch,
                    "Derived Domain executions do not exactly match the reviewed row economics. No rows were imported.");
            context.Trades.Add(TradePersistenceMapper.ToRecord(trade));
            context.TradeBrowse.Add(TradeBrowsePersistenceMapper.ToRecord(trade));
            context.TradeExecutions.AddRange(trade.Executions.Select(TradeExecutionPersistenceMapper.ToRecord));
            context.TopstepImportedRows.Add(new()
            {
                Id = Guid.NewGuid(), TradeId = trade.Id, TradingAccountIdAtImport = accountId,
                SourceId = candidate.SourceRow.Id, EconomicFingerprint = fingerprint,
                SourceRowJson = JsonSerializer.Serialize(candidate.SourceRow), PreviewFingerprint = current.SnapshotFingerprint,
                SourceContentSha256 = current.SourceIdentity.ContentSha256,
                DerivedEntryExecutionId = trade.Executions[0].Id, DerivedExitExecutionId = trade.Executions[1].Id,
                ImportedAtUtc = request.ImportedAtUtc,
            });
            imported.Add(trade.Id);
        }
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        // No cancellable or fallible work follows the commit. Disposal rolls back every noncommitted exit.
        return new(TopstepImportStatus.Imported, imported.Count, duplicates.Count, created.Count,
            imported.AsReadOnly(), duplicates.AsReadOnly(), created.AsReadOnly());
    }

    private static TopstepImportResult Block(string code, string message) => TopstepImportResult.Blocked(code, message);

    private static Trade CreateTrade(TopstepPreviewCandidate candidate, Guid accountId, Guid instrumentId, DateTimeOffset at)
    {
        Guid tradeId = Guid.NewGuid();
        ExecutionSide entrySide = candidate.Direction == TradeDirection.Long ? ExecutionSide.Buy : ExecutionSide.Sell;
        // These two internal lifecycle records represent one closed-row report, NOT broker executions.
        // Costs are known row totals: allocate zero to entry and the complete totals once to exit.
        var entry = new TradeExecution(tradeId, 1, candidate.EnteredAtUtc, entrySide, candidate.ClosedRowQuantity,
            candidate.EntryPrice, 0m, 0m, null, null, candidate.ContractName);
        var exit = new TradeExecution(tradeId, 2, candidate.ExitedAtUtc,
            entrySide == ExecutionSide.Buy ? ExecutionSide.Sell : ExecutionSide.Buy, candidate.ClosedRowQuantity,
            candidate.ExitPrice, candidate.Commissions, candidate.Fees, null, null, candidate.ContractName);
        Trade trade = Trade.Start(accountId, instrumentId, candidate.Economics.Pricing!, entry, at);
        trade.AddExecution(exit, at);
        return trade;
    }

    private static string EconomicFingerprint(TopstepPreviewCandidate candidate)
    {
        TopstepSourceRow row = candidate.SourceRow;
        static string D(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);
        // File/line/order and decimal textual scale are deliberately excluded. Identity stays ordinal.
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Version = "topstep-row-v1", row.ContractName, row.Type, EntryUtcTicks = row.EnteredAtUtc.Ticks,
            ExitUtcTicks = row.ExitedAtUtc.Ticks, Entry = D(row.EntryPrice), Exit = D(row.ExitPrice), Quantity = D(row.Size),
            Gross = D(row.SourceReportedPnL), Fees = D(row.SourceReportedFees), Commission = D(row.SourceReportedCommissions),
            row.SourceTradeDay, DurationTicks = row.SourceReportedDuration.Ticks,
            PointValue = D(candidate.Economics.Pricing!.PointValue), candidate.Currency, candidate.Economics.CostInterpretation,
        });
        return Convert.ToHexString(SHA256.HashData(payload));
    }

    // Snapshots read on the SAME context/transaction, not the normal readers' independent contexts.
    private sealed class TransactionReferences(TradingAccountRecord? account, InstrumentRecord[] instruments) : ITradingAccountReader, IInstrumentReader
    {
        Task<TradingAccountDetails?> ITradingAccountReader.GetByIdAsync(Guid id, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(account?.Id == id ? new TradingAccountDetails(account.Id, account.Name, account.AccountType,
                account.ProviderName, account.ExternalAccountId, account.Currency, account.StartingBalance, account.IsActive,
                account.CreatedAtUtc, account.UpdatedAtUtc) : null);
        }
        Task<IReadOnlyList<InstrumentListItem>> IInstrumentReader.GetAllAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<InstrumentListItem>>(instruments.Select(i => new InstrumentListItem(i.Id,
                i.Symbol, i.DisplayName, i.AssetClass, i.Exchange, i.Currency, i.TickSize, i.TickValue,
                i.TickSize > 0 ? i.TickValue / i.TickSize : 0, i.IsActive)).ToArray());
        }
        Task<IReadOnlyList<AccountListItem>> ITradingAccountReader.GetAllAsync(CancellationToken token) => throw new NotSupportedException();
        Task<InstrumentDetails?> IInstrumentReader.GetByIdAsync(Guid id, CancellationToken token) => throw new NotSupportedException();
    }
}
