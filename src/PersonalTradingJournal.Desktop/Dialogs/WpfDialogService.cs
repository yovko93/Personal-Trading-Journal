using System.Windows;

namespace PersonalTradingJournal.Desktop.Dialogs;

public sealed class WpfDialogService : IDialogService
{
    public bool Confirm(ConfirmationDialogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dialog = new PtjDialogWindow(request);
        ConfigureOwner(dialog);
        return dialog.ShowDialog() == true;
    }

    public void ShowInformation(InformationDialogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dialog = new PtjDialogWindow(request);
        ConfigureOwner(dialog);
        _ = dialog.ShowDialog();
    }

    private static void ConfigureOwner(Window dialog)
    {
        Window? owner = System.Windows.Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive)
            ?? System.Windows.Application.Current?.MainWindow;

        if (owner?.IsVisible == true)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }
}
