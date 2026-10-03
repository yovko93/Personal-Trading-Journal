using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PersonalTradingJournal.Desktop.Views.Calendar;

/// <summary>A focusable date container with a UI Automation invoke surface.</summary>
public sealed class CalendarDayHost : Border
{
    public static readonly RoutedEvent InvokedEvent = EventManager.RegisterRoutedEvent(
        nameof(Invoked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(CalendarDayHost));

    public event RoutedEventHandler Invoked
    {
        add => AddHandler(InvokedEvent, value);
        remove => RemoveHandler(InvokedEvent, value);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new DayPeer(this);

    private sealed class DayPeer(CalendarDayHost owner) : FrameworkElementAutomationPeer(owner), IInvokeProvider
    {
        protected override string GetClassNameCore() => nameof(CalendarDayHost);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;
        public override object? GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.Invoke ? this : base.GetPattern(patternInterface);
        public void Invoke()
        {
            if (!IsEnabled()) throw new System.Windows.Automation.ElementNotEnabledException();
            _ = owner.Dispatcher.BeginInvoke(DispatcherPriority.Input,
                new Action(() => owner.RaiseEvent(new RoutedEventArgs(InvokedEvent, owner))));
        }
    }
}
