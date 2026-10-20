using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.TradingPlans;

namespace PersonalTradingJournal.Desktop.ViewModels.TradingPlans;

public sealed record PrincipleRow(int Number, DailyTradingPrinciple Value)
{
    public string Text => Value.Text;
}

/// <summary>Retained global editor. No Plan dirty tracking, snapshots, or review navigation.</summary>
public sealed class DailyTradingPrinciplesViewModel : ObservableObject, IDisposable
{
    private readonly IDailyTradingPrincipleStore? _store;
    private DailyTradingPrinciples _snapshot = new(0, []);
    private string _input = "", _error = "";
    private bool _busy, _loaded, _disposed;
    private CancellationTokenSource? _operation;
    private Task _completion = Task.CompletedTask;
    public DailyTradingPrinciplesViewModel(IDailyTradingPrincipleStore? store)
    {
        _store = store;
        AddCommand = new AsyncRelayCommand(() => RunWrite(null), () => CanWrite);
        RemoveCommand = new AsyncRelayCommand<PrincipleRow>(row => row is null ? Task.CompletedTask : RunWrite(row.Value.Id), row => CanWrite && row is not null);
        ReloadCommand = new AsyncRelayCommand(() => LoadAsync(true), () => !IsBusy && !_disposed && _store is not null);
        CancelCommand = new RelayCommand(() => _operation?.Cancel(), () => IsBusy);
    }
    public bool IsExpanded { get => _expanded; set => SetProperty(ref _expanded, value); }
    private bool _expanded = true;
    public string Input { get => _input; set { if (SetProperty(ref _input, value) && DailyTradingPrincipleLimits.IsValid(value)) Error = ""; } }
    public string Error { get => _error; private set { SetProperty(ref _error, value); OnPropertyChanged(nameof(HasError)); } }
    public int ValidationAttempt { get; private set; }
    public bool HasError => Error.Length != 0;
    public bool IsBusy => _busy;
    public bool CanWrite => _loaded && !_busy && !_disposed && _store is not null;
    public IReadOnlyList<PrincipleRow> Rows { get; private set; } = [];
    public PlanNotice Notice { get; private set; } = new("", PlanNoticeKind.Info);
    public IAsyncRelayCommand AddCommand { get; }
    public IAsyncRelayCommand<PrincipleRow> RemoveCommand { get; }
    public IAsyncRelayCommand ReloadCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public Task LoadAsync(bool refresh = false)
    {
        if (_disposed || _busy || _store is null || _loaded && !refresh) return _completion;
        return _completion = LoadCore();
    }
    private async Task LoadCore()
    {
        using var ct = Begin();
        try { var data = await Task.Run(() => _store!.ReadAsync(ct.Token), ct.Token); ct.Token.ThrowIfCancellationRequested(); Publish(data); Report(""); }
        catch (OperationCanceledException) { Report("Principles loading cancelled. Your input is retained; reload principles to try again."); }
        catch (Exception) { Report("Daily Trading Principles could not be loaded. Your input is retained; reload principles to try again.", PlanNoticeKind.Error); }
        finally { End(); }
    }
    private Task RunWrite(Guid? remove)
    {
        if (!CanWrite) return Task.CompletedTask;
        if (remove is null && !DailyTradingPrincipleLimits.IsValid(Input))
        { Error = "Enter a principle of 1–500 characters, not just whitespace."; IsExpanded = true; ValidationAttempt++; OnPropertyChanged(nameof(ValidationAttempt)); return Task.CompletedTask; }
        return _completion = WriteCore(remove);
    }
    private async Task WriteCore(Guid? remove)
    {
        string entered = Input; long version = _snapshot.Version;
        using var ct = Begin();
        try
        {
            var result = await Task.Run(() => remove is { } id ? _store!.RemoveAsync(id, version, ct.Token) : _store!.AddAsync(entered, version, ct.Token), ct.Token);
            if (result is { Status: PrincipleWriteStatus.Success, Snapshot: { } saved })
            { Publish(saved); if (remove is null && Input == entered) Input = ""; Error = ""; Report(""); }
            else if (result.Status == PrincipleWriteStatus.Conflict)
            {
                // Refresh only the independent list; never discard unsubmitted input or repeat the write.
                Publish(await Task.Run(() => _store!.ReadAsync(ct.Token), ct.Token));
                Report("Principles changed elsewhere. The saved list was reloaded; your input is retained. Review it before trying again.", PlanNoticeKind.Warning);
            }
            else Report(result.Status == PrincipleWriteStatus.Cancelled ? "Principle change cancelled; input retained." :
                result.Status == PrincipleWriteStatus.Invalid ? "The principle is invalid or the 1,000-entry list limit was reached. Input retained." :
                "Principle change could not be saved. Input retained; reload principles to check the saved list.",
                result.Status == PrincipleWriteStatus.Cancelled ? PlanNoticeKind.Info : PlanNoticeKind.Error);
        }
        catch (OperationCanceledException) { Report("Principle change cancelled; input retained."); }
        catch (Exception) { Report("Principle change could not be verified. Input retained; reload principles to check the saved list.", PlanNoticeKind.Error); }
        finally { End(); }
    }
    private CancellationTokenSource Begin() { _operation = new(); _busy = true; Notify(); return _operation; }
    private void End() { _operation = null; _busy = false; Notify(); }
    private void Publish(DailyTradingPrinciples data)
    { _snapshot = data; _loaded = true; Rows = data.Entries.Select((v, i) => new PrincipleRow(i + 1, v)).ToArray(); OnPropertyChanged(nameof(Rows)); }
    private void Report(string text, PlanNoticeKind kind = PlanNoticeKind.Info) { Notice = new(text, kind); OnPropertyChanged(nameof(Notice)); }
    private void Notify()
    {
        OnPropertyChanged(nameof(IsBusy)); OnPropertyChanged(nameof(CanWrite));
        AddCommand.NotifyCanExecuteChanged(); RemoveCommand.NotifyCanExecuteChanged(); ReloadCommand.NotifyCanExecuteChanged(); CancelCommand.NotifyCanExecuteChanged();
    }
    public async Task CancelAsync() { _operation?.Cancel(); await _completion; }
    public void Dispose() { _disposed = true; _operation?.Cancel(); }
}
