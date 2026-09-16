using System.Windows;

namespace PersonalTradingJournal.Desktop.Dialogs;

public partial class PtjDialogWindow : Window
{
    private readonly bool _isConfirmation;

    public PtjDialogWindow(ConfirmationDialogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        InitializeComponent();
        _isConfirmation = true;
        ConfigureCommonContent(request.Title, request.Message);
        CancelButton.Content = request.CancelButtonText;
        PrimaryButton.Content = request.ConfirmButtonText;
        PrimaryButton.Style = (Style)FindResource(
            request.IsDestructive
                ? "PtjDangerButtonStyle"
                : "PtjPrimaryButtonStyle");
    }

    public PtjDialogWindow(InformationDialogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        InitializeComponent();
        ConfigureCommonContent(request.Title, request.Message);
        CancelButton.Visibility = Visibility.Collapsed;
        CancelButton.IsDefault = false;
        PrimaryButton.Content = request.CloseButtonText;
        PrimaryButton.IsCancel = true;
        PrimaryButton.IsDefault = true;
        PrimaryButton.Style = (Style)FindResource("PtjPrimaryButtonStyle");
    }

    private void ConfigureCommonContent(string title, string message)
    {
        Title = title;
        DialogTitle.Text = title;
        DialogMessage.Text = message;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) =>
        DialogResult = false;

    private void PrimaryButton_Click(object sender, RoutedEventArgs e) =>
        DialogResult = _isConfirmation;
}
