using System.Windows;
using System.Windows.Controls;

namespace PersonalTradingJournal.Desktop.Views.Journals;

public partial class JournalHistoryView : UserControl
{
    public JournalHistoryView()
    {
        InitializeComponent();
        SizeChanged += (_, e) => SetValue(IsCompactProperty, e.NewSize.Width < 720);
    }

    // Pure layout state; never changes history filters, pages or editor state.
    public static readonly DependencyProperty IsCompactProperty = DependencyProperty.Register(
        nameof(IsCompact), typeof(bool), typeof(JournalHistoryView), new PropertyMetadata(false));
    public bool IsCompact => (bool)GetValue(IsCompactProperty);
}
