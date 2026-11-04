namespace PersonalTradingJournal.Desktop.ViewModels.Calendar;

public sealed partial class CalendarViewModel
{
    // The read owns completion; callers own cancellation leases. CTS.Dispose is
    // not safe against Cancel, so the source is disposed only when both are done.
    // Cancel runs outside the gate: callbacks may complete/reenter the view model.
    private sealed class ReadCancellation : IDisposable
    {
        private readonly object _gate = new();
        private readonly CancellationTokenSource _source = new();
        private int _cancelers;
        private bool _completed;
        internal ReadCancellation() => Token = _source.Token;
        internal CancellationToken Token { get; }
        internal bool IsCancellationRequested => Token.IsCancellationRequested;

        internal void Cancel()
        {
            lock (_gate)
            {
                if (_completed) return; // No unfinished read remains to cancel.
                _cancelers++;
            }
            try { _source.Cancel(); }
            finally
            {
                lock (_gate)
                {
                    if (--_cancelers == 0 && _completed) _source.Dispose();
                }
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_completed) return;
                _completed = true;
                if (_cancelers == 0) _source.Dispose();
            }
        }
    }
}
