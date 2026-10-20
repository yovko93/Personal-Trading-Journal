using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Notebook;

public sealed class NotebookRecoveryStore(IDbContextFactory<JournalDbContext> factory, INotebookContentCodec codec) : INotebookRecoveryStore
{
    public async Task<NotebookRecoveryPage> ListAsync(Guid? after = null, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var query = db.NotebookRecoveryDrafts.AsNoTracking();
        if (after.HasValue) query = query.Where(x => x.Id.CompareTo(after.Value) > 0);
        var rows = await query.OrderBy(x => x.Id).Select(x => new NotebookRecoveryItem(x.Id, x.NoteId, x.Revision,
            x.Title.Substring(0, 200), x.CheckpointedAtUtc)).Take(51).ToArrayAsync(cancellationToken);
        return new([.. rows.Take(50)], rows.Length > 50 ? rows[49].Id : null);
    }
    public async Task<NotebookRecoveryLoad> LoadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var snapshot = await db.Database.BeginTransactionAsync(cancellationToken);
        var metadata = await db.NotebookRecoveryDrafts.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.EnvelopeVersion, Length = x.DocumentJson.Length, TitleLength = x.Title.Length }).SingleOrDefaultAsync(cancellationToken);
        if (metadata is null) return new(NotebookLoadStatus.NotFound);
        if (metadata.EnvelopeVersion != 1) return new(NotebookLoadStatus.UnsupportedContent);
        if (metadata.Length > 1024 * 1024 || metadata.TitleLength > 200) return new(NotebookLoadStatus.TooLarge);
        var r = await db.NotebookRecoveryDrafts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (r is null) return new(NotebookLoadStatus.NotFound);
        if (r.EnvelopeVersion != 1) return new(NotebookLoadStatus.UnsupportedContent);
        var content = codec.Decode(r.DocumentJson);
        if (content.Status != NotebookContentStatus.Success) return new(content.Status == NotebookContentStatus.TooLarge ? NotebookLoadStatus.TooLarge :
            content.Status == NotebookContentStatus.UnsupportedVersion ? NotebookLoadStatus.UnsupportedContent : NotebookLoadStatus.InvalidContent);
        try
        {
            NotebookLimits.Id(r.NoteId, nameof(r.NoteId)); NotebookLimits.Text(r.Title, NotebookLimits.Title, nameof(r.Title));
            if (r.Revision <= 0 || r.ExpectedVersion <= 0 || r.CheckpointedAtUtc.Offset != TimeSpan.Zero) return new(NotebookLoadStatus.InvalidContent);
            return new(NotebookLoadStatus.Success, new(r.Id, r.NoteId, r.ExpectedVersion, r.Revision, r.Title, content.Document!, r.CheckpointedAtUtc));
        }
        catch (ArgumentException) { return new(NotebookLoadStatus.InvalidContent); }
    }
    public Task<NotebookWriteStatus> PutAsync(NotebookRecoveryDraft draft, long expectedRevision, CancellationToken cancellationToken = default) =>
        WriteAsync(async (db, ct) =>
        {
            NotebookLimits.Id(draft.Id, nameof(draft.Id)); NotebookLimits.Id(draft.NoteId, nameof(draft.NoteId));
            NotebookLimits.Text(draft.Title, NotebookLimits.Title, nameof(draft.Title));
            if (expectedRevision < 0 || draft.Revision <= expectedRevision || draft.ExpectedVersion <= 0 ||
                draft.CheckpointedAtUtc.Offset != TimeSpan.Zero) return NotebookWriteStatus.Invalid;
            var encoded = codec.Encode(draft.Document);
            var row = await db.NotebookRecoveryDrafts.SingleOrDefaultAsync(x => x.Id == draft.Id, ct);
            if (row is null)
            {
                if (expectedRevision != 0) return NotebookWriteStatus.Conflict;
                if (await db.NotebookRecoveryDrafts.CountAsync(ct) >= 128) return NotebookWriteStatus.TooLarge;
                row = new() { Id = draft.Id, NoteId = draft.NoteId }; db.Add(row);
            }
            else if (row.EnvelopeVersion != 1 || row.Revision != expectedRevision || row.NoteId != draft.NoteId ||
                     row.ExpectedVersion > draft.ExpectedVersion) return NotebookWriteStatus.Conflict;
            row.Revision = draft.Revision; row.ExpectedVersion = draft.ExpectedVersion;
            row.Title = draft.Title; row.DocumentJson = encoded.Json; row.CheckpointedAtUtc = draft.CheckpointedAtUtc;
            return NotebookWriteStatus.Success;
        }, cancellationToken);
    public Task<NotebookWriteStatus> RemoveAsync(Guid id, long expectedRevision, CancellationToken cancellationToken = default) =>
        WriteAsync(async (db, ct) =>
        {
            var row = await db.NotebookRecoveryDrafts.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (row is null) return NotebookWriteStatus.Success; // Idempotent cleanup after a committed save.
            if (expectedRevision <= 0 || row.Revision != expectedRevision) return NotebookWriteStatus.Conflict;
            db.Remove(row); return NotebookWriteStatus.Success;
        }, cancellationToken);
    private async Task<NotebookWriteStatus> WriteAsync(Func<JournalDbContext, CancellationToken, Task<NotebookWriteStatus>> action, CancellationToken ct)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            await db.Database.OpenConnectionAsync(ct);
            await using var tx = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: false);
            await db.Database.UseTransactionAsync(tx, ct);
            var status = await action(db, ct);
            if (status != NotebookWriteStatus.Success) return status;
            await db.SaveChangesAsync(ct); ct.ThrowIfCancellationRequested();
            await tx.CommitAsync(CancellationToken.None);
            return NotebookWriteStatus.Success;
        }
        catch (OperationCanceledException) { return NotebookWriteStatus.Cancelled; }
        catch (ArgumentOutOfRangeException) { return NotebookWriteStatus.TooLarge; }
        catch (ArgumentException) { return NotebookWriteStatus.Invalid; }
        catch (DbUpdateConcurrencyException) { return NotebookWriteStatus.Conflict; }
        catch (Exception ex) when (ex is SqliteException or DbUpdateException or IOException) { return NotebookWriteStatus.StorageFailure; }
    }
}
