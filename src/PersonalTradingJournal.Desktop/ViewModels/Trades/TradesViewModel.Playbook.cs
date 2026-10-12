using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Trades;
using PersonalTradingJournal.Desktop.Screenshots;
using PersonalTradingJournal.Desktop.ViewModels.Setups;
using PersonalTradingJournal.Domain.Setups;

namespace PersonalTradingJournal.Desktop.ViewModels.Trades;

public sealed record CapturedAssociationView(int Sequence, string Heading, TradingSetupPlaybookRevision? Definition)
{
    public IReadOnlyList<PlaybookExampleItem> Examples { get; } = Definition?.Guidance.Examples.Select(e => new PlaybookExampleItem(e)).ToArray() ?? [];
    public string Guidance => Definition is not { } d ? "No captured Playbook revision. This is classification/removal history only." :
        $"Market context\n{d.Guidance.MarketContext}\n\nEntry conditions\n{d.Guidance.EntryConditions}\n\nConfirmations\n{d.Guidance.Confirmations}\n\nInvalidation\n{d.Guidance.Invalidation}\n\nExecution guidance\n{d.Guidance.ExecutionGuidance}";
}

public sealed partial class TradesViewModel
{
    private readonly ITradePlaybookAssociationStore? _playbookAssociations;
    private readonly ITradePlaybookContextStore? _playbookContexts;
    private readonly IPlaybookExampleImages? _playbookImages;
    private IReadOnlyList<CapturedAssociationView> _capturedAssociations = [];
    private string _playbookAssociationStatus = "No captured context loaded.";
    private int _associationReadGeneration;
    private int? _associationCursor;
    private bool _hasOlderAssociations;
    private TradePlaybookAssociationPreview? _selectedAssociationPreview;
    private string? _displayedTradeToken;
    private int _selectionPreviewGeneration;
    private string _selectedPlaybookPreviewText = "Select a Setup to preview its current revision.";
    public string SelectedPlaybookPreviewText { get => _selectedPlaybookPreviewText; private set => SetProperty(ref _selectedPlaybookPreviewText, value); }
    public bool HasOlderAssociations { get => _hasOlderAssociations; private set => SetProperty(ref _hasOlderAssociations, value); }
    public IReadOnlyList<CapturedAssociationView> CapturedAssociations
    { get => _capturedAssociations; private set => SetProperty(ref _capturedAssociations, value); }
    public string PlaybookAssociationStatus
    { get => _playbookAssociationStatus; private set => SetProperty(ref _playbookAssociationStatus, value); }
    public IAsyncRelayCommand LoadPlaybookHistoryCommand { get; }
    public IAsyncRelayCommand LoadOlderAssociationsCommand { get; private set; } = null!;
    public IAsyncRelayCommand RefreshAssociationPreviewCommand { get; private set; } = null!;
    public IAsyncRelayCommand<PlaybookExample> OpenCapturedImageCommand { get; }

    private async Task PrepareAssociationAsync(CancellationToken token)
    {
        int generation = ++_selectionPreviewGeneration;
        _selectedAssociationPreview = null;
        SaveTradingSetupCommand.NotifyCanExecuteChanged();
        if (_playbookAssociations is null || SelectedTradeDetail is not { } detail || SelectedTradeDetailTradingSetup is not { IsActive: true } setup)
        { SelectedPlaybookPreviewText = "Choose an active Setup for a new association. Archived captures remain readable."; return; }
        SelectedPlaybookPreviewText = "Loading the selected Setup revision…";
        try
        {
            var preview = await _playbookAssociations.PreviewAsync(detail.Id, setup.Id, token);
            if (generation != _selectionPreviewGeneration || !ReferenceEquals(detail, SelectedTradeDetail)) return;
            _selectedAssociationPreview = preview;
            SelectedPlaybookPreviewText = preview.Definition is { } d ? $"Selected: {preview.SetupName} · revision {d.Revision}. Confirm this exact revision." :
                $"Selected: {preview.SetupName} · no Playbook guidance; classification only.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception) { if (generation == _selectionPreviewGeneration) SelectedPlaybookPreviewText = "The selected Setup preview is unavailable. Refresh preview or choose an active Setup."; }
        finally { SaveTradingSetupCommand.NotifyCanExecuteChanged(); }
    }

    private Task LoadPlaybookHistoryAsync(CancellationToken token) => LoadAssociationPageAsync(null, token);
    private async Task LoadAssociationPageAsync(int? before, CancellationToken token)
    {
        if (_playbookAssociations is null || SelectedTradeDetail is not { } detail) return;
        int generation = ++_associationReadGeneration;
        if (before is null) PlaybookAssociationStatus = "Loading frozen association history…";
        try
        {
            var baseline = _displayedTradeToken is null ? await _playbookAssociations.PreviewAsync(detail.Id, null, token) : null;
            const int pageSize = 20;
            var history = await _playbookAssociations.ReadHistoryAsync(detail.Id, before, limit: pageSize + 1, cancellationToken: token);
            var legacy = _playbookContexts is null ? null : await _playbookContexts.GetAsync(detail.Id, token);
            if (!ReferenceEquals(detail, SelectedTradeDetail) || generation != _associationReadGeneration) return;
            var page = history.Take(pageSize).ToArray();
            _displayedTradeToken ??= baseline?.TradeToken;
            HasOlderAssociations = history.Count > pageSize;
            _associationCursor = page.LastOrDefault()?.Sequence;
            var rows = page.Select(a => new CapturedAssociationView(a.Sequence,
                a.Definition is { } d ? $"Association {a.Sequence} · {d.SetupName} · revision {d.Revision} (frozen)" :
                a.SetupId is null ? $"Association {a.Sequence} · removed" : $"Association {a.Sequence} · classification only", a.Definition)).ToList();
            if (legacy is not null && !HasOlderAssociations) rows.Add(new(0, $"Original capture · {legacy.Definition.SetupName} · revision {legacy.Definition.Revision}", legacy.Definition));
            CapturedAssociations = rows;
            if (before is not null) return;
            var current = page.Length > 0 ? page[0].Definition : legacy?.Definition;
            PlaybookAssociationStatus = current is not null && current.TradingSetupId == detail.TradingSetupId ?
                $"{current.SetupName} · captured revision {current.Revision}. Association is not verified adherence." :
                detail.TradingSetupId.HasValue ? "Legacy / no captured Playbook revision for the current Setup." : "No current Setup association. Earlier captures remain below.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception) { if (ReferenceEquals(detail, SelectedTradeDetail) && generation == _associationReadGeneration) PlaybookAssociationStatus = "Captured context could not be loaded. Try Open captured context again."; }
    }

    private async Task OpenCapturedImageAsync(PlaybookExample? example, CancellationToken token)
    {
        if (_playbookImages is null || example is null || IsTradingSetupSaving || IsTradeEditVisible || IsManualEntryVisible || IsCloseTradeVisible) return;
        IsTradingSetupSaving = true;
        try { await _playbookImages.ShowAsync(new PlaybookExampleItem(example), token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception) { PlaybookAssociationStatus = "The captured example image is unavailable. Its saved annotation remains readable."; }
        finally { IsTradingSetupSaving = false; }
    }
}
