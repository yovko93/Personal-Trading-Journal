using System.Xml.Linq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
using PersonalTradingJournal.Desktop.Tests.Notebook;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;

namespace PersonalTradingJournal.Desktop.Tests.CalendarPage;

public sealed class IsolatedTestSelectionTests
{
    private const string Host = "PTJ_NOTEBOOK_IMAGE_EDITOR_HOST";
    private static string Name(string theme = "Light", int width = 1280, int dpi = 96) =>
        $"{typeof(NotebookImageEditorTests).FullName}.{nameof(NotebookImageEditorTests.ImageRoundTripUndoPlacementPasteAndRenders)}(theme: \"{theme}\", width: {width}, dpi: {dpi})";

    [Fact]
    public void ExactFiltersRetainEveryArgumentAndEscapeFilterOperators()
    {
        string[] names = [Name(), Name("Dark"), Name("Light", 480, 240), Name("Dark", 480, 240)];
        var filters = names.Select(IsolatedTestProcess.ExactDisplayNameFilter).ToArray();
        Assert.Equal(4, filters.Distinct(StringComparer.Ordinal).Count());
        for (int i = 0; i < names.Length; i++)
        {
            Assert.StartsWith("DisplayName=", filters[i]);
            Assert.Equal(names[i], FilterHelper.Unescape(filters[i][12..]));
            Assert.Contains(@"\(", filters[i]); Assert.Contains(@"\)", filters[i]);
        }
        const string operators = "case(a&b|c=d!e~f\\g)";
        Assert.Equal(operators, FilterHelper.Unescape(IsolatedTestProcess.ExactDisplayNameFilter(operators)[12..]));
        Assert.Throws<ArgumentException>(() => IsolatedTestProcess.ExactDisplayNameFilter(" "));
    }

