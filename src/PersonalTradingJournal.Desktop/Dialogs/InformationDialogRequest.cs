namespace PersonalTradingJournal.Desktop.Dialogs;

public sealed class InformationDialogRequest
{
    public InformationDialogRequest(
        string title,
        string message,
        string closeButtonText = "Close")
    {
        Title = RequireText(title, nameof(title));
        Message = RequireText(message, nameof(message));
        CloseButtonText = RequireText(closeButtonText, nameof(closeButtonText));
    }

    public string Title { get; }

    public string Message { get; }

    public string CloseButtonText { get; }

    private static string RequireText(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }
}
