using PersonalTradingJournal.Application.TradingPlans;
using PersonalTradingJournal.Domain.TradingPlans;

namespace PersonalTradingJournal.Application.Tests.TradingPlans;

public sealed class TradingPlanReadinessSubmissionTests
{
    [Theory]
    [InlineData(TradingPlanWriteOperation.MarkReady)] [InlineData(TradingPlanWriteOperation.RefreshReferences)] [InlineData(TradingPlanWriteOperation.ConfirmChecklist)]
    public void CombinedConfirmationOnlyExtendsSaveDraft(TradingPlanWriteOperation operation)
    {
        Assert.Throws<ArgumentException>(() => new TradingPlanWriteCommand(operation, new(Guid.NewGuid(), new(2026, 10, 17)), new(),
            new(Guid.NewGuid(), 1), explicitConfirmations: TradingPlanChecklist.All));
    }
    [Theory] [InlineData("")] [InlineData("usd")] [InlineData(" USD ")] [InlineData("ABCDEFGHI")]
    public void CurrencyAcknowledgementMustBeCanonical(string currency)
    {
        Assert.Throws<ArgumentException>(() => new TradingPlanWriteCommand(TradingPlanWriteOperation.SaveDraft,
            new(Guid.NewGuid(), new(2026, 10, 17)), new(), new(Guid.NewGuid(), 1), expectedAccountCurrency: currency));
    }
    [Fact]
    public void ExplicitSubmissionIsIndependentOfIgnoredIncomingContentChecks()
    {
        var key = new TradingPlanKey(Guid.NewGuid(), new(2026, 10, 17)); var state = new TradingPlanExpectedState(Guid.NewGuid(), 1);
        var legacy = new TradingPlanWriteCommand(TradingPlanWriteOperation.SaveDraft, key, new(confirmed: TradingPlanChecklist.All), state);
        Assert.Null(legacy.ExplicitConfirmations);
        var explicitSubmission = new TradingPlanWriteCommand(TradingPlanWriteOperation.SaveDraft, key, legacy.Content, state,
            explicitConfirmations: TradingPlanChecklist.None, expectedAccountCurrency: "USD");
        Assert.Equal(TradingPlanChecklist.None, explicitSubmission.ExplicitConfirmations);
        Assert.Equal("USD", explicitSubmission.ExpectedAccountCurrency);
        Assert.Throws<ArgumentOutOfRangeException>(() => new TradingPlanWriteCommand(TradingPlanWriteOperation.SaveDraft, key, new(), state,
            explicitConfirmations: (TradingPlanChecklist)32));
    }
}
