using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Domain.TradingPlans;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.TradingPlans;

public sealed class TradingPlanStore(IDbContextFactory<JournalDbContext> factory, TimeProvider? clock = null,
    PersonalTradingJournal.Application.Setups.IPlaybookImageStorage? images = null) : ITradingPlanStore
{
    public async Task<TradingPlan?> LoadAsync(TradingPlanKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var tx = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: true);
        await db.Database.UseTransactionAsync(tx, cancellationToken);
        var row = await db.TradingPlans.AsNoTracking().SingleOrDefaultAsync(x => x.DeletedAtUtc == null && x.AccountId == key.AccountId && x.NewYorkDate == key.NewYorkDate, cancellationToken);
        return row is null ? null : await Read(db, row, cancellationToken);
    }

    public async Task<TradingPlanReadyRevision?> LoadReadyAsync(TradingPlanRevisionId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await ReadReady(db, id, cancellationToken);
    }

    public async Task<IReadOnlyList<TradingPlanLoadedState>> HistoryAsync(TradingPlanHistoryQuery query,
        DateOnly? beforeDate = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var tx = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: true);
        await db.Database.UseTransactionAsync(tx, cancellationToken);
        var rows = await db.TradingPlans.AsNoTracking().Where(x => x.DeletedAtUtc == null && x.AccountId == query.AccountId &&
            x.NewYorkDate >= query.FirstDate && x.NewYorkDate <= query.LastDate && (!beforeDate.HasValue || x.NewYorkDate < beforeDate.Value) &&
            (!query.Status.HasValue || (query.Status == TradingPlanStatus.Draft ? x.DraftJson != null : x.DraftJson == null)))
            .OrderByDescending(x => x.NewYorkDate).Take(query.Limit)
            .ToArrayAsync(cancellationToken);
        var ids = rows.Select(x => x.Id).ToArray();
        // A bounded batch, never one query per Plan or every Ready revision.
        var latestRows = await ReadyRows(db, db.TradingPlanReadyRevisions.Where(r => ids.Contains(r.PlanId) &&
            db.TradingPlans.Any(p => p.Id == r.PlanId && p.LatestReadyRevision == r.Revision))).ToArrayAsync(cancellationToken);
        var latest = latestRows.Select(ReadOwnedReady).ToDictionary(r => r.Id.PlanId);
        var drafts = rows.Where(x => x.DraftJson != null).ToDictionary(x => x.Id, x => TradingPlanJson.ReadDraft(x.DraftJson!));
        var account = await db.TradingAccounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == query.AccountId, cancellationToken);
        var instrumentIds = drafts.Values.SelectMany(d => d.Content.Instruments).Select(i => i.InstrumentId).Distinct().ToArray();
        var instruments = await db.Instruments.AsNoTracking().Where(i => instrumentIds.Contains(i.Id))
            .Select(i => new { i.Id, i.Symbol }).ToDictionaryAsync(i => i.Id, i => i.Symbol, cancellationToken);
        return rows.Select(x =>
        {
            var ready = latest.GetValueOrDefault(x.Id);
            if (x.LatestReadyRevision > 0 && ready is null) throw new InvalidDataException("Missing Ready history.");
            var draft = drafts.GetValueOrDefault(x.Id);
            var content = draft?.Content ?? ready?.Content ?? throw new InvalidDataException("Missing Plan content.");
            var labels = draft is null ? ready!.References.Instruments.Select(i => i.Symbol).ToArray() :
                content.Instruments.Select(i => instruments.GetValueOrDefault(i.InstrumentId) ?? "Unavailable Instrument").ToArray();
            return new TradingPlanLoadedState(new(x.AccountId, x.NewYorkDate), new(x.Id, x.Version),
                draft is not null ? TradingPlanStatus.Draft : TradingPlanStatus.Ready, x.LatestReadyRevision)
            {
                Summary = new(draft is null ? ready!.References.Account.Name : x.AccountId is null ? "All Accounts" : account?.Name ?? "Unavailable Account",
                    draft is null ? ready!.References.Account.Currency : x.AccountId is null ? content.PlanCurrency! : account?.Currency ?? "Unknown currency", x.AccountId is null || account is not null,
                    content.Session, [.. labels], draft?.UpdatedAtUtc ?? ready!.ReadyAtUtc)
            };
        }).ToArray();
    }

    public async Task<IReadOnlyList<TradingPlanReadyRevision>> ReadyHistoryAsync(Guid planId, int? beforeRevision = null,
        int limit = 20, CancellationToken cancellationToken = default)
    {
        if (planId == Guid.Empty || limit is < 1 or > 100 || beforeRevision is < 1) throw new ArgumentException("Invalid history query.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var selected = db.TradingPlanReadyRevisions.Where(x => x.PlanId == planId &&
            (!beforeRevision.HasValue || x.Revision < beforeRevision.Value)).OrderByDescending(x => x.Revision).Take(limit);
        var rows = await ReadyRows(db, selected).ToArrayAsync(cancellationToken);
        return rows.OrderByDescending(x => x.Row.Revision).Select(ReadOwnedReady).ToArray();
    }

    public async Task<IReadOnlyList<TradingPlanHistoryAccount>> HistoryAccountsAsync(DateOnly firstDate, DateOnly lastDate,
        int offset = 0, int limit = 21, CancellationToken cancellationToken = default)
    {
        if (firstDate > lastDate || offset is < 0 or > 100_000 || limit is < 1 or > 100) throw new ArgumentException("Invalid Account discovery query.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var tx = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: true);
        await db.Database.UseTransactionAsync(tx, cancellationToken);
        var inRange = db.TradingPlans.Where(p => p.DeletedAtUtc == null && p.NewYorkDate >= firstDate && p.NewYorkDate <= lastDate);
        var ids = await inRange.Select(p => p.AccountId).Distinct().OrderBy(id => id).Skip(offset).Take(limit).ToArrayAsync(cancellationToken);
        var current = await db.TradingAccounts.AsNoTracking().Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
        var latestPlans = inRange.Where(p => ids.Contains(p.AccountId) && p.LatestReadyRevision > 0 &&
            !inRange.Any(n => n.AccountId == p.AccountId && n.LatestReadyRevision > 0 && n.NewYorkDate > p.NewYorkDate));
        var readyRows = await ReadyRows(db, db.TradingPlanReadyRevisions.Where(r => latestPlans.Any(p => p.Id == r.PlanId && p.LatestReadyRevision == r.Revision))).ToArrayAsync(cancellationToken);
        var saved = readyRows.Select(ReadOwnedReady).ToArray();
        return ids.Select(id =>
        {
            var a = id is { } exact ? current.GetValueOrDefault(exact) : null; var s = saved.FirstOrDefault(r => r.Key.AccountId == id)?.References.Account;
            if (id is null) return new TradingPlanHistoryAccount(null, "All Accounts", s?.Currency ?? "Plan currency", true, true);
            return new TradingPlanHistoryAccount(id, a?.Name ?? s?.Name ?? "Unavailable Account (no saved name)",
                a?.Currency ?? s?.Currency ?? "Unknown currency", a is not null, a?.IsActive == true);
        }).ToArray();
    }

    public async Task<TradingPlanWriteResult> WriteAsync(TradingPlanWriteCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            await db.Database.OpenConnectionAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await using var tx = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
            await db.Database.UseTransactionAsync(tx, cancellationToken);
            var row = await db.TradingPlans.SingleOrDefaultAsync(x => x.DeletedAtUtc == null && x.AccountId == command.Key.AccountId && x.NewYorkDate == command.Key.NewYorkDate, cancellationToken);
            var plan = row is null ? null : await Read(db, row, cancellationToken);
            var conflict = command.CheckTransition(plan is null ? null : TradingPlanLoadedState.FromPlan(plan));
            if (conflict.HasValue) return new(conflict.Value);
            var now = (clock ?? TimeProvider.System).GetUtcNow();
            if (command.Operation == TradingPlanWriteOperation.Delete)
            {
                if (now < row!.UpdatedAtUtc) return new(TradingPlanWriteStatus.Invalid);
                // Tombstone only: owner audit/version and immutable Ready/assets remain exact.
                row!.DeletedAtUtc = now;
                await db.SaveChangesAsync(cancellationToken);
                if (!await db.TradingPlans.AsNoTracking().AnyAsync(p => p.Id == row.Id && p.DeletedAtUtc == now, cancellationToken))
                    return new(TradingPlanWriteStatus.StorageFailure);
                cancellationToken.ThrowIfCancellationRequested();
                await db.Database.CommitTransactionAsync(CancellationToken.None);
                return new(TradingPlanWriteStatus.Success, DeletedPlanId: row.Id);
            }
            var content = command.Content;
            DateOnly? prefilled = null;
            string? inheritedTokens = null;
            bool compareInheritedTokens = false;
            if (command.Operation == TradingPlanWriteOperation.CreateDraft)
            {
                var previous = await db.TradingPlans.AsNoTracking().Where(p => p.DeletedAtUtc == null &&
                    p.AccountId == command.Key.AccountId && p.NewYorkDate < command.Key.NewYorkDate)
                    .OrderByDescending(p => p.NewYorkDate).FirstOrDefaultAsync(cancellationToken);
                if (previous is not null)
                {
                    var source = await Read(db, previous, cancellationToken);
                    var saved = source.WorkingDraft?.Content ?? source.LatestReadyRevision!.Content;
                    var account = await db.TradingAccounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == command.Key.AccountId, cancellationToken);
                    // Legacy Drafts did not freeze currency. Only use today's label if its
                    // saved Account fingerprint matches; otherwise retain unset for correction.
                    var baseline = previous.ReferenceStateJson is null ? null :
                        TradingPlanJson.Deserialize<Dictionary<string, string>>(previous.ReferenceStateJson);
                    var currency = saved.PlanCurrency ?? (source.WorkingDraft is null ? source.LatestReadyRevision!.References.Account.Currency :
                        account is not null && baseline?.GetValueOrDefault($"1:{account.Id:D}") == TradingPlanReferences.Fingerprint(account) ? account.Currency : null);
                    content = saved.PrefillPreviousDay(currency);
                    prefilled = previous.NewYorkDate;
                    compareInheritedTokens = previous.ReferenceStateJson is not null;
                    inheritedTokens = previous.ReferenceStateJson ?? TradingPlanJson.Write(TradingPlanReferences.UnresolvedTokens(command.Key, content));
                }
            }
            string? derivedCurrency = null;
            var op = command.Operation;
            if (op is TradingPlanWriteOperation.CopyForward or TradingPlanWriteOperation.StartDraft)
            {
                var source = await ReadReady(db, command.SourceRevision!, cancellationToken);
                if (source is null) return new(TradingPlanWriteStatus.Conflict);
                derivedCurrency = source.References.Account.Currency;
                // Legacy Ready content may omit PlanCurrency. Derive only from its frozen
                // Account snapshot, never today's Account label; the source itself is untouched.
                if (op == TradingPlanWriteOperation.CopyForward)
                {
                    content = source.Content.CopyForward();
                    if (content.PlanCurrency is null && (content.RiskLimits.RiskPerTrade.HasValue || content.RiskLimits.DailyLossLimit.HasValue))
                        content = content.WithPlanCurrency(derivedCurrency);
                }
                else { plan!.StartDraft(plan.Version, source, now); content = plan.WorkingDraft!.Content; }
            }
            if (plan is null) plan = new(command.Key, content, now);
            else if (op == TradingPlanWriteOperation.SaveDraft)
            {
                plan.EditDraft(plan.Version, content, now);
                // Legacy confirmation fields are read-only compatibility data.
            }
            else if (op == TradingPlanWriteOperation.RefreshReferences)
            {
                plan.EditDraft(plan.Version, content, now); plan.RefreshReferences(plan.Version, now);
            }
            else if (op == TradingPlanWriteOperation.MarkReady)
            {
                if (!plan.WorkingDraft!.Content.HasSamePreparation(content)) return new(TradingPlanWriteStatus.Conflict);
                // Old editable Draft flags are not new confirmation evidence. Immutable Ready
                // revisions are untouched; clearing legacy Draft flags is part of this transaction.
                plan.EditDraft(plan.Version, content, now);
            }

            TradingPlanReferences? refs = null;
            TradingPlanReferenceIssue? pendingReference = null;
            try
            {
                refs = await TradingPlanReferences.Resolve(db, command.Key, plan.WorkingDraft!.Content, now, cancellationToken);
                if (prefilled.HasValue && compareInheritedTokens && inheritedTokens is not null) refs.CheckPrevious(inheritedTokens);
            }
            catch (PlanReferenceException e) when (prefilled.HasValue)
            {
                // Preserve copied prose and exact links for correction, never claim verified Ready.
                refs = null; pendingReference = e.Issue;
            }
            bool monetary = content.RiskLimits.RiskPerTrade.HasValue || content.RiskLimits.DailyLossLimit.HasValue;
            if (!prefilled.HasValue && (command.ExpectedAccountCurrency is { } reviewed && reviewed != refs!.Snapshot.Account.Currency ||
                op == TradingPlanWriteOperation.CopyForward && monetary && derivedCurrency is { } priorCurrency && priorCurrency != refs!.Snapshot.Account.Currency && command.ExpectedAccountCurrency is null))
                return new(TradingPlanWriteStatus.ReferenceChanged, References: [new(TradingPlanReferenceKind.Account, command.Key.AccountId,
                    TradingPlanReferenceState.Changed, "Review the changed Account currency explicitly; planned amounts are not converted.")]);
            if (op == TradingPlanWriteOperation.RefreshReferences && command.Key.AccountId is { } exactAccount &&
                monetary && content.PlanCurrency is null && row?.ReferenceStateJson is { } previousTokens &&
                TradingPlanJson.Deserialize<Dictionary<string, string>>(previousTokens).GetValueOrDefault($"1:{exactAccount:D}") != refs!.Tokens[$"1:{exactAccount:D}"])
                return new(TradingPlanWriteStatus.ReferenceChanged, References: [new(TradingPlanReferenceKind.Account, exactAccount,
                    TradingPlanReferenceState.Changed, "Enter the planned currency and correct the monetary amounts before refreshing the changed Account.")]);
            if (row?.ReferenceStateJson is not null && op != TradingPlanWriteOperation.RefreshReferences)
                refs!.CheckPrevious(row.ReferenceStateJson);
            TradingPlanReadyRevision? ready = null;
            if (op == TradingPlanWriteOperation.MarkReady && monetary && content.PlanCurrency is { } retainedCurrency &&
                retainedCurrency != refs!.Snapshot.Account.Currency)
                return new(TradingPlanWriteStatus.ReferenceChanged, References: [new(TradingPlanReferenceKind.Account, command.Key.AccountId,
                    TradingPlanReferenceState.Changed, "Planned currency differs from the Account. Correct currency and amounts explicitly; no conversion is performed.")]);
            if (op == TradingPlanWriteOperation.MarkReady) ready = plan.MarkReady(plan.Version, refs!.Snapshot, now);
            bool creating = row is null;
            row ??= new() { Id = plan.Id, AccountId = plan.Key.AccountId, NewYorkDate = plan.Key.NewYorkDate, CreatedAtUtc = plan.CreatedAtUtc };
            row.Version = plan.Version; row.UpdatedAtUtc = plan.UpdatedAtUtc;
            row.LatestReadyRevision = plan.LatestReadyRevision?.Id.Revision ?? 0;
            row.DraftJson = plan.WorkingDraft is null ? null : TradingPlanJson.WriteDraft(plan.WorkingDraft);
            row.ReferenceStateJson = plan.WorkingDraft is null ? null : (refs is null ? inheritedTokens : TradingPlanJson.Write(refs.Tokens));
            if (creating) db.TradingPlans.Add(row);
            foreach (var attachment in content.Screenshots)
            {
                var image = attachment.Image;
                var prior = await db.TradingPlanAssets.AsNoTracking().SingleOrDefaultAsync(a => a.Id == attachment.Id, cancellationToken);
                if (prior is not null)
                {
                    if (prior.PlanId != plan.Id || prior.InstrumentId != attachment.InstrumentId || prior.Timeframe != (int)attachment.Timeframe ||
                        prior.StorageKey != image.StorageKey || prior.FileName != image.FileName || prior.SizeBytes != image.SizeBytes || prior.Sha256 != image.Sha256)
                        return new(TradingPlanWriteStatus.Invalid);
                }
                else
                {
                    if (images is null) return new(TradingPlanWriteStatus.StorageFailure);
                    _ = await images.ReadAsync(image, cancellationToken); // Verify original bytes/hash before accepting an asset identity.
                    db.TradingPlanAssets.Add(new() { Id = attachment.Id, PlanId = plan.Id, InstrumentId = attachment.InstrumentId,
                        Timeframe = (int)attachment.Timeframe, StorageKey = image.StorageKey, FileName = image.FileName, SizeBytes = image.SizeBytes, Sha256 = image.Sha256 });
                }
            }
            if (ready is not null) db.TradingPlanReadyRevisions.Add(new() { PlanId = plan.Id, Revision = ready.Id.Revision,
                ReadyAtUtc = ready.ReadyAtUtc, SnapshotJson = TradingPlanJson.WriteReady(ready) });
            await db.SaveChangesAsync(cancellationToken);
            // Verify reconstruction from database inside the same transaction before reporting success.
            db.ChangeTracker.Clear();
            var persisted = await db.TradingPlans.AsNoTracking().SingleAsync(x => x.Id == plan.Id, cancellationToken);
            var result = await Read(db, persisted, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await db.Database.CommitTransactionAsync(CancellationToken.None);
            return new(TradingPlanWriteStatus.Success, result, References: pendingReference is null ? [] : [pendingReference],
                VerifiedReferences: refs?.Snapshot, PrefilledFromDate: prefilled);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return new(TradingPlanWriteStatus.Cancelled); }
        catch (TradingPlanNotReadyException ex) { return new(TradingPlanWriteStatus.Invalid, Issues: ex.Issues); }
        catch (PlanReferenceException ex) { return new(ex.Issue.State == TradingPlanReferenceState.Changed
            ? TradingPlanWriteStatus.ReferenceChanged : TradingPlanWriteStatus.ReferenceUnavailable, References: [ex.Issue]); }
        catch (DbUpdateConcurrencyException) { return new(TradingPlanWriteStatus.Conflict); }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: 1555 or 2067 })
        { return new(command.Expected is null ? TradingPlanWriteStatus.TargetAlreadyExists : TradingPlanWriteStatus.Conflict); }
        catch (Exception ex) when (ex is SqliteException or DbUpdateException or InvalidDataException or IOException or UnauthorizedAccessException)
        { return new(TradingPlanWriteStatus.StorageFailure); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
        { return new(TradingPlanWriteStatus.Invalid); }
    }

    private static async Task<TradingPlan> Read(JournalDbContext db, TradingPlanRecord row, CancellationToken ct)
    {
        var sequence = await db.TradingPlanReadyRevisions.Where(x => x.PlanId == row.Id).GroupBy(x => x.PlanId)
            .Select(g => new { Count = g.Count(), Max = g.Max(x => x.Revision) }).SingleOrDefaultAsync(ct);
        if ((sequence?.Count ?? 0) != row.LatestReadyRevision || (sequence?.Max ?? 0) != row.LatestReadyRevision)
            throw new InvalidDataException("Inconsistent preserved Ready sequence.");
        var latest = row.LatestReadyRevision == 0 ? null : await ReadReady(db, new(row.Id, row.LatestReadyRevision), ct)
            ?? throw new InvalidDataException("Missing preserved Ready revision.");
        if ((row.DraftJson is null) != (row.ReferenceStateJson is null)) throw new InvalidDataException("Invalid Draft reference state.");
        try
        {
            var draft = row.DraftJson is null ? null : TradingPlanJson.ReadDraft(row.DraftJson);
            if (draft is not null) TradingPlanReferences.ValidateStored(row.AccountId, draft.Content, row.ReferenceStateJson!);
            return TradingPlan.Rehydrate(row.Id, new(row.AccountId, row.NewYorkDate), row.Version,
                draft, latest, row.CreatedAtUtc, row.UpdatedAtUtc);
        }
        catch (ArgumentException) { throw new InvalidDataException("Invalid persisted Trading Plan."); }
    }
    private static async Task<TradingPlanReadyRevision?> ReadReady(JournalDbContext db, TradingPlanRevisionId id, CancellationToken ct)
    {
        var row = await ReadyRows(db, db.TradingPlanReadyRevisions.Where(x => x.PlanId == id.PlanId && x.Revision == id.Revision)).SingleOrDefaultAsync(ct);
        return row is null ? null : ReadOwnedReady(row);
    }
    private sealed record OwnedReady(TradingPlanReadyRecord Row, Guid? AccountId, DateOnly NewYorkDate,
        DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, long Version, int LatestReadyRevision);
    private static IQueryable<OwnedReady> ReadyRows(JournalDbContext db, IQueryable<TradingPlanReadyRecord> selected) =>
        selected.AsNoTracking().Join(db.TradingPlans.AsNoTracking(), r => r.PlanId, p => p.Id,
            (r, p) => new OwnedReady(r, p.AccountId, p.NewYorkDate, p.CreatedAtUtc, p.UpdatedAtUtc, p.Version, p.LatestReadyRevision));
    private static TradingPlanReadyRevision ReadOwnedReady(OwnedReady owner)
    {
        var ready = ReadReady(owner.Row);
        if (ready.Key.AccountId != owner.AccountId || ready.Key.NewYorkDate != owner.NewYorkDate ||
            ready.PlanCreatedAtUtc != owner.CreatedAtUtc || ready.ReadyAtUtc > owner.UpdatedAtUtc ||
            ready.SourceDraftVersion >= owner.Version || ready.Id.Revision > owner.LatestReadyRevision)
            throw new InvalidDataException("Ready evidence does not match its Plan owner.");
        return ready;
    }
    private static TradingPlanReadyRevision ReadReady(TradingPlanReadyRecord row)
    {
        var value = TradingPlanJson.ReadReady(row.SnapshotJson);
        if (value.Id.PlanId != row.PlanId || value.Id.Revision != row.Revision || value.ReadyAtUtc != row.ReadyAtUtc)
            throw new InvalidDataException("Ready row does not match its immutable payload.");
        return value;
    }
}
