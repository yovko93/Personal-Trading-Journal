namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

/// <summary>Bounded, content-free state at synthetic interaction boundaries.</summary>
internal static class WpfStepDiagnostics
{
    private static readonly object Gate = new();
    private static int _count;
    internal static void Write(string state)
    {
        lock (Gate)
        {
            if (_count++ >= 128) return;
            string line = $"{DateTimeOffset.UtcNow:O} PID={Environment.ProcessId}; thread={Environment.CurrentManagedThreadId}; {state[..Math.Min(state.Length, 1024)]}";
            Console.WriteLine(line);
            if (Environment.GetEnvironmentVariable("PTJ_TEST_RESULTS_DIRECTORY") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                File.AppendAllText(Path.Combine(output, "wpf-interactions.log"), line + Environment.NewLine);
            }
        }
    }
}
