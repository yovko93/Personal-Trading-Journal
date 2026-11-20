using CommunityToolkit.Mvvm.Input;

namespace PersonalTradingJournal.Desktop.ViewModels.Trades;

public sealed partial class TradesViewModel
{
    private enum ClassificationEditor { None, Setup, Mistake, Description }

    private ClassificationEditor _classificationEditor;

    public bool IsSetupEditorOpen => _classificationEditor == ClassificationEditor.Setup;
    public bool IsMistakeEditorOpen => _classificationEditor == ClassificationEditor.Mistake;
    public bool HasTradeClassificationDraft => IsSetupEditorOpen || IsMistakeEditorOpen;
    public bool HasTradeDetailDraft => _classificationEditor != ClassificationEditor.None;
    public bool HasSavedTradeSetup => SelectedTradeDetail?.TradingSetupId is not null;
    public string TradeSetupEditorLabel => HasSavedTradeSetup ? "Change Setup" : "Add Setup";

    public IRelayCommand OpenSetupEditorCommand { get; private set; } = null!;
    public IRelayCommand CancelSetupEditorCommand { get; private set; } = null!;
    public IRelayCommand OpenMistakeEditorCommand { get; private set; } = null!;
    public IRelayCommand CancelMistakeEditorCommand { get; private set; } = null!;

    private void InitializeClassificationCommands()
    {
        OpenSetupEditorCommand = new RelayCommand(
            () =>
            {
                if (!CanOpenClassificationEditor()) return;
                TradingSetupSaveErrorMessage = null;
                SynchronizeTradeDetailTradingSetupOptions();
                SetClassificationEditor(ClassificationEditor.Setup);
            }, CanOpenClassificationEditor);
        CancelSetupEditorCommand = new RelayCommand(
            () =>
            {
                if (!IsSetupEditorOpen || IsTradingSetupSaving) return;
                TradingSetupSaveErrorMessage = null;
                SetClassificationEditor(ClassificationEditor.None);
                SynchronizeTradeDetailTradingSetupOptions();
            }, () => IsSetupEditorOpen && !IsTradingSetupSaving);
        OpenMistakeEditorCommand = new RelayCommand(
            () =>
            {
                if (!CanOpenClassificationEditor()) return;
                AssignMistakeErrorMessage = null;
                AssignMistakeSuccessMessage = null;
                SetClassificationEditor(ClassificationEditor.Mistake);
            }, CanOpenClassificationEditor);
        CancelMistakeEditorCommand = new RelayCommand(
            () =>
            {
                if (!IsMistakeEditorOpen || IsAssigningMistake) return;
                SelectedTradingMistake = null;
                TradeMistakeNoteText = string.Empty;
                AssignMistakeErrorMessage = null;
                SetClassificationEditor(ClassificationEditor.None);
            }, () => IsMistakeEditorOpen && !IsAssigningMistake);

        PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(SelectedTradeDetail) or nameof(IsTradeDetailVisible)
                or nameof(IsTradeDetailLoading) or nameof(IsTradeMistakesLoading) or nameof(IsTradeScreenshotsLoading)
                or nameof(IsTradeEditVisible) or nameof(IsManualEntryVisible) or nameof(IsSaving) or nameof(IsDeletingTrade)
                or nameof(IsCloseTradeVisible) or nameof(IsClosingTrade) or nameof(IsAddScreenshotVisible)
                or nameof(IsAddingScreenshot) or nameof(IsDeletingScreenshot) or nameof(IsTradingSetupSaving)
                or nameof(IsAssigningMistake) or nameof(IsRemovingMistake) or nameof(HasTradeDetailDraft) or nameof(IsUpdatingTrade))
            {
                OpenDescriptionEditorCommand.NotifyCanExecuteChanged();
                OpenSetupEditorCommand.NotifyCanExecuteChanged();
                OpenMistakeEditorCommand.NotifyCanExecuteChanged();
                CancelSetupEditorCommand.NotifyCanExecuteChanged();
                CancelMistakeEditorCommand.NotifyCanExecuteChanged();
            }
        };
    }

    private bool CanOpenClassificationEditor() =>
        IsTradeDetailVisible && SelectedTradeDetail is not null && !HasTradeDetailDraft &&
        !IsTradeDetailLoading && !IsTradeMistakesLoading && !IsTradeScreenshotsLoading &&
        !IsTradeFormVisible && !IsSaving && !IsUpdatingTrade && !IsDeletingTrade &&
        !IsCloseTradeVisible && !IsClosingTrade && !IsAddScreenshotVisible &&
        !IsAddingScreenshot && !IsDeletingScreenshot && !IsTradingSetupSaving &&
        !IsAssigningMistake && !IsRemovingMistake;

    private void SetClassificationEditor(ClassificationEditor editor)
    {
        if (_classificationEditor == editor) return;
        bool closedDescription = IsDescriptionEditorOpen;
        _classificationEditor = editor;
        if (closedDescription)
        {
            TradeDescriptionDraft = string.Empty;
            TradeDescriptionError = null;
        }
        OnPropertyChanged(nameof(IsDescriptionEditorOpen));
        SaveTradeDescriptionCommand.NotifyCanExecuteChanged();
        CancelDescriptionEditorCommand.NotifyCanExecuteChanged();
        RemoveMistakeCommand.NotifyCanExecuteChanged();
        DeleteScreenshotCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsSetupEditorOpen));
        OnPropertyChanged(nameof(IsMistakeEditorOpen));
        OnPropertyChanged(nameof(HasTradeClassificationDraft));
        OnPropertyChanged(nameof(HasTradeDetailDraft));
        ShowTradeDetailCommand.NotifyCanExecuteChanged();
        CloseTradeDetailCommand.NotifyCanExecuteChanged();
        ShowTradeEditCommand.NotifyCanExecuteChanged();
        ShowSelectedTradeEditCommand.NotifyCanExecuteChanged();
        ShowManualEntryCommand.NotifyCanExecuteChanged();
        ShowCloseTradeCommand.NotifyCanExecuteChanged();
        ShowAddScreenshotCommand.NotifyCanExecuteChanged();
        DeleteTradeCommand.NotifyCanExecuteChanged();
        DeleteSelectedTradeCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
    }
}