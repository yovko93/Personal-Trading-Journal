using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Desktop.Dialogs;
using PersonalTradingJournal.Desktop.ViewModels.PeriodReviews;

namespace PersonalTradingJournal.Desktop.ViewModels.DailyReview;

public interface IReviewClipboard
{
    void Copy(string text);
    string Paste();
}

/// <summary>Selection-independent pending workflow; the service has no provider dependency.</summary>
public sealed class ManualReviewViewModel : ObservableObject
{
    private readonly IManualReviewService _service;
    private readonly ManualReviewKind _kind;
    private readonly Func<ManualReviewRequest?> _selection;
    private readonly IDialogService _dialogs;
    private readonly IReviewClipboard _clipboard;
    private readonly Func<Task> _saved;
    private ManualReviewDraft? _draft;
    private string _response = "";
    private string? _message;
    private bool _busy, _loaded;
    private CancellationTokenSource? _operation;

    public ManualReviewViewModel(IManualReviewService service, ManualReviewKind kind, Func<ManualReviewRequest?> selection,
        IDialogService dialogs, IReviewClipboard clipboard, Func<Task> saved)
    {
        _service = service; _kind = kind; _selection = selection; _dialogs = dialogs; _clipboard = clipboard; _saved = saved;
        PrepareCommand = new AsyncRelayCommand(() => Run(async token =>
        {
            var selection = _selection();
            if (selection is null) { Message = "Choose an available Account and a valid New York date."; return; }
            SetDraft(await _service.PrepareAsync(selection, token)); Message = "Prompt prepared locally. Inspect it before copying. Nothing has been sent.";
        }), () => !IsBusy && _loaded && _draft is null);
        CopyCommand = new RelayCommand(() =>
        {
            try { _clipboard.Copy(_draft!.Prompt); Message = "Prompt copied on your request. You decide where to paste this sensitive information."; }
            catch (Exception) { Message = "Clipboard unavailable. Prepared evidence is safe; select/copy the prompt manually or try Copy Prompt again."; }
        }, () => !IsBusy && _draft is not null);
        PasteCommand = new RelayCommand(() =>
        {
            try { ResponseText = _clipboard.Paste(); Message = "Response pasted. Validate it before saving."; }
            catch (Exception) { Message = "Clipboard unavailable. Paste or type into the response field; prepared evidence is unchanged."; }
        }, () => !IsBusy && _draft is { Saved: false });
        KeepCommand = new AsyncRelayCommand(() => Run(async token =>
        {
            SetDraft(await _service.KeepResponseAsync(_draft!, ResponseText, token)); Message = "Response draft retained locally. It is not a saved analysis.";
        }), () => !IsBusy && _draft is { Saved: false } && IsDirty);
        ValidateCommand = new AsyncRelayCommand(() => Run(async token =>
        {
            if (IsDirty) SetDraft(await _service.KeepResponseAsync(_draft!, ResponseText, token));
            var result = _service.Validate(_draft!, token); Preview = null;
            if (result.Valid) Preview = result.Daily is { } daily ? ReviewSnapshot.Read(daily) : PeriodReport.Read(result.Period!);
            Message = result.Message;
        }), () => !IsBusy && _draft is { Saved: false } && !string.IsNullOrWhiteSpace(ResponseText));
        SaveCommand = new AsyncRelayCommand(() => Run(async token =>
        {
            var preview = Preview;
            SetDraft(await _service.SaveAsync(_draft!, token));
            Preview = preview;
            Message = "Review saved. Pasted external AI response; originating service and claims are unverified.";
            await _saved();
        }), () => !IsBusy && _draft is { Saved: false } && !IsDirty && Preview is not null);
        DiscardCommand = new AsyncRelayCommand(() => Run(async token =>
        {
            if (!_dialogs.Confirm(new("Discard pending review?", $"Discard the local prompt and response for {_draft!.ScopeLabel}? Saved analyses are not deleted.", "Discard pending work"))) return;
            await _service.DiscardAsync(_draft!, token); SetDraft(null); Message = "Pending work discarded. Saved analyses are unchanged.";
        }), () => !IsBusy && _draft is not null);
        CancelCommand = new RelayCommand(() => _operation?.Cancel(), () => IsBusy && _operation?.IsCancellationRequested == false);
        ReloadCommand = new AsyncRelayCommand(() => Run(async token =>
        {
            if (IsDirty && !_dialogs.Confirm(new("Reload pending work?", "Replace the unsaved response in this editor with its last retained working copy? Keep or copy the text first if needed.", "Reload pending work"))) return;
            SetDraft(await _service.LoadAsync(_kind, token)); _loaded = true;
            Message = "Pending work reloaded with its frozen scope. Nothing was sent.";
        }), () => !IsBusy);
    }
    public string ResponseText
    {
        get => _response;
        set { if (SetProperty(ref _response, value ?? "")) { Preview = null; Notify(); } }
    }
    public string? Message { get => _message; private set => SetProperty(ref _message, value); }
    public bool IsBusy => _busy;
    public bool HasPending => _draft is not null;
    public bool IsPeriod => _kind != ManualReviewKind.Daily;
    public bool CanEditResponse => !IsBusy && _draft is { Saved: false };
    public bool IsDirty => _draft is not null && ResponseText != _draft.ResponseText;
    public string Prompt => _draft?.Prompt ?? "";
    public string PendingScope => _draft is null ? "" : $"Frozen {_draft.ScopeLabel} · prepared {_draft.PreparedAtUtc:u}. This does not follow the selectors above.";
    public string Size => _draft is null ? "" : $"Exact prompt: {System.Text.Encoding.UTF8.GetByteCount(Prompt):N0} UTF-8 bytes. PTJ limit 300,000 bytes; ChatGPT limits may be lower.";
    public string Disclosure => "Prepare locally, copy only when ready, then paste the JSON response from ChatGPT. No API key or automatic upload. Prompts contain sensitive Trade and Journal information, frozen Playbook context and saved Ready Trading Plan preparation. Period prompts also include recorded Rule coverage; individual examples are bounded and explicitly identified.";
    public string Details => "Daily includes the complete daily evidence; weekly/monthly include complete aggregates and explicitly bounded source examples. Local snapshots retain all source facts. No chart images are copied. Pending prompts and responses are unencrypted local working files, retained across restart but excluded from portable backups/exports. Keep response draft before leaving. Saved reviews remain in normal backup/export history. PTJ verifies structure and citations, not the service used or the truth of an AI claim.";
    public object? Preview { get; private set; }
    public IReadOnlyList<ManualPreviewCard> PreviewCards => Preview switch
    {
        PeriodReport report => report.Cards.Select(c => new ManualPreviewCard(c.Heading, c.Note + "\n" + string.Join("\n\n", c.Findings.Select(f => f.Text + "\n" + string.Join("; ", f.Citations.Select(s => s.Label)))))).ToArray(),
        ReviewSnapshot day => day.Statements.Select(s => new ManualPreviewCard(s.Heading, s.Text + "\n" + string.Join("; ", s.Citations.Select(c => c.Label)))).ToArray(),
        _ => [],
    };
    public IAsyncRelayCommand PrepareCommand { get; }
    public IRelayCommand CopyCommand { get; }
    public IRelayCommand PasteCommand { get; }
    public IAsyncRelayCommand KeepCommand { get; }
    public IAsyncRelayCommand ValidateCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand DiscardCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IAsyncRelayCommand ReloadCommand { get; }

