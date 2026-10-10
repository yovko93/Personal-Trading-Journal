using System.Windows.Controls;
using System.Windows;
using PersonalTradingJournal.Desktop.ViewModels.Settings;

namespace PersonalTradingJournal.Desktop.Views.Settings;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        Loaded += (_, _) => (DataContext as SettingsViewModel)?.RefreshCredentialStatus();
        Unloaded += (_, _) => ApiKeyEntry.Clear();
    }

    private void SaveKeyClick(object sender, RoutedEventArgs e)
    {
        try { (DataContext as SettingsViewModel)?.SaveKey(ApiKeyEntry.Password); }
        finally { ApiKeyEntry.Clear(); }
    }
    private void ProviderSelectionChanged(object sender, SelectionChangedEventArgs e) => ApiKeyEntry?.Clear();
}
