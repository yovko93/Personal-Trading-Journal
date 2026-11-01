using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal static partial class IsolatedTestProcess
{
    internal static string ExactDisplayNameFilter(string expectedTestName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedTestName);
        return "DisplayName=" + FilterHelper.Escape(expectedTestName);
    }

    /// <summary>Opt-in single-row dispatch must fail closed even when VSTest exits zero with no matches.
    /// Existing whole-suite callers retain their original exit-code contract.</summary>
    internal static void ValidateSingleCaseResult(string resultsDirectory, string expectedTestName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedTestName);
        InvalidOperationException Failure(string reason, Exception? inner = null) => new(
            $"Isolated test selection '{expectedTestName}' was not verified: {reason}. Diagnostics: {resultsDirectory}.", inner);
        var files = Directory.GetFiles(resultsDirectory, "*.trx", SearchOption.TopDirectoryOnly);
        if (files.Length != 1) throw Failure($"expected exactly one TRX, found {files.Length}");
        XDocument document;
        try
        {
            using var reader = XmlReader.Create(files[0], new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            document = XDocument.Load(reader);
        }
        catch (Exception error) when (error is XmlException or IOException)
        { throw Failure("unreadable TRX", error); }
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        var run = document.Root;
        if (run?.Name != ns + "TestRun") throw Failure("invalid TRX root");
        var summary = run.Element(ns + "ResultSummary");
        var counts = summary?.Element(ns + "Counters");
        if ((string?)summary?.Attribute("outcome") != "Completed" || counts is null) throw Failure("run did not complete");
        foreach (string key in new[] { "total", "executed", "passed", "failed", "error", "timeout", "aborted", "inconclusive",
            "passedButRunAborted", "notRunnable", "notExecuted", "disconnected", "warning", "completed", "inProgress", "pending" })
        {
            int expected = key is "total" or "executed" or "passed" ? 1 : 0;
            if (!int.TryParse((string?)counts.Attribute(key), NumberStyles.None, CultureInfo.InvariantCulture, out int actual) || actual != expected)
                throw Failure($"counter '{key}' must be {expected}");
        }
        var results = run.Element(ns + "Results")?.Elements().ToArray() ?? [];
        if (results.Length != 1 || results[0].Name != ns + "UnitTestResult") throw Failure("expected exactly one result");
        var result = results[0];
        if ((string?)result.Attribute("outcome") != "Passed" || (string?)result.Attribute("testName") != expectedTestName)
            throw Failure("result outcome/name does not match the intended passed case");
        if (!Guid.TryParse((string?)result.Attribute("testId"), out var id) || id == Guid.Empty ||
            !Guid.TryParse((string?)result.Attribute("executionId"), out var execution) || execution == Guid.Empty)
            throw Failure("invalid result identity");
        var definitions = run.Element(ns + "TestDefinitions")?.Elements().ToArray() ?? [];
        var entries = run.Element(ns + "TestEntries")?.Elements().ToArray() ?? [];
        if (definitions.Length != 1 || definitions[0].Name != ns + "UnitTest" ||
            (string?)definitions[0].Attribute("name") != expectedTestName || (string?)definitions[0].Attribute("id") != id.ToString() ||
            entries.Length != 1 || entries[0].Name != ns + "TestEntry" || (string?)entries[0].Attribute("testId") != id.ToString() ||
            (string?)entries[0].Attribute("executionId") != execution.ToString())
            throw Failure("definition/entry identity does not match the single result");
        if (!DateTimeOffset.TryParse((string?)result.Attribute("startTime"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) ||
            !DateTimeOffset.TryParse((string?)result.Attribute("endTime"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var end) || end < start)
            throw Failure("missing or invalid completion timestamps");
    }
}
