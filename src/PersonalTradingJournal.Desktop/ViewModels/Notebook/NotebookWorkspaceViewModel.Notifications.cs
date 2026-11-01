using CommunityToolkit.Mvvm.Input;

namespace PersonalTradingJournal.Desktop.ViewModels.Notebook;

public enum NotebookNotificationKind { Information, Success, Warning, Error }
public sealed record NotebookNotification(string Text, NotebookNotificationKind Kind)
{
    public string Icon => Kind switch { NotebookNotificationKind.Success => "✓", NotebookNotificationKind.Warning => "⚠", NotebookNotificationKind.Error => "!", _ => "ⓘ" };
    public string AccessibleText => $"{Kind}: {Text}";
}

public sealed partial class NotebookWorkspaceViewModel
{
    private CancellationTokenSource? _notificationTimer;
    private NotebookNotification? _notification;
    public NotebookNotification? Notification { get => _notification; private set => SetProperty(ref _notification, value); }
    public RelayCommand DismissNotificationCommand { get; private set; } = null!;
    internal Task NotificationExpiryTask { get; private set; } = Task.CompletedTask;

    private void ReportMessage(string text, NotebookNotificationKind kind)
    {
        if (!_active || _disposed) return;
        Message = text;
        ShowNotification(text, kind);
    }

    // An explicit operation event, not a binding/property-refresh subscription. Repeating
    // the same operation may start a fresh interval; rereading its bindings never does.
    internal void ShowNotification(string text, NotebookNotificationKind kind)
    {
        if (!_active || _disposed || string.IsNullOrWhiteSpace(text)) return;
        DismissNotification();
        var owner = new CancellationTokenSource();
        _notificationTimer = owner;
        Notification = new(text, kind);
        NotificationExpiryTask = ExpireAsync();
        async Task ExpireAsync()
        {
            try
            {
                // Retain the caller's UI synchronization context, just like autosave.
                await Task.Delay(TimeSpan.FromSeconds(10), _time, owner.Token);
                if (!_disposed && _active && ReferenceEquals(_notificationTimer, owner))
                {
                    _notificationTimer = null;
                    Notification = null;
                }
            }
            catch (OperationCanceledException) { }
            finally { owner.Dispose(); }
        }
    }

    private void DismissNotification()
    {
        var previous = _notificationTimer;
        _notificationTimer = null; // Invalidate even an already queued expiry continuation.
        previous?.Cancel();
        Notification = null;
        // Message, validation, canonical save state and recovery are intentionally untouched.
    }
}
