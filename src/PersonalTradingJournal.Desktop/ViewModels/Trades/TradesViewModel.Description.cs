using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Trades;

namespace PersonalTradingJournal.Desktop.ViewModels.Trades;

public sealed partial class TradesViewModel
{
    private readonly IImportedTradeDescriptionStore? _importedDescriptionStore;
    private string _tradeDescriptionDraft = string.Empty;
    private string? _tradeDescriptionError;
    private bool _isSavingTradeDescription;

    public bool IsDescriptionEditorOpen => _classificationEditor == ClassificationEditor.Description;
    public string TradeDescriptionDraft
    {
        get => _tradeDescriptionDraft;
        set => SetProperty(ref _tradeDescriptionDraft, value);
    }
    public string? TradeDescriptionError
    {
        get => _tradeDescriptionError;
        private set => SetProperty(ref _tradeDescriptionError, value);
    }
    public bool IsSavingTradeDescription
    {
        get => _isSavingTradeDescription;
        private set
        {
            if (!SetProperty(ref _isSavingTradeDescription, value)) return;
            SaveTradeDescriptionCommand.NotifyCanExecuteChanged();
            CancelDescriptionEditorCommand.NotifyCanExecuteChanged();
        }
    }

    public IRelayCommand OpenDescriptionEditorCommand { get; private set; } = null!;
    public IRelayCommand CancelDescriptionEditorCommand { get; private set; } = null!;
    public IAsyncRelayCommand SaveTradeDescriptionCommand { get; private set; } = null!;

    private void InitializeDescriptionCommands()
    {
        OpenDescriptionEditorCommand = new RelayCommand(() =>
        {
            if (!CanOpenDescriptionEditor()) return;
            TradeDescriptionDraft = SelectedTradeDetail!.Description ?? string.Empty;
            TradeDescriptionError = null;
            SetClassificationEditor(ClassificationEditor.Description);
        }, CanOpenDescriptionEditor);
        CancelDescriptionEditorCommand = new RelayCommand(() =>
        {
            if (CanFinishDescriptionEdit()) SetClassificationEditor(ClassificationEditor.None);
        }, CanFinishDescriptionEdit);
        SaveTradeDescriptionCommand = new AsyncRelayCommand(SaveTradeDescriptionAsync, CanFinishDescriptionEdit);
    }

    private bool CanOpenDescriptionEditor() =>
        _importedDescriptionStore is not null && SelectedTradeDetail is { IsImported: true } && CanOpenClassificationEditor();

    private bool CanFinishDescriptionEdit() => IsDescriptionEditorOpen && !IsSavingTradeDescription;

    private async Task SaveTradeDescriptionAsync()
    {
        if (!CanFinishDescriptionEdit() || SelectedTradeDetail is not { IsImported: true } detail ||
            _importedDescriptionStore is null) return;

        IsSavingTradeDescription = true;
        TradeDescriptionError = null;
        try
        {
            var saved = await _importedDescriptionStore.SaveDescriptionAsync(detail.Id, TradeDescriptionDraft);
            if (SelectedTradeDetail is { } current && current.Id == detail.Id && IsDescriptionEditorOpen)
            {
                SelectedTradeDetail = current with { Description = saved };
                SetClassificationEditor(ClassificationEditor.None);
            }
        }
        catch (ArgumentException error)
        {
            TradeDescriptionError = error.Message;
        }
        catch (Exception)
        {
            TradeDescriptionError = "The Trade description could not be saved. Your text is still available.";
        }
        finally
        {
            IsSavingTradeDescription = false;
        }
    }
}
