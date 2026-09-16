using PersonalTradingJournal.Desktop.Dialogs;

namespace PersonalTradingJournal.Desktop.Screenshots;

public sealed class WpfTradeScreenshotDeleteConfirmation :
    ITradeScreenshotDeleteConfirmation
{
    private readonly IDialogService _dialogService;

    public WpfTradeScreenshotDeleteConfirmation(IDialogService dialogService)
    {
        ArgumentNullException.ThrowIfNull(dialogService);
        _dialogService = dialogService;
    }

    public bool Confirm(string fileName)
    {
        return _dialogService.Confirm(new ConfirmationDialogRequest(
            title: $"Delete screenshot \"{fileName}\"?",
            message: "This permanently removes the screenshot from the trade.",
            confirmButtonText: "Delete Screenshot",
            isDestructive: true));
    }
}
