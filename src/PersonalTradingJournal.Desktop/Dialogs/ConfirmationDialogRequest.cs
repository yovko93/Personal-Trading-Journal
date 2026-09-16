namespace PersonalTradingJournal.Desktop.Dialogs;

public sealed class ConfirmationDialogRequest
{
    public ConfirmationDialogRequest(
        string title,
        string message,
        string confirmButtonText,
        string cancelButtonText = "Cancel",
        bool isDestructive = false)
    {
        Title = RequireText(title, nameof(title));
        Message = RequireText(message, nameof(message));
        ConfirmButtonText = RequireText(confirmButtonText, nameof(confirmButtonText));
        CancelButtonText = RequireText(cancelButtonText, nameof(cancelButtonText));
        IsDestructive = isDestructive;
    }

    public string Title { get; }

    public string Message { get; }

    public string ConfirmButtonText { get; }

    public string CancelButtonText { get; }

    public bool IsDestructive { get; }

    private static string RequireText(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }
}
