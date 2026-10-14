using System.Diagnostics;
using System.Text.Json;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence;

// Opt-in numeric-only setup measurements; never include paths or seeded content.
internal sealed class SyntheticFixtureTiming : IDisposable
{
    private static readonly object Gate = new();
    private static int _records;
    private readonly string _operation;
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();

    private SyntheticFixtureTiming(string operation) => _operation = operation;

    internal static SyntheticFixtureTiming? Start(string operation) =>
        Environment.GetEnvironmentVariable("PTJ_CALENDAR_TIMINGS") == "1"
            ? new(operation) : null;

    public void Dispose()
    {
        _elapsed.Stop();
        if (Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY") is not { Length: > 0 } output)
            return;
        lock (Gate)
        {
            if (_records++ >= 4096) return;
            Directory.CreateDirectory(output);
            File.AppendAllText(Path.Combine(output, "synthetic-fixture-timings.jsonl"),
                JsonSerializer.Serialize(new { operation = _operation, milliseconds = _elapsed.Elapsed.TotalMilliseconds }) + Environment.NewLine);
        }
    }
}
