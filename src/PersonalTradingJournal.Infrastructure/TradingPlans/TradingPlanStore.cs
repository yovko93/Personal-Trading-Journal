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
        var rows = await db.TradingPlans.AsNoTracking().Where(x => x.AccountId == query.AccountId &&
            x.NewYorkDate >= query.FirstDate && x.NewYorkDate <= query.LastDate && (!beforeDate.HasValue || x.NewYorkDate < beforeDate.Value) &&
            (!query.Status.HasValue || (query.Status == TradingPlanStatus.Draft ? x.DraftJson != null : x.DraftJson == null)))
            .OrderByDescending(x => x.NewYorkDate).Take(query.Limit)
            .Select(x => new { x.Id, x.AccountId, x.NewYorkDate, x.Version, x.LatestReadyRevision, HasDraft = x.DraftJson != null }).ToArrayAsync(cancellationToken);
        return rows.Select(x => new TradingPlanLoadedState(new(x.AccountId, x.NewYorkDate), new(x.Id, x.Version),
            x.HasDraft ? TradingPlanStatus.Draft : TradingPlanStatus.Ready, x.LatestReadyRevision)).ToArray();
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
            var op = command.Operation;
            if (op is TradingPlanWriteOperation.CopyForward or TradingPlanWriteOperation.StartDraft)
            {
                var source = await ReadReady(db, command.SourceRevision!, cancellationToken);
                if (source is null) return new(TradingPlanWriteStatus.Conflict);
                if (op == TradingPlanWriteOperation.CopyForward) content = source.Content.CopyForward();
                else { plan!.StartDraft(plan.Version, source, now); content = plan.WorkingDraft!.Content; }
            }
            if (plan is null) plan = new(command.Key, content, now);
            else if (op == TradingPlanWriteOperation.SaveDraft) plan.EditDraft(plan.Version, content, now);
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
            return new(TradingPlanWriteStatus.Success, result);
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
