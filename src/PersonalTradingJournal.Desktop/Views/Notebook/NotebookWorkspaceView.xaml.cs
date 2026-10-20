using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Automation;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;

namespace PersonalTradingJournal.Desktop.Views.Notebook;

public partial class NotebookWorkspaceView : UserControl
{
    private NotebookWorkspaceViewModel? _model;
    private bool _updating, _selectionUpdating;
    public NotebookWorkspaceView()
    {
        InitializeComponent();
        InputBindings.Add(new KeyBinding(new CommunityToolkit.Mvvm.Input.RelayCommand(() => SearchBox.Focus()), Key.F, ModifierKeys.Control));
        DataObject.AddPastingHandler(Preview, OnPaste);
        DataObject.AddPastingHandler(TitleEditor, OnTitlePaste);
        // Do not inspect the clipboard during CanExecute, activation or selection.
        Preview.CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, (_, e) => PasteFromClipboard(false, e),
            (_, e) => { e.CanExecute = _model?.CanEditBody == true; e.Handled = true; }));
        TitleEditor.CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, (_, e) => PasteFromClipboard(true, e),
            (_, e) => { e.CanExecute = _model?.CanEditDraft == true; e.Handled = true; }));
        Preview.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (_model?.CanEditBody != true) return;
            var position = Preview.GetPositionFromPoint(e.GetPosition(Preview), true);
            if (position is null) return;
            var item = NotebookEditorAdapter.Ancestor<ListItem>(position);
            if (item?.Parent is System.Windows.Documents.List { MarkerStyle: TextMarkerStyle.None } && position.Paragraph == item.Blocks.FirstBlock)
            { e.Handled = true; Preview.CaretPosition = position; Edit(() => NotebookEditorAdapter.ToggleCheck(Preview)); }
        };
    }
    private void OnLoaded(object sender, RoutedEventArgs e) => Attach();
    private void OnUnloaded(object sender, RoutedEventArgs e) { Detach(); _model = null; }
    private void Detach() { if (_model is not null) { _model.PropertyChanged -= Changed; _model.NoteCreated -= Created; } }
    private void Created(object? sender, EventArgs e) { LayoutPanels(); TitleEditor.Focus(); TitleEditor.SelectAll(); }
    private void OnContextChanged(object sender, DependencyPropertyChangedEventArgs e) { if (IsLoaded) Attach(); }
    private void Attach()
    {
        Detach();
        _model = DataContext as NotebookWorkspaceViewModel;
        if (_model is not null) { _model.PropertyChanged += Changed; _model.NoteCreated += Created; }
        UpdatePreview(); LayoutPanels();
    }
    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NotebookWorkspaceViewModel.EditorRevision)) UpdatePreview();
        if (e.PropertyName == nameof(NotebookWorkspaceViewModel.Panel)) LayoutPanels();
    }
    private void UpdatePreview()
    {
        _updating = true;
        try
        {
            bool protectedBody = _model?.DraftDocument.ImageNodes.Count > 0;
            Preview.Document = protectedBody ? NotebookDocumentPreview.Create(_model?.DraftDocument) : NotebookEditorAdapter.Create(_model?.DraftDocument ?? NotebookDocument.Empty);
            EditorFeedback.Text = protectedBody ? "This body contains protected image references and is read-only. The title can still be edited." : "";
            AutomationProperties.SetName(Preview, protectedBody ? "Read-only note content" : "Note content");
            if (_model?.HasDraft != true) TitleEditor.SetCurrentValue(TextBox.TextProperty, _model?.NoteTitle ?? "Select a note");
        }
        finally { _updating = false; }
        UpdateUndo();
        SelectionChanged(Preview, new RoutedEventArgs());
    }
    private void BodyChanged(object sender, TextChangedEventArgs e)
    {
        if (_updating || _model?.CanEditBody != true) return;
        try { var document = NotebookEditorAdapter.Read(Preview.Document); _model.ValidateDocument(document); _model.SetDraftDocument(document); }
        catch (ArgumentException) { _model.SetDraftError("Content exceeds its limits or has an unsupported structure. Undo or correct the change before saving."); }
        UpdateUndo();
    }
    private void Edit(Action action)
    {
        if (_model?.CanEditBody != true) return;
        try { Preview.Focus(); action(); BodyChanged(Preview, null!); EditorFeedback.Text = ""; SelectionChanged(Preview, new RoutedEventArgs()); }
        catch (ArgumentException ex) { EditorFeedback.Text = ex.Message; }
    }
    private void StyleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_selectionUpdating && ParagraphStyle.SelectedIndex >= 0) Edit(() => NotebookEditorAdapter.Heading(Preview, ParagraphStyle.SelectedIndex));
    }
    private void SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (Preview is null || BoldButton is null || _selectionUpdating) return;
        _selectionUpdating = true;
        try
        {
            double line = Preview.CaretPosition.Paragraph?.LineHeight ?? 23;
            ParagraphStyle.SelectedIndex = line == 32 ? 1 : line == 28 ? 2 : line == 25 ? 3 : 0;
            foreach (var (button, property, enabled) in new[] {
                (BoldButton, TextElement.FontWeightProperty, (object)FontWeights.Bold),
                (ItalicButton, TextElement.FontStyleProperty, (object)FontStyles.Italic),
                (UnderlineButton, Inline.TextDecorationsProperty, (object)TextDecorations.Underline) })
            {
                var state = Preview.Selection.GetPropertyValue(property);
                bool on = state is TextDecorationCollection decorations
                    ? decorations.Any(d => d.Location == TextDecorationLocation.Underline) : Equals(state, enabled);
                string label = state == DependencyProperty.UnsetValue ? "Mixed" : on ? "On" : "Off";
                button.ToolTip = label; AutomationProperties.SetItemStatus(button, label);
                button.SetResourceReference(Control.BackgroundProperty, label == "On" ? "PtjSurfaceElevatedBrush" : "PtjSurfaceBrush");
            }
            var link = NotebookEditorAdapter.SelectedLink(Preview.Selection);
            if (!LinkUrl.IsKeyboardFocusWithin) LinkUrl.Text = link?.NavigateUri?.AbsoluteUri ?? "";
        }
        finally { _selectionUpdating = false; }
    }
    private void AddChecklist(object sender, RoutedEventArgs e) => Edit(() => NotebookEditorAdapter.Checklist(Preview));
    private void UpdateUndo() { if (UndoButton is not null) { UndoButton.IsEnabled = Preview.CanUndo; RedoButton.IsEnabled = Preview.CanRedo; } }
    private void ToggleBold(object sender, RoutedEventArgs e) => Edit(() => Preview.Selection.ApplyPropertyValue(TextElement.FontWeightProperty,
        Equals(Preview.Selection.GetPropertyValue(TextElement.FontWeightProperty), FontWeights.Bold) ? FontWeights.Normal : FontWeights.Bold));
    private void ToggleItalic(object sender, RoutedEventArgs e) => Edit(() => Preview.Selection.ApplyPropertyValue(TextElement.FontStyleProperty,
        Equals(Preview.Selection.GetPropertyValue(TextElement.FontStyleProperty), FontStyles.Italic) ? FontStyles.Normal : FontStyles.Italic));
    private void ToggleUnderline(object sender, RoutedEventArgs e) => Edit(() => Preview.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty,
        Preview.Selection.GetPropertyValue(Inline.TextDecorationsProperty) is TextDecorationCollection values && values.Any(d => d.Location == TextDecorationLocation.Underline) ? new TextDecorationCollection() : TextDecorations.Underline));
    private void ToggleBullets(object sender, RoutedEventArgs e) => Edit(() => EditingCommands.ToggleBullets.Execute(null, Preview));
    private void ToggleNumbering(object sender, RoutedEventArgs e) => Edit(() => EditingCommands.ToggleNumbering.Execute(null, Preview));
    private void Undo(object sender, RoutedEventArgs e) => Edit(() => Preview.Undo());
    private void Redo(object sender, RoutedEventArgs e) => Edit(() => Preview.Redo());
    private void ToggleCheck(object sender, RoutedEventArgs e) => Edit(() => NotebookEditorAdapter.ToggleCheck(Preview));
    private void ApplyLink(object sender, RoutedEventArgs e) => Edit(() => NotebookEditorAdapter.SetLink(Preview, LinkUrl.Text.Trim()));
    private void RemoveLink(object sender, RoutedEventArgs e) => Edit(() => NotebookEditorAdapter.SetLink(Preview, null));
    private void OpenLink(object sender, RoutedEventArgs e)
    {
        var link = NotebookEditorAdapter.SelectedLink(Preview.Selection);
        if (link?.NavigateUri is not { } uri) { EditorFeedback.Text = "Place the caret inside a link."; return; }
        try
        {
            _ = NotebookNode.Hyperlink(Guid.NewGuid(), uri.AbsoluteUri, []);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is ArgumentException or System.ComponentModel.Win32Exception or InvalidOperationException)
        { EditorFeedback.Text = "The link could not be opened."; }
    }
    private void EditorKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.Control)
        { e.Handled = true; LinkActions.IsExpanded = true; LinkUrl.Focus(); }
        else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        { e.Handled = true; ToggleCheck(sender, e); }
        else if (_model?.CanEditBody == true && e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            e.Handled = NotebookEditorAdapter.AddChecklistItem(Preview);
    }
    private void OnTitlePaste(object sender, DataObjectPastingEventArgs e)
    {
        e.CancelCommand();
        if (_model?.CanEditDraft != true) return;
        try
        {
            if (e.DataObject.GetData(DataFormats.UnicodeText, false) is not string text) throw new ArgumentException();
            var proposed = TitleEditor.Text.Remove(TitleEditor.SelectionStart, TitleEditor.SelectionLength).Insert(TitleEditor.SelectionStart, text);
            NotebookLimits.Text(proposed, NotebookLimits.Title, nameof(text));
            TitleEditor.SelectedText = text;
        }
        catch (Exception ex) when (ex is ArgumentException or System.Runtime.InteropServices.ExternalException)
        { EditorFeedback.Text = "Paste rejected. Use text within the 200-character title limit."; }
    }
    private void PasteFromClipboard(bool title, ExecutedRoutedEventArgs e)
    {
        e.Handled = true;
        try
        {
            var data = Clipboard.GetDataObject();
            if (data is null) { EditorFeedback.Text = "No text was available to paste."; return; }
            var request = new DataObjectPastingEventArgs(data, false, DataFormats.UnicodeText);
            if (title) OnTitlePaste(TitleEditor, request); else OnPaste(Preview, request);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.ExternalException or InvalidOperationException or ArgumentException)
        { EditorFeedback.Text = "The clipboard could not be read. Your draft is unchanged; try Paste again."; }
    }
    private void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        e.CancelCommand();
        if (_model?.CanEditBody != true) return;
        try
        {
            if (e.DataObject.GetData(DataFormats.UnicodeText, false) is not string text) throw new ArgumentException();
            NotebookEditorAdapter.PasteText(Preview, text, _model.ValidateDocument);
            EditorFeedback.Text = "Pasted as readable text; external formatting and images were not imported.";
        }
        catch (Exception ex) when (ex is ArgumentException or System.Runtime.InteropServices.ExternalException)
        { EditorFeedback.Text = "Paste rejected. Use Unicode text within the document limits; the draft was not changed."; }
    }
    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => LayoutPanels();
    private void LayoutPanels()
    {
        if (OrganizationPanel is null) return;
        bool narrow = ActualWidth < 1000;
        NarrowNavigation.Visibility = narrow ? Visibility.Visible : Visibility.Collapsed;
        int selected = _model?.Panel ?? 1;
        var panels = new[] { OrganizationPanel, NotesPanel, ContentPanel };
        for (int i = 0; i < panels.Length; i++)
        {
            panels[i].Visibility = !narrow || selected == i ? Visibility.Visible : Visibility.Collapsed;
            Grid.SetColumn(panels[i], narrow ? 0 : i); Grid.SetColumnSpan(panels[i], narrow ? 3 : 1);
        }
        OrganizationColumn.Width = narrow ? new(0) : new(210);
        NotesColumn.Width = narrow ? new(0) : new(290);
        ContentColumn.Width = new(1, GridUnitType.Star);
    }
    private void ShowOrganization(object sender, RoutedEventArgs e) { if (_model is not null) _model.Panel = 0; ScopeList.Focus(); }
    private void ShowNotes(object sender, RoutedEventArgs e) { if (_model is not null) _model.Panel = 1; NoteList.Focus(); }
    private void ShowContent(object sender, RoutedEventArgs e) { if (_model is not null) _model.Panel = 2; Preview.Focus(); }
}
