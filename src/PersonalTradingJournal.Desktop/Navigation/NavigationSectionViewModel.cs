using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PersonalTradingJournal.Desktop.Navigation;

public sealed class NavigationSectionViewModel : ObservableObject
{
    private bool _isExpanded = true;

    public NavigationSectionViewModel(
        string title,
        IReadOnlyList<NavigationItemViewModel> items)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            throw new ArgumentException(
                "A navigation section must contain at least one item.",
                nameof(items));
        }

        Title = title;
        Items = items;
        ToggleCommand = new RelayCommand(Toggle);
    }

    public string Title { get; }

    public IReadOnlyList<NavigationItemViewModel> Items { get; }

    public bool IsCollapsible => true;

    public bool IsExpanded
    {
        get => _isExpanded;
        private set => SetProperty(ref _isExpanded, value);
    }

    public IRelayCommand ToggleCommand { get; }

    internal bool Contains(NavigationDestination destination) =>
        Items.Any(item => item.Destination == destination);

    internal void Expand() => IsExpanded = true;

    private void Toggle() => IsExpanded = !IsExpanded;
}
