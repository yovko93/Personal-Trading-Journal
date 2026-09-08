using CommunityToolkit.Mvvm.ComponentModel;

namespace PersonalTradingJournal.Desktop.ViewModels.Common;

public sealed class PlaceholderViewModel : ObservableObject
{
    public PlaceholderViewModel(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Message = message;
    }

    public string Message { get; }
}
