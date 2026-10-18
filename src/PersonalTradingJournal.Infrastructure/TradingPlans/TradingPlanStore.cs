using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Domain.TradingPlans;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.TradingPlans;

public sealed class TradingPlanStore(IDbContextFactory<JournalDbContext> factory, TimeProvider? clock = null) : ITradingPlanStore
{
    public async Task<TradingPlan?> LoadAsync(TradingPlanKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var tx = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: true);
        await db.Database.UseTransactionAsync(tx, cancellationToken);
        var row = await db.TradingPlans.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == key.AccountId && x.NewYorkDate == key.NewYorkDate, cancellationToken);
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
        var rows = await db.TradingPlans.AsNoTracking().Where(x => x.AccountId == query.AccountId &&
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
                Summary = new(draft is null ? ready!.References.Account.Name : account?.Name ?? "Unavailable Account",
                    draft is null ? ready!.References.Account.Currency : account?.Currency ?? "Unknown currency", account is not null,
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
        var inRange = db.TradingPlans.Where(p => p.NewYorkDate >= firstDate && p.NewYorkDate <= lastDate);
        var ids = await inRange.Select(p => p.AccountId).Distinct().OrderBy(id => id).Skip(offset).Take(limit).ToArrayAsync(cancellationToken);
        var current = await db.TradingAccounts.AsNoTracking().Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
        var latestPlans = inRange.Where(p => ids.Contains(p.AccountId) && p.LatestReadyRevision > 0 &&
            !inRange.Any(n => n.AccountId == p.AccountId && n.LatestReadyRevision > 0 && n.NewYorkDate > p.NewYorkDate));
        var readyRows = await ReadyRows(db, db.TradingPlanReadyRevisions.Where(r => latestPlans.Any(p => p.Id == r.PlanId && p.LatestReadyRevision == r.Revision))).ToArrayAsync(cancellationToken);
        var saved = readyRows.Select(ReadOwnedReady).ToDictionary(r => r.Key.AccountId, r => r.References.Account);
        return ids.Select(id =>
        {
            var a = current.GetValueOrDefault(id); var s = saved.GetValueOrDefault(id);
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
            var row = await db.TradingPlans.SingleOrDefaultAsync(x => x.AccountId == command.Key.AccountId && x.NewYorkDate == command.Key.NewYorkDate, cancellationToken);
            var plan = row is null ? null : await Read(db, row, cancellationToken);
            var conflict = command.CheckTransition(plan is null ? null : TradingPlanLoadedState.FromPlan(plan));
            if (conflict.HasValue) return new(conflict.Value);
            var now = (clock ?? TimeProvider.System).GetUtcNow();
            var content = command.Content;
            string? derivedCurrency = null;
            var op = command.Operation;
            if (op is TradingPlanWriteOperation.CopyForward or TradingPlanWriteOperation.StartDraft)
            {
                var source = await ReadReady(db, command.SourceRevision!, cancellationToken);
                if (source is null) return new(TradingPlanWriteStatus.Conflict);
                derivedCurrency = source.References.Account.Currency;
                if (op == TradingPlanWriteOperation.CopyForward) content = source.Content.CopyForward();
                else { plan!.StartDraft(plan.Version, source, now); content = plan.WorkingDraft!.Content; }
            }
            if (plan is null) plan = new(command.Key, content, now);
            else if (op == TradingPlanWriteOperation.SaveDraft)
            {
                plan.EditDraft(plan.Version, content, now);
                if (command.ExplicitConfirmations is { } checks) plan.SetConfirmations(plan.Version, checks, now);
            }
            else if (op == TradingPlanWriteOperation.RefreshReferences)
            {
                plan.EditDraft(plan.Version, content, now); plan.RefreshReferences(plan.Version, now);
            }
            else if (op == TradingPlanWriteOperation.ConfirmChecklist)
            {
                // Confirmation cannot smuggle a simultaneous preparation edit past checklist invalidation.
                var proposed = TradingPlanDraft.Rehydrate(content, plan.WorkingDraft!.DerivedFromRevision,
                    plan.WorkingDraft.StartedAtUtc, plan.WorkingDraft.UpdatedAtUtc);
                var comparable = new TradingPlanContent(content.Session, content.Focus, content.Intent, content.Instruments,
                    content.Scenarios, content.NewsAndRestrictions, content.StandAside, content.RiskLimits, content.Rules,
                    plan.WorkingDraft.Content.Confirmed);
                if (TradingPlanJson.WriteContent(comparable) != TradingPlanJson.WriteContent(plan.WorkingDraft.Content))
                    return new(TradingPlanWriteStatus.Invalid);
                plan.SetConfirmations(plan.Version, proposed.Content.Confirmed, now);
            }
            else if (op == TradingPlanWriteOperation.MarkReady &&
                TradingPlanJson.WriteContent(content) != TradingPlanJson.WriteContent(plan.WorkingDraft!.Content))
                return new(TradingPlanWriteStatus.Conflict);

            var refs = await TradingPlanReferences.Resolve(db, command.Key, plan.WorkingDraft!.Content, now, cancellationToken);
            bool monetary = content.RiskLimits.RiskPerTrade.HasValue || content.RiskLimits.DailyLossLimit.HasValue;
            if (command.ExpectedAccountCurrency is { } reviewed && reviewed != refs.Snapshot.Account.Currency ||
                monetary && derivedCurrency is { } priorCurrency && priorCurrency != refs.Snapshot.Account.Currency && command.ExpectedAccountCurrency is null)
                return new(TradingPlanWriteStatus.ReferenceChanged, References: [new(TradingPlanReferenceKind.Account, command.Key.AccountId,
                    TradingPlanReferenceState.Changed, "Review the changed Account currency explicitly; planned amounts are not converted.")]);
            if (monetary && op == TradingPlanWriteOperation.RefreshReferences && command.ExpectedAccountCurrency is null && row?.ReferenceStateJson is { } baseline &&
                TradingPlanJson.Deserialize<Dictionary<string, string>>(baseline)[$"1:{command.Key.AccountId:D}"] != refs.Tokens[$"1:{command.Key.AccountId:D}"])
                return new(TradingPlanWriteStatus.ReferenceChanged, References: [new(TradingPlanReferenceKind.Account, command.Key.AccountId,
                    TradingPlanReferenceState.Changed, "Review the current Account currency before refreshing monetary preparation.")]);
            if (row?.ReferenceStateJson is not null && op != TradingPlanWriteOperation.RefreshReferences)
                refs.CheckPrevious(row.ReferenceStateJson);
            TradingPlanReadyRevision? ready = null;
            if (op == TradingPlanWriteOperation.MarkReady) ready = plan.MarkReady(plan.Version, refs.Snapshot, now);
            bool creating = row is null;
            row ??= new() { Id = plan.Id, AccountId = plan.Key.AccountId, NewYorkDate = plan.Key.NewYorkDate, CreatedAtUtc = plan.CreatedAtUtc };
            row.Version = plan.Version; row.UpdatedAtUtc = plan.UpdatedAtUtc;
            row.LatestReadyRevision = plan.LatestReadyRevision?.Id.Revision ?? 0;
            row.DraftJson = plan.WorkingDraft is null ? null : TradingPlanJson.WriteDraft(plan.WorkingDraft);
            row.ReferenceStateJson = plan.WorkingDraft is null ? null : TradingPlanJson.Write(refs.Tokens);
            if (creating) db.TradingPlans.Add(row);
            if (ready is not null) db.TradingPlanReadyRevisions.Add(new() { PlanId = plan.Id, Revision = ready.Id.Revision,
                ReadyAtUtc = ready.ReadyAtUtc, SnapshotJson = TradingPlanJson.WriteReady(ready) });
            await db.SaveChangesAsync(cancellationToken);
            // Verify reconstruction from database inside the same transaction before reporting success.
            db.ChangeTracker.Clear();
            var persisted = await db.TradingPlans.AsNoTracking().SingleAsync(x => x.Id == plan.Id, cancellationToken);
            var result = await Read(db, persisted, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await db.Database.CommitTransactionAsync(CancellationToken.None);
            return new(TradingPlanWriteStatus.Success, result, VerifiedReferences: refs.Snapshot);
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
    private sealed record OwnedReady(TradingPlanReadyRecord Row, Guid AccountId, DateOnly NewYorkDate,
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