    public Task LoadAsync() => _loaded ? Task.CompletedTask : Run(async token =>
    {
        SetDraft(await _service.LoadAsync(_kind, token)); _loaded = true;
        if (_draft is not null) Message = _draft.Saved ? "This pending review is already saved. Discard its working copy to prepare another." : "Pending review restored with its original frozen scope. No request was sent.";
    });
    public bool TryLeave()
    {
        if (IsBusy) { Message = "Wait for this local operation to finish, or cancel it, before leaving."; return false; }
        if (IsDirty) { Message = "Unsaved response: choose Keep response draft or explicitly Discard pending work before leaving."; return false; }
        return true;
    }
    private void SetDraft(ManualReviewDraft? draft)
    {
        _draft = draft; _response = draft?.ResponseText ?? ""; Preview = null; Notify();
    }
    private async Task Run(Func<CancellationToken, Task> work)
    {
        if (_busy) return;
        using var cts = new CancellationTokenSource(); _operation = cts; _busy = true; Notify();
        try { await work(cts.Token); }
        catch (OperationCanceledException) { Message = "Local operation cancelled. Pending text is retained; no provider was contacted. Reopen history if Save coincided with commit."; }
        catch (ManualReviewException ex) { Message = ex.Message; }
        catch (Exception) { Message = "Local review operation failed. Text and prepared evidence are retained. Check storage permissions, then retry; if Save may have committed, check history first. No provider was contacted."; }
        finally { _busy = false; _operation = null; Notify(); }
    }
    private void Notify()
    {
        OnPropertyChanged(string.Empty);
        foreach (var command in new IRelayCommand[] { PrepareCommand, CopyCommand, PasteCommand, KeepCommand, ValidateCommand, SaveCommand, DiscardCommand, CancelCommand, ReloadCommand }) command?.NotifyCanExecuteChanged();
    }
}
public sealed record ManualPreviewCard(string Heading, string Text);
