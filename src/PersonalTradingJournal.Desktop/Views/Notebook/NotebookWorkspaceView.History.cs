using System.Text.Json;
using System.Windows.Documents;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Desktop.Views.Notebook;

public partial class NotebookWorkspaceView
{
    private sealed record EditorState(NotebookDocument Document, string Signature, int Start, int End);
    private readonly List<EditorState> _editorHistory = [];
    private int _historyIndex;
    private bool _referenceHistory, _invalidEditor;
    private int _nativeHistoryDirection;
    private void EnableReferenceHistory()
    { _referenceHistory = true; Preview.IsUndoEnabled = false; }
    private static object HistoryValue(NotebookNode n) => new { n.Kind, n.Text, n.Marks, n.HeadingLevel, n.IsChecked,
        n.Url, n.ImageId, n.Caption, n.AlternativeText, Children = n.Children.Select(HistoryValue).ToArray() };
    private void ResetHistory()
    {
        _editorHistory.Clear(); _historyIndex = -1; _referenceHistory = false; _invalidEditor = false;
        Preview.IsUndoEnabled = true;
        RecordHistory(_model?.DraftDocument ?? NotebookDocument.Empty);
    }
    private void RecordHistory(NotebookDocument document)
    {
        if (document.ImageNodes.Count > 0) EnableReferenceHistory();
        // WPF may drop BlockUIContainer visual children and Run identities during native undo.
        // From the first reference onward, this note's bounded semantic history owns all undo units.
        if (_referenceHistory) Preview.IsUndoEnabled = false;
        string key = JsonSerializer.Serialize(document.Blocks.Select(HistoryValue));
        if (_nativeHistoryDirection != 0)
        {
            int match = _nativeHistoryDirection < 0
                ? _editorHistory.FindLastIndex(Math.Max(0, _historyIndex - 1), state => state.Signature == key)
                : _editorHistory.FindIndex(Math.Min(_editorHistory.Count, _historyIndex + 1), state => state.Signature == key);
            if (match >= 0) { _historyIndex = match; return; }
        }
        if (_historyIndex >= 0 && _editorHistory[_historyIndex].Signature == key) return;
        if (_historyIndex + 1 < _editorHistory.Count) _editorHistory.RemoveRange(_historyIndex + 1, _editorHistory.Count - _historyIndex - 1);
        var content = Preview.Document.ContentStart;
        _editorHistory.Add(new(document, key, content.GetOffsetToPosition(Preview.Selection.Start), content.GetOffsetToPosition(Preview.Selection.End)));
        if (_editorHistory.Count > 101) _editorHistory.RemoveAt(0);
        _historyIndex = _editorHistory.Count - 1;
    }
    private void RememberSelection()
    {
        if (_updating || _historyIndex < 0 || _historyIndex >= _editorHistory.Count) return;
        _editorHistory[_historyIndex] = _editorHistory[_historyIndex] with {
            Start = Preview.Document.ContentStart.GetOffsetToPosition(Preview.Selection.Start),
            End = Preview.Document.ContentStart.GetOffsetToPosition(Preview.Selection.End) };
    }
    private bool CanUndoEditor => _referenceHistory ? _invalidEditor || _historyIndex > 0 : Preview.CanUndo;
    private bool CanRedoEditor => _referenceHistory ? !_invalidEditor && _historyIndex + 1 < _editorHistory.Count : Preview.CanRedo;
    private void UndoEditor(bool redo)
    {
        if (_model?.CanUseEditor != true || !(redo ? CanRedoEditor : CanUndoEditor)) return;
        if (!_referenceHistory)
        {
            _nativeHistoryDirection = redo ? 1 : -1;
            try { Edit(() => { if (redo) Preview.Redo(); else Preview.Undo(); }); }
            finally { _nativeHistoryDirection = 0; }
            return;
        }
        var state = _editorHistory[_historyIndex += _invalidEditor ? 0 : redo ? 1 : -1];
        _invalidEditor = false;
        _updating = true;
        try
        {
            Preview.Document = NotebookEditorAdapter.Create(state.Document, CreateImageBlock);
            var start = Preview.Document.ContentStart.GetPositionAtOffset(state.Start) ?? Preview.Document.ContentEnd;
            var end = Preview.Document.ContentStart.GetPositionAtOffset(state.End) ?? Preview.Document.ContentEnd;
            Preview.Selection.Select(start, end);
        }
        finally { _updating = false; }
        _model.SetDraftDocument(state.Document); UpdateUndo();
    }
}
