using System.Collections.Immutable;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Application.Notebook;

/// <summary>Existing targets only. Trades include inactive accounts/instruments; Setups include
/// inactive definitions, explicitly labelled. Neither selection nor resolution mutates targets.</summary>
public sealed class NotebookTargetQuery
{
    public NotebookTargetQuery(NotebookTargetKind kind, string search = "", int limit = 25, string? cursor = null)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        Kind = kind; Search = NotebookLimits.Text(search, NotebookLimits.Search, nameof(search)).Trim(); Limit = limit;
        if (cursor is not null) NotebookLimits.Text(cursor, 2048, nameof(cursor), true);
        Cursor = cursor;
    }
    public NotebookTargetKind Kind { get; }
    public string Search { get; }
    public int Limit { get; }
    public string? Cursor { get; }
}

public sealed record NotebookLinkTarget(NotebookTargetKind Kind, Guid Id, string Label, string Context);
public sealed record NotebookTargetPage(ImmutableArray<NotebookLinkTarget> Items, long TotalCount, string? NextCursor);
