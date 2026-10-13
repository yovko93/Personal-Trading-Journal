using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace PersonalTradingJournal.Desktop.Diagnostics;

internal enum RuleDragCounter
{
    PointerMoves, CatalogReads, RevisionReads, MoveWrites, CatalogPublishes, PropertyNotifications,
    CollectionChanges, RowsLoaded, RowsUnloaded, RowMeasures, RowArranges, ViewLayouts,
    PreviewCaptures, PreviewMoves, TargetChanges, TreeScans, ScrollRequests, Animations, ShiftFrames
}
internal enum RuleDragPhase { InputEnded, DropCompleted }

/// <summary>Opt-in numeric counters only: no Rule text, identifiers or database paths.</summary>
internal sealed class RulesDragDiagnostics
{
    private readonly long[] _counts = new long[Enum.GetValues<RuleDragCounter>().Length];
    private long _started = Stopwatch.GetTimestamp();
    private int _reports;
    internal static bool Enabled => Environment.GetEnvironmentVariable("PTJ_RULES_DRAG_DIAGNOSTICS") == "1";
    internal void Count(RuleDragCounter counter) => Interlocked.Increment(ref _counts[(int)counter]);
    internal long this[RuleDragCounter counter] => Interlocked.Read(ref _counts[(int)counter]);
    internal void Reset()
    {
        foreach (var counter in Enum.GetValues<RuleDragCounter>()) Interlocked.Exchange(ref _counts[(int)counter], 0);
        _started = Stopwatch.GetTimestamp();
    }
    internal string Snapshot() => JsonSerializer.Serialize(new
    {
        ElapsedMs = Math.Round(Stopwatch.GetElapsedTime(_started).TotalMilliseconds, 3),
        Counters = Enum.GetValues<RuleDragCounter>().ToDictionary(c => c.ToString(), c => this[c])
    });
    internal void Report(RuleDragPhase phase = RuleDragPhase.InputEnded)
    {
        // Counters stay in memory during movement; at most 32 bounded summaries per view model.
        if (Interlocked.Increment(ref _reports) > 32) return;
        string summary = "{\"Phase\":\"" + phase + "\",\"Measurement\":" + Snapshot() + "}";
        Trace.WriteLine("RulesDrag " + summary);
        if (Environment.GetEnvironmentVariable("PTJ_RULES_DRAG_DIAGNOSTICS_DIRECTORY") is not { Length: > 0 } directory) return;
        try
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, $"rules-drag-{Environment.ProcessId}.jsonl");
            if (!File.Exists(path) || new FileInfo(path).Length < 64 * 1024)
                File.AppendAllText(path, summary + Environment.NewLine);
        }
        catch (Exception) { /* Optional diagnostic output must never break dragging or hide the operation result. */ }
    }
}
