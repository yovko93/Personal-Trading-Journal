namespace PersonalTradingJournal.Desktop.ViewModels.Trades;

public sealed partial class TradesViewModel
{
    private readonly TimeProvider _setupFeedbackClock;
    private CancellationTokenSource? _setupSuccessLifetime;
    internal Task SetupSuccessExpiryTask { get; private set; } = Task.CompletedTask;

    private void ShowTradingSetupSuccess()
    {
        var lifetime = new CancellationTokenSource();
        _setupSuccessLifetime = lifetime;
        SetupSuccessExpiryTask = ExpireSetupSuccessAsync(lifetime);
        TradingSetupSuccessMessage = TradingSetupSavedMessage;
    }

    private void ClearTradingSetupSuccess()
    {
        var lifetime = _setupSuccessLifetime;
        _setupSuccessLifetime = null;
        lifetime?.Cancel();
        TradingSetupSuccessMessage = null;
    }

    private async Task ExpireSetupSuccessAsync(CancellationTokenSource lifetime)
    {
        try
        {
            // Continue on the UI context captured when the command published feedback.
            await Task.Delay(TimeSpan.FromSeconds(5), _setupFeedbackClock, lifetime.Token);
            if (!ReferenceEquals(_setupSuccessLifetime, lifetime)) return;

            _setupSuccessLifetime = null;
            TradingSetupSuccessMessage = null;
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            // A newer operation or leaving the view invalidated this message.
        }
        finally
        {
            lifetime.Dispose();
        }
    }
}