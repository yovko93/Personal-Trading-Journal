using System.ComponentModel;
using System.Windows;

namespace PersonalTradingJournal.Desktop.Dialogs;

/// <summary>Collects non-destructive consent before ending owned modal frames.</summary>
internal static class WindowCloseAttempt
{
    // Accessed only on the windows' dispatcher. Each attempt owns its consent; a veto
    // must not authorize a later close. Nested confirmation windows may pump messages.
    [ThreadStatic] private static Attempt? _current;
    private sealed class Attempt
    {
        public HashSet<Window> Preparing { get; } = [];
        public Dictionary<object, bool> Consent { get; } = new(ReferenceEqualityComparer.Instance);
        public HashSet<Window> Registered { get; } = [];
        public List<Action> Complete { get; } = [];
    }

    public static void Run(Window owner, CancelEventArgs args, Action raiseClosing,
        Func<bool> canClose, Action completeClose)
    {
        if (_current is not null) { args.Cancel = true; return; }
        var attempt = _current = new Attempt();
        try
        {
            if (!Prepare(owner, args, raiseClosing, canClose)) return;
            // This is the final cancellation result, not a deferred dispatcher guess.
            foreach (var closeModal in attempt.Complete) closeModal();
            completeClose();
        }
        finally { _current = null; }
    }

    public static bool Check(object editor, Func<bool> guard)
    {
        if (_current is null) return guard();
        if (!_current.Consent.TryGetValue(editor, out bool accepted))
            _current.Consent.Add(editor, accepted = guard());
        return accepted;
    }

    private static bool Prepare(Window owner, CancelEventArgs args, Action raiseClosing, Func<bool> canClose)
    {
        if (_current is null || !_current.Preparing.Add(owner)) { args.Cancel = true; return false; }
        try
        {
            raiseClosing(); // All subscribers run, including owned-window preparation.
            if (args.Cancel || !canClose()) { args.Cancel = true; return false; }
            return true;
        }
        finally { _current.Preparing.Remove(owner); }
    }

    public static bool PrepareOwned(Window owner, Window child, Action<CancelEventArgs> raiseClosing,
        Func<bool> canClose, Action closeAccepted)
    {
        if (_current?.Preparing.Contains(owner) != true) return false;
        var args = new CancelEventArgs();
        if (!Prepare(child, args, () => raiseClosing(args), canClose)) return false;
        Register(child, closeAccepted);
        return true;
    }

    public static bool AfterAcceptance(Window owner, Window child, Action closeModal)
    {
        if (_current?.Preparing.Contains(owner) != true) return false;
        Register(child, closeModal);
        return true;
    }

    private static void Register(Window child, Action closeModal)
    {
        if (_current!.Registered.Add(child)) _current.Complete.Add(closeModal);
    }
}
