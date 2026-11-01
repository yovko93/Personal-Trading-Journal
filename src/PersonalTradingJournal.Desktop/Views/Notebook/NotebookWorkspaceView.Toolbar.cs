using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;

namespace PersonalTradingJournal.Desktop.Views.Notebook;

public partial class NotebookWorkspaceView
{
    private FlowDocument? _linkEditDocument;
    private TextPointer? _linkEditStart, _linkEditEnd;
    private Func<bool>? _linkEditOwns;
    private void ToggleTagAssignment(object sender, RoutedEventArgs e)
    {
        if (_model?.CanUseEditor != true) return;
        TagAssignment.IsExpanded = !TagAssignment.IsExpanded;
        if (TagAssignment.IsExpanded) _folderFocus.Request(() => TagPicker);
    }
    private void ToggleLinkActions(object sender, RoutedEventArgs e)
    {
        if (LinkActions.IsExpanded) CloseLinkActions(sender, e);
        else OpenLinkActions();
    }
    private void OpenLinkActions()
    {
        if (_model is not { CanUseEditor: true } model) return;
        _linkEditDocument = Preview.Document;
        _linkEditStart = Preview.Selection.Start; _linkEditEnd = Preview.Selection.End;
        var owns = model.CaptureNoteActionOwnership();
        _linkEditOwns = () => ReferenceEquals(_model, model) && owns() && ReferenceEquals(Preview.Document, _linkEditDocument);
        LinkUrl.Text = NotebookEditorAdapter.SelectedLink(Preview.Selection)?.NavigateUri?.AbsoluteUri ?? "";
        LinkActions.IsExpanded = true;
        _folderFocus.Request(() => _linkEditOwns?.Invoke() == true ? LinkUrl : null);
    }
    private bool RestoreLinkSelection()
    {
        if (_linkEditOwns is null) return true;
        if (!_linkEditOwns()) { ResetInlineActions(); return false; }
        Preview.Selection.Select(_linkEditStart!, _linkEditEnd!);
        return true;
    }
    private void CloseLinkActions(object sender, RoutedEventArgs e)
    {
        bool restore = LinkActions.IsKeyboardFocusWithin;
        LinkActions.IsExpanded = false; _linkEditOwns = null; _linkEditDocument = null;
        _linkEditStart = _linkEditEnd = null;
        if (restore) _folderFocus.Request(() => HyperlinkButton);
    }
    private void InlineActionsKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        if (ReferenceEquals(sender, LinkActions)) CloseLinkActions(sender, e);
        else { TagAssignment.IsExpanded = false; _folderFocus.Request(() => AddTagButton); }
    }
    private void ResetInlineActions()
    {
        LinkActions.IsExpanded = false; TagAssignment.IsExpanded = false;
        _linkEditDocument = null; _linkEditStart = _linkEditEnd = null; _linkEditOwns = null;
    }
}