    [Fact]
    public async Task ExpectedIdentityRequiresANonemptySelectorBeforeStartingAChild()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => IsolatedTestProcess.RunSuiteAsync(typeof(NotebookImageEditorTests),
            "invalid-selection", Host, TimeSpan.FromSeconds(90), expectedTestName: Name()));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => IsolatedTestProcess.RunSuiteAsync(typeof(NotebookImageEditorTests),
            "invalid-selection", Host, TimeSpan.FromSeconds(90), testCaseFilter: " ", expectedTestName: Name()));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => IsolatedTestProcess.RunSuiteAsync(typeof(NotebookImageEditorTests),
            "invalid-selection", Host, TimeSpan.FromSeconds(90), testCaseFilter: "DisplayName=x", expectedTestName: " "));
    }

    [Fact]
    public async Task SuccessfulVstestExitWithZeroMatchesCannotPassSingleCaseDispatch()
    {
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => IsolatedTestProcess.RunSuiteAsync(
            typeof(NotebookImageEditorTests), "selection-zero-match", Host, TimeSpan.FromSeconds(90),
            caseHangTimeout: TimeSpan.FromSeconds(30), testCaseFilter: IsolatedTestProcess.ExactDisplayNameFilter(Name() + " absent"), expectedTestName: Name()));
        Assert.Contains("was not verified", failure.Message); Assert.Contains("Diagnostics:", failure.Message);
    }

    [Fact]
    public async Task SuccessfulDifferentTheoryRowCannotSatisfyExpectedIdentity()
    {
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => IsolatedTestProcess.RunSuiteAsync(
            typeof(NotebookImageEditorTests), "selection-wrong-row", Host, TimeSpan.FromSeconds(90),
            caseHangTimeout: TimeSpan.FromSeconds(30), testCaseFilter: IsolatedTestProcess.ExactDisplayNameFilter(Name()), expectedTestName: Name("Dark")));
        Assert.Contains("result outcome/name", failure.Message);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("missing-report")]
    [InlineData("extra-report")]
    [InlineData("malformed")]
    [InlineData("zero")]
    [InlineData("extra-result")]
    [InlineData("duplicate-result")]
    [InlineData("skipped")]
    [InlineData("failed")]
    [InlineData("aborted")]
    [InlineData("pending")]
    [InlineData("missing-counter")]
    [InlineData("wrong-name")]
    [InlineData("wrong-definition")]
    [InlineData("missing-definition")]
    [InlineData("wrong-entry")]
    [InlineData("missing-entry")]
    [InlineData("missing-end")]
    [InlineData("reversed-time")]
    public void TrxMustProveExactlyOneCompletedIntendedCase(string scenario)
    {
        string directory = Directory.CreateTempSubdirectory("ptj-selection-result-").FullName;
        try
        {
            XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
            var document = Report(ns); var root = document.Root!;
            var result = root.Element(ns + "Results")!.Element(ns + "UnitTestResult")!;
            var counts = root.Element(ns + "ResultSummary")!.Element(ns + "Counters")!;
            switch (scenario)
            {
                case "zero": counts.SetAttributeValue("total", 0); root.Element(ns + "Results")!.RemoveNodes(); break;
                case "extra-result": counts.SetAttributeValue("total", 2); root.Element(ns + "Results")!.Add(new XElement(result)); break;
                case "duplicate-result": root.Element(ns + "Results")!.Add(new XElement(result)); break;
                case "skipped": result.SetAttributeValue("outcome", "NotExecuted"); counts.SetAttributeValue("passed", 0); counts.SetAttributeValue("notExecuted", 1); break;
                case "failed": result.SetAttributeValue("outcome", "Failed"); break;
                case "aborted": root.Element(ns + "ResultSummary")!.SetAttributeValue("outcome", "Aborted"); break;
                case "pending": counts.SetAttributeValue("pending", 1); break;
                case "missing-counter": counts.Attribute("executed")!.Remove(); break;
                case "wrong-name": result.SetAttributeValue("testName", Name("Dark")); break;
                case "wrong-definition": root.Element(ns + "TestDefinitions")!.Element(ns + "UnitTest")!.SetAttributeValue("id", Guid.NewGuid()); break;
                case "missing-definition": root.Element(ns + "TestDefinitions")!.RemoveNodes(); break;
                case "wrong-entry": root.Element(ns + "TestEntries")!.Element(ns + "TestEntry")!.SetAttributeValue("executionId", Guid.NewGuid()); break;
                case "missing-entry": root.Element(ns + "TestEntries")!.RemoveNodes(); break;
                case "missing-end": result.Attribute("endTime")!.Remove(); break;
                case "reversed-time": result.SetAttributeValue("endTime", "2026-10-20T00:00:00Z"); break;
            }
            if (scenario != "missing-report") document.Save(Path.Combine(directory, "child.trx"));
            if (scenario == "extra-report") document.Save(Path.Combine(directory, "extra.trx"));
            if (scenario == "malformed") File.WriteAllText(Path.Combine(directory, "child.trx"), "<TestRun>");
            if (scenario == "valid") IsolatedTestProcess.ValidateSingleCaseResult(directory, Name());
            else
            {
                var failure = Assert.Throws<InvalidOperationException>(() => IsolatedTestProcess.ValidateSingleCaseResult(directory, Name()));
                Assert.Contains("was not verified", failure.Message); Assert.Contains(directory, failure.Message);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    private static XDocument Report(XNamespace ns)
    {
        var id = Guid.NewGuid(); var execution = Guid.NewGuid();
        var counters = new XElement(ns + "Counters", new[] { "total", "executed", "passed", "failed", "error", "timeout", "aborted", "inconclusive",
            "passedButRunAborted", "notRunnable", "notExecuted", "disconnected", "warning", "completed", "inProgress", "pending" }
            .Select(key => new XAttribute(key, key is "total" or "executed" or "passed" ? 1 : 0)));
        return new(new XElement(ns + "TestRun",
            new XElement(ns + "Results", new XElement(ns + "UnitTestResult", new XAttribute("testId", id), new XAttribute("executionId", execution),
                new XAttribute("testName", Name()), new XAttribute("outcome", "Passed"), new XAttribute("startTime", "2026-10-21T00:00:00Z"), new XAttribute("endTime", "2026-10-21T00:00:01Z"))),
            new XElement(ns + "TestDefinitions", new XElement(ns + "UnitTest", new XAttribute("id", id), new XAttribute("name", Name()))),
            new XElement(ns + "TestEntries", new XElement(ns + "TestEntry", new XAttribute("testId", id), new XAttribute("executionId", execution))),
            new XElement(ns + "ResultSummary", new XAttribute("outcome", "Completed"), counters)));
    }
}
