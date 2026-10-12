using PersonalTradingJournal.Domain.Setups;
using PersonalTradingJournal.Domain.Screenshots;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Domain.Tests.Setups;

public sealed class PlaybookTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
    private static PlaybookGuidance Guidance(string context = "NQ synthetic range") =>
        new(context, "Retest", "Observe acceptance", "Return inside range", "Record planned risk");

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void EveryGuidanceFieldRequiresBoundedMeaningfulText(int field)
    {
        foreach (string invalid in new[] { " ", new string('x', 4001) })
        {
            string[] fields = ["context", "entry", "confirmation", "invalidation", "execution"];
            fields[field] = invalid;
            Assert.ThrowsAny<ArgumentException>(() => new PlaybookGuidance(fields[0], fields[1], fields[2], fields[3], fields[4]));
        }
        Assert.Equal("context", Guidance("  context  ").MarketContext);
        Assert.Equal(4000, Guidance(new string('x', 4000)).MarketContext.Length);
    }

    [Fact]
    public void ExamplesAreOptionalDefensivelyCopiedBoundedAndUniquelyIdentified()
    {
        var example = new PlaybookExample(Guid.NewGuid(), " Annotated synthetic retest ");
        var examples = new List<PlaybookExample> { example };
        var guidance = new PlaybookGuidance("a", "b", "c", "d", "e", examples);
        examples.Clear();
        Assert.Equal("Annotated synthetic retest", Assert.Single(guidance.Examples).Annotation);
        Assert.Throws<NotSupportedException>(() => ((IList<PlaybookExample>)guidance.Examples).Clear());
        Assert.Empty(Guidance().Examples);
        Assert.Throws<ArgumentException>(() => new PlaybookGuidance("a", "b", "c", "d", "e", [example, example]));
        Assert.Throws<ArgumentException>(() => new PlaybookGuidance("a", "b", "c", "d", "e", [null!]));
        Assert.Throws<ArgumentException>(() => new PlaybookGuidance("a", "b", "c", "d", "e",
            Enumerable.Range(0, 13).Select(_ => new PlaybookExample(Guid.NewGuid(), "note"))));
    }

    [Fact]
    public void ExampleProvenanceRequiresStableNonEmptyIdentitiesAndAnnotation()
    {
        Assert.Throws<ArgumentException>(() => new PlaybookExample(Guid.Empty, "note"));
        Assert.Throws<ArgumentException>(() => new PlaybookExample(Guid.NewGuid(), " "));
        Assert.Throws<ArgumentException>(() => new PlaybookExample(Guid.NewGuid(), "note", Guid.Empty));
        Assert.Throws<ArgumentException>(() => new PlaybookExample(Guid.NewGuid(), "note", null, Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => new PlaybookExample(Guid.NewGuid(), "note", Guid.NewGuid(), Guid.Empty));
        Guid trade = Guid.NewGuid(), screenshot = Guid.NewGuid();
        var example = new PlaybookExample(Guid.NewGuid(), "note", trade, screenshot);
        Assert.Equal(trade, example.SourceTradeId);
        Assert.Equal(screenshot, example.SourceScreenshotId);
    }

    [Fact]
    public void ScreenshotExampleReusesAuthoritativeOwnershipWithoutCopyingStorageKeys()
    {
        var screenshot = new TradeScreenshot(Guid.NewGuid(), TradeScreenshotType.Entry,
            "synthetic.png", "synthetic.png", Now, "5m", "Original caption", Now);
        Guid exampleId = Guid.NewGuid();
        var example = PlaybookExample.FromScreenshot(exampleId, "Look for the failed retest", screenshot);
        Assert.Equal(exampleId, example.Id);
        Assert.Equal(screenshot.TradeId, example.SourceTradeId);
        Assert.Equal(screenshot.Id, example.SourceScreenshotId);
        Assert.Equal("Look for the failed retest", example.Annotation);
        Assert.Equal("Original caption", screenshot.Description);
    }

    [Fact]
    public void RevisionsExtendExistingSetupIdentityAndFreezeItsText()
    {
        var setup = new TradingSetup("NQ retest", "Original", Now);
        var first = TradingSetupPlaybookRevision.Create(setup, Guidance(), Now);
        setup.UpdateDetails("NQ revised", "New", Now.AddMinutes(1));
        var second = first.Revise(setup, 1, Guidance("New context"), Now.AddMinutes(1));
        Assert.Equal(setup.Id, first.TradingSetupId);
        Assert.Equal(first.TradingSetupId, second.TradingSetupId);
        Assert.Equal(1, first.Revision);
        Assert.Equal(2, second.Revision);
        Assert.Equal("NQ retest", first.SetupName);
        Assert.Equal("Original", first.SetupDescription);
        Assert.Equal("NQ revised", second.SetupName);
        Assert.Equal("NQ synthetic range", first.Guidance.MarketContext);
        Assert.Equal("New context", second.Guidance.MarketContext);
        Assert.Throws<InvalidOperationException>(() => second.Revise(setup, 1, Guidance(), Now.AddMinutes(2)));
        Assert.Throws<ArgumentException>(() => second.Revise(new TradingSetup("Other", null, Now), 2, Guidance(), Now.AddMinutes(2)));
    }

    [Fact]
    public void ArchiveBlocksNewRevisionsButKeepsHistoryAndReactivationUsesSameIdentity()
    {
        var setup = new TradingSetup("NQ", null, Now);
        var first = TradingSetupPlaybookRevision.Create(setup, Guidance(), Now);
        setup.Deactivate(Now.AddMinutes(1));
        Assert.Throws<InvalidOperationException>(() => first.Revise(setup, 1, Guidance(), Now.AddMinutes(2)));
        Assert.Throws<InvalidOperationException>(() => TradingSetupPlaybookRevision.Create(setup, Guidance(), Now.AddMinutes(2)));
        Assert.Equal("NQ", first.SetupName);
        setup.Activate(Now.AddMinutes(3));
        Assert.Equal(setup.Id, first.Revise(setup, 1, Guidance(), Now.AddMinutes(3)).TradingSetupId);
    }

    [Fact]
    public void RevisionAuditRequiresUtcAndMonotonicTime()
    {
        var setup = new TradingSetup("NQ", null, Now);
        Assert.Throws<ArgumentException>(() => TradingSetupPlaybookRevision.Create(setup, Guidance(), Now.ToOffset(TimeSpan.FromHours(2))));
        Assert.Throws<ArgumentException>(() => TradingSetupPlaybookRevision.Create(setup, Guidance(), Now.AddTicks(-1)));
        var first = TradingSetupPlaybookRevision.Create(setup, Guidance(), Now.AddMinutes(2));
        Assert.Throws<ArgumentException>(() => first.Revise(setup, 1, Guidance(), Now.AddMinutes(1)));
    }

    [Fact]
    public void FrozenTradeContextSurvivesRevisionArchiveAndClassificationChangesWithoutChangingEconomics()
    {
        var setup = new TradingSetup("NQ", null, Now);
        var first = TradingSetupPlaybookRevision.Create(setup, Guidance(), Now);
        Trade trade = CreateTrade();
        trade.SetTradingSetup(setup.Id, Now);
        var context = TradePlaybookContext.Capture(trade, setup, first, Now);
        Assert.False(context.IsRetrospective);
        Assert.Equal(trade.Id, context.TradeId);
        Assert.Equal(trade.OpenedAtUtc, context.TradeOpenedAtUtc);
        Assert.True(TradePlaybookContext.Capture(trade, setup, first, Now.AddSeconds(1)).IsRetrospective);
        _ = first.Revise(setup, 1, Guidance("Changed"), Now.AddMinutes(1));
        setup.Deactivate(Now.AddMinutes(2));
        Assert.Throws<InvalidOperationException>(() => TradePlaybookContext.Capture(trade, setup, first, Now.AddMinutes(3)));
        trade.SetTradingSetup(null, Now.AddMinutes(3));
        Assert.Same(first, context.Definition);
        Assert.Equal("NQ synthetic range", context.Definition.Guidance.MarketContext);
        Assert.Null(trade.GrossPnL);
        Assert.Null(trade.NetPnL);
        Assert.Single(trade.Executions);
    }

    [Fact]
    public void CaptureRejectsWrongClassificationWrongDefinitionAndBackdating()
    {
        var setup = new TradingSetup("NQ", null, Now);
        var other = new TradingSetup("Other", null, Now);
        var definition = TradingSetupPlaybookRevision.Create(setup, Guidance(), Now);
        Trade trade = CreateTrade();
        Assert.Throws<ArgumentException>(() => TradePlaybookContext.Capture(trade, setup, definition, Now));
        trade.SetTradingSetup(setup.Id, Now);
        Assert.Throws<ArgumentException>(() => TradePlaybookContext.Capture(trade, setup,
            TradingSetupPlaybookRevision.Create(other, Guidance(), Now), Now));
        Assert.Throws<ArgumentException>(() => TradePlaybookContext.Capture(trade, setup, definition, Now.AddTicks(-1)));
        Assert.Throws<ArgumentException>(() => TradePlaybookContext.Capture(trade, setup, definition, Now.ToOffset(TimeSpan.FromHours(1))));
        var later = definition.Revise(setup, 1, Guidance(), Now.AddMinutes(1));
        Assert.Throws<ArgumentException>(() => TradePlaybookContext.Capture(trade, setup, later, Now));
    }

    private static Trade CreateTrade() => Trade.Start(Guid.NewGuid(), Guid.NewGuid(),
        new TradePricingSnapshot(1m, "USD"),
        new TradeExecution(Guid.NewGuid(), 1, Now, ExecutionSide.Buy, 1m, 100m, null, null, null, null, null), Now);
}
