namespace PersonalTradingJournal.Desktop.ViewModels.Notebook;

public sealed partial class NotebookWorkspaceViewModel
{
    private bool _tagCreationOpen, _tagRenameOpen;
    private long _tagFormGeneration;
    private NotebookTagOption? _tagFormTarget;
    public bool IsTagCreationOpen { get => _tagCreationOpen; private set => SetProperty(ref _tagCreationOpen, value); }
    public bool IsTagRenameOpen { get => _tagRenameOpen; private set => SetProperty(ref _tagRenameOpen, value); }
    public bool HasTagName => !string.IsNullOrWhiteSpace(TagName);
    public bool IsTagOperationPending => _tagOperation;
    public bool CanManageTags => CanChangeTags;
    public string TagRenameContext => _tagFormTarget is { } tag ? $"Rename global tag “{tag.Name}”" : "";

    public bool BeginTagCreation()
    {
        if (!CanManageTags) return false;
        CloseTagForm(); TagName = ""; TagMessage = ""; IsTagCreationOpen = true;
        return true;
    }
    public bool BeginTagRename(NotebookTagOption tag)
    {
        if (!CanManageTags || !Tags.Any(t => t.Id == tag.Id && t.Version == tag.Version)) return false;
        CloseTagForm(); _tagFormTarget = tag; ManagedTag = tag; TagName = tag.Name; TagMessage = "";
        OnPropertyChanged(nameof(TagRenameContext)); IsTagRenameOpen = true;
        return true;
    }
    private void CloseTagForm()
    {
        ++_tagFormGeneration; IsTagCreationOpen = false; IsTagRenameOpen = false; _tagFormTarget = null;
        OnPropertyChanged(nameof(TagRenameContext));
    }
    public void DismissTagForm()
    {
        bool wasOpen = IsTagCreationOpen || IsTagRenameOpen;
        CloseTagForm();
        if (wasOpen && _tagOperation) Message = "Tag input closed. A submitted change may already be saved; check TAGS or Refresh.";
    }
    public void CancelTagForm()
    {
        string error = TagMessage;
        DismissTagForm(); TagName = ""; TagMessage = "";
        if (!_tagOperation && error.Length != 0 && Message == error) Message = "";
    }
}
