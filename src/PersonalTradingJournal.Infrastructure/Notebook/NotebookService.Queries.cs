using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Notebook;

public sealed partial class NotebookService
{
    private sealed record Cursor(string Scope, string Key, string Id, long Ticks);
    private static string Fingerprint(object value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    private static string EncodeCursor(string scope, string key, Guid id, DateTimeOffset updated) =>
        Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new Cursor(scope, key, id.ToString().ToUpperInvariant(), updated.UtcTicks)));
    private static Cursor? DecodeCursor(string? text, string scope)
    {
        if (text is null) return null;
        try
        {
            var cursor = JsonSerializer.Deserialize<Cursor>(Convert.FromBase64String(text));
            if (cursor is null || cursor.Scope != scope || cursor.Key is null || cursor.Key.Length > 800 ||
                cursor.Key.Any(c => !Uri.IsHexDigit(c)) || !Guid.TryParseExact(cursor.Id, "D", out var id) || id == Guid.Empty ||
                cursor.Ticks < DateTimeOffset.MinValue.UtcTicks || cursor.Ticks > DateTimeOffset.MaxValue.UtcTicks)
                throw new ArgumentException("The cursor does not belong to this query.");
            return cursor;
        }
        catch (Exception ex) when (ex is FormatException or JsonException) { throw new ArgumentException("Invalid Notebook cursor.", nameof(text)); }
    }
    public async Task<NotebookPage> ListAsync(NotebookQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var ct = cancellationToken;
        string scope = Fingerprint(new { Population = "non-trash", query.Scope, query.FolderId, query.Search, Tags = query.TagIds.Order(), query.Sort });
        var cursor = DecodeCursor(query.Cursor, scope);
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.OpenConnectionAsync(ct);
        await using var tx = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: true);
        await db.Database.UseTransactionAsync(tx, ct);
        IQueryable<NotebookNoteRecord> notes = db.NotebookNotes.AsNoTracking().Where(x =>
            query.Scope == NotebookScope.Trash ? x.State == 3 : x.State != 3);
        if (query.Scope == NotebookScope.Folder) notes = notes.Where(x => x.FolderId == query.FolderId);
        if (query.Scope == NotebookScope.Pinned) notes = notes.Where(x => x.IsPinned);
        long scopeCount = await notes.LongCountAsync(ct);
        if (query.Search.Length > 0)
        {
            string search = query.Search.Normalize(NormalizationForm.FormC).ToUpperInvariant();
            notes = notes.Where(x => x.SearchText.Contains(search));
        }
        foreach (Guid tag in query.TagIds) notes = notes.Where(x => db.NotebookTagAssignments.Any(a => a.NoteId == x.Id && a.TagId == tag));
        long count = await notes.LongCountAsync(ct);
        if (cursor is not null)
        {
            var stamp = new DateTimeOffset(cursor.Ticks, TimeSpan.Zero); var key = cursor.Key; var id = cursor.Id;
            notes = query.Sort == NotebookSort.Title
                ? notes.Where(x => string.Compare(x.TitleKey, key) > 0 || x.TitleKey == key && string.Compare(x.Id.ToString(), id) > 0)
                : notes.Where(x => x.UpdatedAtUtc < stamp || x.UpdatedAtUtc == stamp && string.Compare(x.Id.ToString(), id) > 0);
        }
        var ordered = query.Sort == NotebookSort.Title ? notes.OrderBy(x => x.TitleKey).ThenBy(x => x.Id)
            : notes.OrderByDescending(x => x.UpdatedAtUtc).ThenBy(x => x.Id);
        // Explicit projection: never transfer DocumentJson or all PlainText just to display a list.
        var rows = await ordered.Select(x => new { x.Id, x.Title, x.TitleKey, Preview = x.PlainText.Substring(0, 180),
            x.State, x.IsPinned, x.FolderId, x.Version, x.UpdatedAtUtc }).Take(query.Limit + 1).ToArrayAsync(ct);
        var shown = rows.Take(query.Limit).ToArray(); var ids = shown.Select(x => x.Id).ToArray();
        var tags = await (from a in db.NotebookTagAssignments.AsNoTracking()
            join tag in db.NotebookTags.AsNoTracking() on a.TagId equals tag.Id
            where ids.Contains(a.NoteId)
            orderby tag.NameKey, tag.Id
            select new { a.NoteId, tag.Id, tag.Name, tag.Version }).Take(query.Limit * NotebookLimits.Tags + 1).ToArrayAsync(ct);
        if (tags.Length > query.Limit * NotebookLimits.Tags) throw new InvalidDataException("Notebook tag bound exceeded.");
        var items = shown.Select(x => new NotebookListItem(x.Id, x.Title, x.Preview, (NotebookNoteState)x.State, x.IsPinned,
            x.FolderId, x.Version, x.UpdatedAtUtc, tags.Where(t => t.NoteId == x.Id).Select(t => new NotebookNamedItem(t.Id, t.Name, t.Version)).ToImmutableArray())).ToImmutableArray();
        string? next = rows.Length > query.Limit ? EncodeCursor(scope, shown[^1].TitleKey, shown[^1].Id, shown[^1].UpdatedAtUtc) : null;
        var counts = await db.NotebookNotes.AsNoTracking().GroupBy(x => x.State)
            .Select(g => new { State = g.Key, Total = g.LongCount(), Pinned = g.LongCount(x => x.IsPinned) }).ToArrayAsync(ct);
        var scopes = ImmutableArray.CreateBuilder<NotebookScopeCount>();
        scopes.Add(new(NotebookScope.AllNotes, null, counts.Where(x => x.State != 3).Sum(x => x.Total)));
        scopes.Add(new(NotebookScope.Pinned, null, counts.Where(x => x.State != 3).Sum(x => x.Pinned)));
        scopes.Add(new(NotebookScope.Trash, null, counts.FirstOrDefault(x => x.State == 3)?.Total ?? 0));
        if (query.Scope == NotebookScope.Folder) scopes.Add(new(NotebookScope.Folder, query.FolderId, scopeCount));
        // Resolve at most 32 selected identities in the same snapshot, independently of
        // the first catalog page. Absence is authoritative, not a paging omission.
        var requestedIds = query.TagIds.ToArray();
        var requestedTags = await db.NotebookTags.AsNoTracking().Where(t => requestedIds.Contains(t.Id))
            .Select(t => new NotebookCatalogItem(new(t.Id, t.Name, t.Version),
                db.NotebookTagAssignments.LongCount(a => a.TagId == t.Id && db.NotebookNotes.Any(n => n.Id == a.NoteId && n.State != 3))))
            .Take(NotebookLimits.Tags).ToArrayAsync(ct);
        return new(items, count, next, new(scopes.ToImmutable(),
            await CatalogPage(db, new(NotebookCatalogKind.Folders), ct), await CatalogPage(db, new(NotebookCatalogKind.Tags), ct), [.. requestedTags]));
    }
    public async Task<NotebookCatalogPage> ListCatalogAsync(NotebookCatalogQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var tx = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: true);
        await db.Database.UseTransactionAsync(tx, cancellationToken);
        return await CatalogPage(db, query, cancellationToken);
    }
    private sealed class CatalogRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = "";
        public string Key { get; init; } = "";
        public long Version { get; init; }
        public long Count { get; init; }
    }
    private static async Task<NotebookCatalogPage> CatalogPage(JournalDbContext db, NotebookCatalogQuery query, CancellationToken ct)
    {
        string scope = Fingerprint(new { Catalog = query.Kind, Population = "non-trash" }); var cursor = DecodeCursor(query.Cursor, scope);
        IQueryable<CatalogRow> rows = query.Kind == NotebookCatalogKind.Folders
            ? db.NotebookFolders.AsNoTracking().Select(x => new CatalogRow { Id = x.Id, Name = x.Name, Key = x.NameKey, Version = x.Version, Count = db.NotebookNotes.LongCount(n => n.FolderId == x.Id && n.State != 3) })
            : db.NotebookTags.AsNoTracking().Select(x => new CatalogRow { Id = x.Id, Name = x.Name, Key = x.NameKey, Version = x.Version,
                Count = db.NotebookTagAssignments.LongCount(a => a.TagId == x.Id && db.NotebookNotes.Any(n => n.Id == a.NoteId && n.State != 3)) });
        var total = await rows.LongCountAsync(ct);
        if (cursor is not null) rows = rows.Where(x => string.Compare(x.Key, cursor.Key) > 0 || x.Key == cursor.Key && string.Compare(x.Id.ToString(), cursor.Id) > 0);
        var page = await rows.OrderBy(x => x.Key).ThenBy(x => x.Id).Take(query.Limit + 1).ToArrayAsync(ct);
        var shown = page.Take(query.Limit).ToArray();
        return new(shown.Select(x => new NotebookCatalogItem(new(x.Id, x.Name, x.Version), x.Count)).ToImmutableArray(), total,
            page.Length > query.Limit ? EncodeCursor(scope, shown[^1].Key, shown[^1].Id, DateTimeOffset.MinValue) : null);
    }
    public async Task<ImmutableArray<NotebookResolvedLink>> ResolveLinksAsync(ImmutableArray<NotebookLink> links, CancellationToken cancellationToken = default)
    {
        if (links.IsDefault || links.Length > NotebookLimits.Links || links.Any(x => x is null) ||
            links.Select(x => (x.Kind, x.TargetId)).Distinct().Count() != links.Length) throw new ArgumentException("Invalid link batch.", nameof(links));
        var ct = cancellationToken;
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.OpenConnectionAsync(ct);
        await using var tx = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: true);
        await db.Database.UseTransactionAsync(tx, ct);
        var tradeIds = links.Where(x => x.Kind == NotebookTargetKind.Trade).Select(x => x.TargetId).ToArray();
        var setupIds = links.Where(x => x.Kind == NotebookTargetKind.TradingSetup).Select(x => x.TargetId).ToArray();
        var tradeRows = await TradeTargets(db).Where(x => tradeIds.Contains(x.Id)).ToArrayAsync(ct);
        var trades = tradeRows.ToDictionary(x => x.Id, TradeLabel);
        var setups = await db.TradingSetups.AsNoTracking().Where(x => setupIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        return links.Select(link =>
        {
            string? label = link.Kind == NotebookTargetKind.Trade ? trades.GetValueOrDefault(link.TargetId) : setups.GetValueOrDefault(link.TargetId)?.Name;
            return new NotebookResolvedLink(link, label is not null ? NotebookTargetAvailability.Available : NotebookTargetAvailability.Unavailable,
                label ?? link.LastKnownLabel, link.Kind == NotebookTargetKind.TradingSetup && setups.GetValueOrDefault(link.TargetId)?.IsActive == false);
        }).ToImmutableArray();
    }
}
