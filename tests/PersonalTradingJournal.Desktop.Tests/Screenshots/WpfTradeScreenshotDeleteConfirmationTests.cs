using PersonalTradingJournal.Desktop.Screenshots;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;

namespace PersonalTradingJournal.Desktop.Tests.Screenshots;

public sealed class WpfTradeScreenshotDeleteConfirmationTests
{
    [Fact]
    public void ConfirmUsesSharedDestructiveDialogWithContextualFileName()
    {
        var dialogService = new FakeDialogService
        {
            ConfirmationResult = true,
        };
        var confirmation = new WpfTradeScreenshotDeleteConfirmation(dialogService);

        bool result = confirmation.Confirm("entry.png");

        Assert.True(result);
        Assert.NotNull(dialogService.ConfirmationRequest);
        Assert.Equal(
            "Delete screenshot \"entry.png\"?",
            dialogService.ConfirmationRequest.Title);
        Assert.Equal(
            "Delete Screenshot",
            dialogService.ConfirmationRequest.ConfirmButtonText);
        Assert.True(dialogService.ConfirmationRequest.IsDestructive);
    }
}
