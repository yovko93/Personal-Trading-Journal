using CommunityToolkit.Mvvm.ComponentModel;

namespace PersonalTradingJournal.Desktop.Navigation;

public sealed class NavigationItemViewModel : ObservableObject
{
    private bool _isSelected;

    public NavigationItemViewModel(
        string title,
        NavigationDestination destination,
        string iconKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(iconKey);

        Title = title;
        Destination = destination;
        IconKey = iconKey;
    }

    public string Title { get; }

    public NavigationDestination Destination { get; }

    public string IconKey { get; }

    public bool IsSelected
    {
        get => _isSelected;
        private set => SetProperty(ref _isSelected, value);
    }

    internal void SetSelected(bool isSelected) => IsSelected = isSelected;
}
