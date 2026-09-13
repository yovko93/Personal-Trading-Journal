using System.Windows;

namespace PersonalTradingJournal.Desktop.Screenshots;

public sealed class WpfTradeScreenshotDeleteConfirmation :
    ITradeScreenshotDeleteConfirmation
{
    public bool Confirm(string fileName)
    {
        MessageBoxResult result = MessageBox.Show(
            $"Delete screenshot \"{fileName}\"?\n\n" +
            "This removes the screenshot from the trade.",
            "Delete Screenshot",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }
}
