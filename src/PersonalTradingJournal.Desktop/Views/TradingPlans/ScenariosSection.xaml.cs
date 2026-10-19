using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PersonalTradingJournal.Desktop.Interactions;
using PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

namespace PersonalTradingJournal.Desktop.Views.TradingPlans;

public partial class ScenariosSection : UserControl
{
    private readonly PageFocus _focus;
    public static readonly DependencyProperty CardWidthProperty = DependencyProperty.Register(nameof(CardWidth), typeof(double), typeof(ScenariosSection), new PropertyMetadata(400d));
    public double CardWidth { get => (double)GetValue(CardWidthProperty); private set => SetValue(CardWidthProperty, value); }
    public ScenariosSection() { InitializeComponent(); _focus = new(this); }
    // Leave rounding headroom: two half-DIP widths can both round up and wrap
    // the second card on an odd-width production viewport.
    private void OnCardsSizeChanged(object sender, SizeChangedEventArgs e) => CardWidth = e.NewSize.Width >= 900 ? Math.Floor(e.NewSize.Width / 2) : e.NewSize.Width;
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        _focus.Invalidate();
        if (e.OldValue is SessionPreparationViewModel old) PropertyChangedEventManager.RemoveHandler(old, OnStateChanged, "");
        if (e.NewValue is SessionPreparationViewModel current) PropertyChangedEventManager.AddHandler(current, OnStateChanged, "");
    }
    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SessionPreparationViewModel.ValidationAttempt) || sender is not SessionPreparationViewModel vm || vm.FirstInvalidField is not { } field) return;
        if (field != vm.StandAside && !vm.Scenarios.Any(s => s.Fields.Contains(field))) return;
        _focus.Request(() => Descendants(this).OfType<TextBox>().FirstOrDefault(t => ReferenceEquals(t.DataContext, field)));
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var next in Descendants(child)) yield return next;
        }
    }
}
