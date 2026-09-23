using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Imports.Tradovate;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Mapping;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Imports.Tradovate;

public sealed class TradovateImportStore : ITradovateImportStore
{
    private readonly IDbContextFactory<JournalDbContext> _contextFactory;

    public TradovateImportStore(IDbContextFactory<JournalDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        _contextFactory = contextFactory;
    }

    public async Task<TradovateImportResult> ImportAsync(
        TradovateImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Preparation);

        if (!request.Preparation.IsReadyForPreview ||
            request.Preparation.AccountSnapshot is null)
        {
            return TradovateImportResult.Blocked(
                TradovateImportConflictCodes.PreparationBlocked,
                "Tradovate import preparation is not ready.");
        }

        await using JournalDbContext context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);

        Guid accountId = request.Preparation.AccountSnapshot.TradingAccountId;
        bool accountExists = await context.TradingAccounts
            .AsNoTracking()
            .AnyAsync(record => record.Id == accountId, cancellationToken);
        if (!accountExists)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return TradovateImportResult.Blocked(
                TradovateImportConflictCodes.TradingAccountNotFound,
                $"Trading Account '{accountId}' no longer exists.");
        }

        TradovateImportedExecutionRecord[] persistedIdentities = await context
            .TradovateImportedExecutions
            .AsNoTracking()
            .Where(record => record.TradingAccountIdAtImport == accountId)
            .ToArrayAsync(cancellationToken);

        Classification classification = ClassifyCandidates(
            request.Preparation.PreparedCandidates,
            persistedIdentities);
        if (classification.ConflictMessage is not null)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return TradovateImportResult.Blocked(
                TradovateImportConflictCodes.DeduplicationConflict,
                classification.ConflictMessage);
        }

        if (classification.NewCandidates.Count == 0)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return new TradovateImportResult(
                TradovateImportStatus.NoChanges,
                0,
                classification.DuplicateTradeIds.Count,
                0,
                [],
                [],
                classification.DuplicateTradeIds);
        }

        InstrumentRecord[] currentInstruments = await context.Instruments
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);
        var instrumentByCanonical = new Dictionary<string, InstrumentRecord>(
            StringComparer.OrdinalIgnoreCase);
        var newInstrumentRecords = new List<InstrumentRecord>();

        foreach (TradovatePreparedTradeCandidate candidate in
                 classification.NewCandidates)
        {
            if (instrumentByCanonical.ContainsKey(candidate.CanonicalSymbol))
            {
                continue;
            }

            InstrumentResolution resolution = ResolveInstrument(
                candidate,
                request.Preparation,
                currentInstruments,
                request.ImportedAtUtc);
            if (resolution.ConflictMessage is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return TradovateImportResult.Blocked(
                    TradovateImportConflictCodes.ReferenceDataChanged,
                    resolution.ConflictMessage);
            }

            instrumentByCanonical.Add(candidate.CanonicalSymbol, resolution.Record!);
            if (resolution.WasCreated)
            {
                newInstrumentRecords.Add(resolution.Record!);
            }
        }

        var importedTradeIds = new List<Guid>();
        var tradeRecords = new List<TradeRecord>();
        var executionRecords = new List<TradeExecutionRecord>();
        var browseRecords = new List<TradeBrowseRecord>();
        var identityRecords = new List<TradovateImportedExecutionRecord>();

        foreach (TradovatePreparedTradeCandidate candidate in
                 classification.NewCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InstrumentRecord instrument = instrumentByCanonical[candidate.CanonicalSymbol];
            Trade trade = CreateTrade(
                candidate,
                accountId,
                instrument,
                request.ImportedAtUtc);

            importedTradeIds.Add(trade.Id);
            tradeRecords.Add(TradePersistenceMapper.ToRecord(trade));
            browseRecords.Add(TradeBrowsePersistenceMapper.ToRecord(trade));
            foreach (TradeExecution execution in trade.Executions)
            {
                executionRecords.Add(TradeExecutionPersistenceMapper.ToRecord(execution));
                identityRecords.Add(new TradovateImportedExecutionRecord
                {
                    TradeExecutionId = execution.Id,
                    TradeId = trade.Id,
                    TradingAccountIdAtImport = accountId,
                    BrokerSymbol = execution.BrokerSymbol!,
                    Side = execution.Side,
                    ExternalExecutionId = execution.ExternalExecutionId!,
                    ImportedAtUtc = request.ImportedAtUtc,
                });
            }
        }

        context.Instruments.AddRange(newInstrumentRecords);
        context.Trades.AddRange(tradeRecords);
        context.TradeExecutions.AddRange(executionRecords);
        context.TradeBrowse.AddRange(browseRecords);
        context.TradovateImportedExecutions.AddRange(identityRecords);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        return new TradovateImportResult(
            TradovateImportStatus.Imported,
            importedTradeIds.Count,
            classification.DuplicateTradeIds.Count,
            newInstrumentRecords.Count,
            importedTradeIds,
            newInstrumentRecords.Select(record => record.Id).ToArray(),
            classification.DuplicateTradeIds);
    }

    private static Classification ClassifyCandidates(
        IReadOnlyList<TradovatePreparedTradeCandidate> candidates,
        IReadOnlyList<TradovateImportedExecutionRecord> persisted)
    {
        var newCandidates = new List<TradovatePreparedTradeCandidate>();
        var duplicateTradeIds = new List<Guid>();
        var incomingOwners = new Dictionary<ExecutionIdentity, int>();

        for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            TradovatePreparedTradeCandidate candidate = candidates[candidateIndex];
            ExecutionIdentity[] keys = candidate.OrderedExecutions
                .Select(execution => new ExecutionIdentity(
                    candidate.TradingAccountId,
                    execution.BrokerSymbol,
                    execution.Side,
                    execution.ExternalFillId))
                .ToArray();
            if (keys.Distinct().Count() != keys.Length || keys.Any(key =>
                    incomingOwners.TryGetValue(key, out int owner) && owner != candidateIndex))
            {
                return Classification.Conflict(
                    "An external execution identity appears in more than one import candidate.");
            }

            foreach (ExecutionIdentity key in keys)
            {
                incomingOwners[key] = candidateIndex;
            }

            TradovateImportedExecutionRecord[] matches = persisted
                .Where(record => keys.Contains(ExecutionIdentity.From(record)))
                .ToArray();
            if (matches.Length == 0)
            {
                newCandidates.Add(candidate);
                continue;
            }

            if (matches.Length != keys.Length ||
                matches.Select(record => record.TradeId).Distinct().Count() != 1)
            {
                return Classification.Conflict(
                    "The import overlaps an incomplete or cross-Trade execution identity set.");
            }

            Guid tradeId = matches[0].TradeId;
            ExecutionIdentity[] persistedTradeKeys = persisted
                .Where(record => record.TradeId == tradeId)
                .Select(ExecutionIdentity.From)
                .ToArray();
            if (persistedTradeKeys.Length != keys.Length ||
                !persistedTradeKeys.ToHashSet().SetEquals(keys))
            {
                return Classification.Conflict(
                    "The persisted Trade identity set does not exactly match the import candidate.");
            }

            duplicateTradeIds.Add(tradeId);
        }

        return new Classification(
            newCandidates,
            duplicateTradeIds.Distinct().ToArray(),
            null);
    }

    private static InstrumentResolution ResolveInstrument(
        TradovatePreparedTradeCandidate candidate,
        TradovateImportPreparationResult preparation,
        IReadOnlyList<InstrumentRecord> currentInstruments,
        DateTimeOffset importedAtUtc)
    {
        TradovateCanonicalInstrumentResolution expected = preparation
            .InstrumentResolution.CanonicalInstrumentResolutions
            .Single(resolution => string.Equals(
                resolution.CanonicalSymbol,
                candidate.CanonicalSymbol,
                StringComparison.Ordinal));

        if (candidate.ExistingInstrumentId.HasValue)
        {
            InstrumentRecord? current = currentInstruments.SingleOrDefault(record =>
                record.Id == candidate.ExistingInstrumentId.Value);
            if (current is null || expected.ExistingInstrument is null ||
                !MateriallyMatches(current, candidate.CanonicalSymbol, expected.ExistingInstrument))
            {
                return InstrumentResolution.Conflict(
                    $"Instrument '{candidate.CanonicalSymbol}' changed after preview.");
            }

            return new InstrumentResolution(current, false, null);
        }

        TradovateInstrumentCreationProposal proposal =
            candidate.InstrumentCreationProposal!;
        InstrumentRecord[] matches = currentInstruments
            .Where(record => string.Equals(
                record.Symbol,
                candidate.CanonicalSymbol,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length > 1)
        {
            return InstrumentResolution.Conflict(
                $"Instrument '{candidate.CanonicalSymbol}' is no longer uniquely resolved.");
        }

        if (matches.Length == 1)
        {
            if (!MateriallyMatches(matches[0], proposal))
            {
                return InstrumentResolution.Conflict(
                    $"Instrument '{candidate.CanonicalSymbol}' has conflicting economics.");
            }

            return new InstrumentResolution(matches[0], false, null);
        }

        var instrument = new Instrument(
            proposal.CanonicalSymbol,
            proposal.DisplayName,
            proposal.AssetClass,
            proposal.Exchange,
            proposal.Currency,
            proposal.TickSize,
            proposal.TickValue,
            importedAtUtc);
        return new InstrumentResolution(
            InstrumentPersistenceMapper.ToRecord(instrument),
            true,
            null);
    }

    private static bool MateriallyMatches(
        InstrumentRecord current,
        string canonicalSymbol,
        TradovateExistingInstrumentSnapshot expected) =>
        string.Equals(current.Symbol, canonicalSymbol, StringComparison.OrdinalIgnoreCase) &&
        current.AssetClass == expected.AssetClass &&
        string.Equals(current.Currency, expected.Currency, StringComparison.OrdinalIgnoreCase) &&
        current.TickSize == expected.TickSize &&
        current.TickValue == expected.TickValue;

    private static bool MateriallyMatches(
        InstrumentRecord current,
        TradovateInstrumentCreationProposal expected) =>
        string.Equals(current.Symbol, expected.CanonicalSymbol, StringComparison.OrdinalIgnoreCase) &&
        current.AssetClass == expected.AssetClass &&
        string.Equals(current.Currency, expected.Currency, StringComparison.OrdinalIgnoreCase) &&
        current.TickSize == expected.TickSize &&
        current.TickValue == expected.TickValue;

    private static Trade CreateTrade(
        TradovatePreparedTradeCandidate candidate,
        Guid accountId,
        InstrumentRecord instrument,
        DateTimeOffset importedAtUtc)
    {
        Guid tradeId = Guid.NewGuid();
        TradeExecution[] executions = candidate.OrderedExecutions
            .Select((execution, index) => new TradeExecution(
                tradeId,
                index + 1,
                execution.ExecutedAtUtc,
                execution.Side,
                execution.Quantity,
                execution.Price,
                commission: null,
                fees: null,
                execution.ExternalFillId,
                externalOrderId: null,
                execution.BrokerSymbol))
            .ToArray();
        var pricing = new TradePricingSnapshot(
            instrument.TickValue / instrument.TickSize,
            instrument.Currency);
        Trade trade = Trade.Start(
            accountId,
            instrument.Id,
            pricing,
            executions[0],
            importedAtUtc);
        foreach (TradeExecution execution in executions.Skip(1))
        {
            trade.AddExecution(execution, importedAtUtc);
        }

        return trade;
    }

    private sealed record Classification(
        IReadOnlyList<TradovatePreparedTradeCandidate> NewCandidates,
        IReadOnlyList<Guid> DuplicateTradeIds,
        string? ConflictMessage)
    {
        public static Classification Conflict(string message) => new([], [], message);
    }

    private sealed record InstrumentResolution(
        InstrumentRecord? Record,
        bool WasCreated,
        string? ConflictMessage)
    {
        public static InstrumentResolution Conflict(string message) =>
            new(null, false, message);
    }

    private readonly record struct ExecutionIdentity(
        Guid TradingAccountId,
        string BrokerSymbol,
        ExecutionSide Side,
        string ExternalExecutionId)
    {
        public static ExecutionIdentity From(TradovateImportedExecutionRecord record) =>
            new(
                record.TradingAccountIdAtImport,
                record.BrokerSymbol,
                record.Side,
                record.ExternalExecutionId);
    }
}
