using System.Collections.Immutable;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Common.Time;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Persistence;
using PersonalTradingJournal.Infrastructure.Persistence.Records;

namespace PersonalTradingJournal.Infrastructure.Notebook;

public sealed partial class NotebookService
{
    private sealed class LinkTargetRow
    {
        public Guid Id { get; init; }
        public string Label { get; init; } = "";
        public string Account { get; init; } = "";
        public DateTimeOffset? Opened { get; init; }
        public bool IsActive { get; init; }
    }
    private static IQueryable<LinkTargetRow> TradeTargets(JournalDbContext db, IQueryable<TradeRecord>? trades = null) =>
        from t in (trades ?? db.Trades).AsNoTracking()
        join i in db.Instruments.AsNoTracking() on t.InstrumentId equals i.Id
        join a in db.TradingAccounts.AsNoTracking() on t.TradingAccountId equals a.Id
        join b in db.TradeBrowse.AsNoTracking() on t.Id equals b.TradeId into browse
        from b in browse.DefaultIfEmpty()
        select new LinkTargetRow { Id = t.Id, Label = i.Symbol, Account = a.Name,
            Opened = b == null ? null : b.OpenedAtUtc, IsActive = true };

    private static string TradeLabel(LinkTargetRow row) => $"{row.Label} · {row.Account} · " +
        (row.Opened is { } time ? $"{TradingTimePolicy.ConvertUtcToTradingTime(time):g} NY" : "Time unavailable") + $" · {row.Id}";

    public async Task<NotebookTargetPage> ListLinkTargetsAsync(NotebookTargetQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var ct = cancellationToken;
        string scope = Fingerprint(new { Target = query.Kind, query.Search });
        var cursor = DecodeCursor(query.Cursor, scope);
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.OpenConnectionAsync(ct);
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        // SQLite's built-in upper()/NOCASE only fold ASCII. Keep target-name search
        // ordinal-ignore-case for Unicode as well, without loading an unbounded catalog.
        connection.CreateFunction<string, string, bool>("ptj_notebook_contains",
            static (value, search) => value.Contains(search, StringComparison.OrdinalIgnoreCase), isDeterministic: true);
        await using var tx = ((SqliteConnection)db.Database.GetDbConnection()).BeginTransaction(deferred: true);
        await db.Database.UseTransactionAsync(tx, ct);
        IQueryable<TradeRecord> trades = db.Trades;
        IQueryable<TradingSetupRecord> setups = db.TradingSetups;
        if (query.Search.Length != 0)
        {
            // Parameterized source filters compose with SQL count/order/Take below.
            string search = query.Search;
            trades = db.Trades.FromSqlInterpolated($"SELECT t.* FROM Trades t JOIN Instruments i ON t.InstrumentId = i.Id JOIN TradingAccounts a ON t.TradingAccountId = a.Id WHERE ptj_notebook_contains(i.Symbol, {search}) OR ptj_notebook_contains(a.Name, {search}) OR ptj_notebook_contains(t.Id, {search})");
            setups = db.TradingSetups.FromSqlInterpolated($"SELECT * FROM TradingSetups WHERE ptj_notebook_contains(Name, {search}) OR ptj_notebook_contains(Id, {search})");
        }
        IQueryable<LinkTargetRow> rows = query.Kind == NotebookTargetKind.Trade ? TradeTargets(db, trades) :
            setups.AsNoTracking().Select(x => new LinkTargetRow { Id = x.Id, Label = x.Name, Account = "", Opened = null, IsActive = x.IsActive });
        long total = await rows.LongCountAsync(ct);
        if (cursor is not null) rows = rows.Where(x => string.Compare(x.Id.ToString(), cursor.Id) > 0);
        var page = await rows.OrderBy(x => x.Id).Take(query.Limit + 1).ToArrayAsync(ct);
        var shown = page.Take(query.Limit).ToArray();
        return new([.. shown.Select(x => new NotebookLinkTarget(query.Kind, x.Id,
            query.Kind == NotebookTargetKind.Trade ? TradeLabel(x) : x.Label,
            query.Kind == NotebookTargetKind.Trade ? "Trade" : $"Trading Setup · {(x.IsActive ? "Active" : "Inactive")} · {x.Id}"))], total,
            page.Length > query.Limit ? EncodeCursor(scope, "", shown[^1].Id, DateTimeOffset.MinValue) : null);
    }
}
