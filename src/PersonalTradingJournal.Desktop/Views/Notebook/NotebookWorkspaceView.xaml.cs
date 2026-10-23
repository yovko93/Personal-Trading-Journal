using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Automation;
using System.Windows.Data;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Interactions;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;

namespace PersonalTradingJournal.Desktop.Views.Notebook;

public partial class NotebookWorkspaceView : UserControl
{
    private NotebookWorkspaceViewModel? _model;
    private readonly PageFocus _folderFocus;
    private long _headerInputVersion, _creationFocusVersion = -1;
    private IInputElement? _creationFocusOrigin, _creationFocusOwner;
    private bool _updating, _selectionUpdating, _scopeSelectionUpdating, _ownsFolderCreation;
    public NotebookWorkspaceView()
    {
        InitializeComponent();
        _folderFocus = new(this);
        PreviewKeyDown += (_, e) => { ++_headerInputVersion; if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control) CaptureCreationFocus(Keyboard.FocusedElement); };
        PreviewMouseDown += (_, _) => ++_headerInputVersion;
        PreviewMouseWheel += (_, _) => ++_headerInputVersion;
        InputBindings.Add(new KeyBinding(new CommunityToolkit.Mvvm.Input.RelayCommand(() => SearchBox.Focus()), Key.F, ModifierKeys.Control));
        DataObject.AddPastingHandler(Preview, OnPaste);
        DataObject.AddPastingHandler(TitleEditor, OnTitlePaste);
        Preview.CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo, (_, e) => { e.Handled = true; UndoEditor(false); },
            (_, e) => { e.Handled = true; e.CanExecute = _model?.CanEditBody == true && CanUndoEditor; }));
        Preview.CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo, (_, e) => { e.Handled = true; UndoEditor(true); },
            (_, e) => { e.Handled = true; e.CanExecute = _model?.CanEditBody == true && CanRedoEditor; }));
        Preview.CommandBindings.Add(new CommandBinding(ApplicationCommands.Cut, (_, e) => CutText(e),
            (_, e) => { e.CanExecute = _model?.CanEditBody == true && !Preview.Selection.IsEmpty; e.Handled = true; }));
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
    internal Action<string> WriteClipboardText { get; set; } = text => Clipboard.SetText(text, TextDataFormat.UnicodeText);
    private void CaptureCreationFocus(IInputElement? owner)
    { _creationFocusVersion = _headerInputVersion; _creationFocusOrigin = Keyboard.FocusedElement; _creationFocusOwner = owner; }
    private void RememberNoteCreationFocus(object sender, RoutedEventArgs e) => CaptureCreationFocus(NewNoteButton);
    private async void CreateTemplateFromMenu(object sender, RoutedEventArgs e)
    {
        if (_model is not { } model || sender is not MenuItem { DataContext: NotebookTemplateDefinition template } || !model.CreateTemplateCommand.CanExecute(null)) return;
        e.Handled = true;
        // Capture the clicked definition synchronously; the existing command captures its ID before awaiting.
        model.SelectedTemplate = template; CaptureCreationFocus(NewNoteMenuButton);
        await model.CreateTemplateCommand.ExecuteAsync(null);
    }
    private bool _ownsFolderRename;
    private void OpenFolderRename(object sender, RoutedEventArgs e)
    {
        if (_model?.BeginFolderRename() == true) { _ownsFolderRename = true; _folderFocus.Request(() => RenameFolderName); }
    }
    private async void FolderRenameKeyDown(object sender, KeyEventArgs e)
    {
        if (_model is not { } model) return;
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            bool restore = RenameFolderName.IsKeyboardFocusWithin;
            model.DismissFolderRename(); _ownsFolderRename = false; _folderFocus.Invalidate();
            if (restore) _folderFocus.Request(() => model.HasFolderActions && (model.Panel == 1 || ActualWidth >= 1000) ? RenameFolderButton : null);
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (model.RenameFolderCommand.CanExecute(null)) await model.RenameFolderCommand.ExecuteAsync(null);
            // Never request focus after asynchronous completion.
        }
    }
    private void OpenFolderCreation(object sender, RoutedEventArgs e)
    {
        if (_model?.BeginFolderCreation() == true) { _ownsFolderCreation = true; _folderFocus.Request(() => NewFolderName); }
    }
    private async void FolderCreationKeyDown(object sender, KeyEventArgs e)
    {
        if (_model is not { } model) return;
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            bool restoreFocus = NewFolderName.IsKeyboardFocusWithin;
            model.DismissFolderCreation();
            _ownsFolderCreation = false;
            _folderFocus.Invalidate();
            if (restoreFocus) _folderFocus.Request(() => model.Panel == 0 || ActualWidth >= 1000 ? NewFolderButton : null);
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (model.CreateFolderCommand.CanExecute(null)) await model.CreateFolderCommand.ExecuteAsync(null);
            // Completion never requests focus: newer user input retains ownership.
        }
    }
    private void FilterWorkspaceScopes(object sender, FilterEventArgs e) =>
        e.Accepted = e.Item is NotebookScopeOption { Scope: NotebookScope.AllNotes or NotebookScope.Pinned };
    private void FilterFolderScopes(object sender, FilterEventArgs e) =>
        e.Accepted = e.Item is NotebookScopeOption { Scope: NotebookScope.Folder };
    private void FilterOtherScopes(object sender, FilterEventArgs e) =>
        e.Accepted = e.Item is NotebookScopeOption { Scope: NotebookScope.Unfiled or NotebookScope.Archived or NotebookScope.Trash };
    private async void SelectPrimaryScope(object sender, SelectionChangedEventArgs e)
    {
        if (_scopeSelectionUpdating || e.AddedItems.Count == 0 || e.AddedItems[0] is not NotebookScopeOption scope ||
            DataContext is not NotebookWorkspaceViewModel model || ReferenceEquals(model.Scope, scope)) return;
        // Removed selections during catalog publication must never clear the canonical scope.
        model.Scope = scope;
        await model.LoadTask;
        // Restore the accepted selection if the existing unsaved-draft guard declined navigation.
        SynchronizeScopeSelection();
    }
    private void SynchronizeScopeSelection()
    {
        var scope = (DataContext as NotebookWorkspaceViewModel)?.Scope;
        _scopeSelectionUpdating = true;
        try
        {
            // A scope outside a ListBox's Items is not a valid SelectedItem. Explicitly clear
            // the other list, including when a dirty-draft guard rejects its proposed choice.
            ScopeList.SelectedItem = scope?.Scope is NotebookScope.AllNotes or NotebookScope.Pinned ? scope : null;
            FolderScopeList.SelectedItem = scope?.Scope == NotebookScope.Folder ? scope : null;
        }
        finally { _scopeSelectionUpdating = false; }
    }
    private void SelectOverflowScope(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: NotebookScopeOption scope } && DataContext is NotebookWorkspaceViewModel model)
            model.Scope = scope;
    }
    private void NotebookOverflowOpened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu)
            menu.Items.OfType<MenuItem>().Single().Command = (menu.PlacementTarget as FrameworkElement)?.DataContext is NotebookWorkspaceViewModel model ? model.RefreshCommand : null;
    }
    internal Func<IDataObject?> ReadClipboardData { get; set; } = Clipboard.GetDataObject;
    private void CutText(ExecutedRoutedEventArgs e)
    {
        e.Handled = true;
        if (_model?.CanEditBody != true || Preview.Selection.IsEmpty) return;
        try
        {
            // Cut exports readable text only. Reference removal uses the same validated native undo unit.
            WriteClipboardText(Preview.Selection.Text);
            NotebookEditorAdapter.PasteText(Preview, "", _model.ValidateDocument);
        }
        catch (Exception ex) when (ex is ArgumentException or System.Runtime.InteropServices.ExternalException)
        { EditorFeedback.Text = "Cut could not finish. The selected content is retained."; }
    }
    private void OnLoaded(object sender, RoutedEventArgs e) => Attach();
    private void OnUnloaded(object sender, RoutedEventArgs e) { ++_headerInputVersion; _folderFocus.Invalidate(); if (_ownsFolderRename) _model?.DismissFolderRename(); _ownsFolderRename = false; if (_ownsFolderCreation) _model?.DismissFolderCreation(); _ownsFolderCreation = false; _imageLoads?.Cancel(); Detach(); _model = null; }
    private void Detach() { NoteOverflow.ContextMenu.IsOpen = false; CloseNoteMove(false); if (_model is not null) { _model.PropertyChanged -= Changed; _model.NoteCreated -= Created; } }
    private void Created(object? sender, EventArgs e)
    {
        LayoutPanels();
        if (_creationFocusVersion != _headerInputVersion || Keyboard.FocusedElement is { } focused &&
            !ReferenceEquals(focused, _creationFocusOrigin) && !ReferenceEquals(focused, _creationFocusOwner)) return;
        _creationFocusVersion = -1;
        _folderFocus.Request(() => { TitleEditor.SelectAll(); return TitleEditor; });
    }
    private void OnContextChanged(object sender, DependencyPropertyChangedEventArgs e) { ++_headerInputVersion; _folderFocus?.Invalidate(); if (_ownsFolderRename && e.OldValue is NotebookWorkspaceViewModel previous) previous.DismissFolderRename(); _ownsFolderRename = false; if (_ownsFolderCreation && e.OldValue is NotebookWorkspaceViewModel old) old.DismissFolderCreation(); _ownsFolderCreation = false; if (IsLoaded) Attach(); }
    private void Attach()
    {
        Detach();
        _model = DataContext as NotebookWorkspaceViewModel;
        if (_model is not null) { _model.PropertyChanged += Changed; _model.NoteCreated += Created; }
        SynchronizeScopeSelection(); UpdatePreview(); LayoutPanels();
    }
    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        RefreshNoteActionOwnership();
        if (e.PropertyName == nameof(NotebookWorkspaceViewModel.Scope)) SynchronizeScopeSelection();
        if (e.PropertyName == nameof(NotebookWorkspaceViewModel.EditorRevision)) UpdatePreview();
        if (e.PropertyName == nameof(NotebookWorkspaceViewModel.Panel)) LayoutPanels();
    }
    private void UpdatePreview()
    {
        _updating = true;
        try
        {
            _imageLoads?.Cancel(); _imageLoads = new();
            Preview.Document = NotebookEditorAdapter.Create(_model?.DraftDocument ?? NotebookDocument.Empty, CreateImageBlock);
            EditorFeedback.Text = "";
            AutomationProperties.SetName(Preview, "Note content");
            if (_model?.HasDraft != true) TitleEditor.SetCurrentValue(TextBox.TextProperty, _model?.NoteTitle ?? "Select a note");
        }
        finally { _updating = false; }
        ResetHistory(); UpdateUndo();
        SelectionChanged(Preview, new RoutedEventArgs());
    }
    private void BodyChanged(object sender, TextChangedEventArgs e)
    {
        if (_updating || _model?.CanEditBody != true) return;
        try { var document = NotebookEditorAdapter.Read(Preview.Document); _model.ValidateDocument(document); _model.SetDraftDocument(document); _invalidEditor = false; RecordHistory(document); }
        catch (ArgumentException) { _invalidEditor = true; _model.SetDraftError("Content exceeds its limits or has an unsupported structure. Undo or correct the change before saving."); }
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
        RememberSelection();
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
    private void UpdateUndo() { if (UndoButton is not null) { UndoButton.IsEnabled = CanUndoEditor; RedoButton.IsEnabled = CanRedoEditor; } }
    private void ToggleBold(object sender, RoutedEventArgs e) => Edit(() => Preview.Selection.ApplyPropertyValue(TextElement.FontWeightProperty,
        Equals(Preview.Selection.GetPropertyValue(TextElement.FontWeightProperty), FontWeights.Bold) ? FontWeights.Normal : FontWeights.Bold));
    private void ToggleItalic(object sender, RoutedEventArgs e) => Edit(() => Preview.Selection.ApplyPropertyValue(TextElement.FontStyleProperty,
        Equals(Preview.Selection.GetPropertyValue(TextElement.FontStyleProperty), FontStyles.Italic) ? FontStyles.Normal : FontStyles.Italic));
    private void ToggleUnderline(object sender, RoutedEventArgs e) => Edit(() => Preview.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty,
        Preview.Selection.GetPropertyValue(Inline.TextDecorationsProperty) is TextDecorationCollection values && values.Any(d => d.Location == TextDecorationLocation.Underline) ? new TextDecorationCollection() : TextDecorations.Underline));
    private void ToggleBullets(object sender, RoutedEventArgs e) => Edit(() => EditingCommands.ToggleBullets.Execute(null, Preview));
    private void ToggleNumbering(object sender, RoutedEventArgs e) => Edit(() => EditingCommands.ToggleNumbering.Execute(null, Preview));
    private void Undo(object sender, RoutedEventArgs e) => UndoEditor(false);
    private void Redo(object sender, RoutedEventArgs e) => UndoEditor(true);
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
            var data = ReadClipboardData();
            if (data is null) { EditorFeedback.Text = "No text was available to paste."; return; }
            var format = data.GetDataPresent(DataFormats.UnicodeText, false) ? DataFormats.UnicodeText
                : data.GetDataPresent("PNG", false) ? "PNG"
                : data.GetDataPresent(DataFormats.Bitmap, false) ? DataFormats.Bitmap
                : data.GetFormats(false).FirstOrDefault() ?? throw new ArgumentException("No supported clipboard format.");
            var request = new DataObjectPastingEventArgs(data, false, format);
            if (title) OnTitlePaste(TitleEditor, request); else OnPaste(Preview, request);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.ExternalException or InvalidOperationException or ArgumentException)
        { EditorFeedback.Text = "The clipboard could not be read. Your draft is unchanged; try Paste again."; }
    }
    private async void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        e.CancelCommand();
        if (_model?.CanEditBody != true) return;
        var owner = _model;
        try
        {
            if (ImageInput.ClipboardImage(e.DataObject) is { } bytes)
            { await InsertBytesAsync(bytes); return; }
            if (e.DataObject.GetData(DataFormats.UnicodeText, false) is not string text) throw new ArgumentException();
            NotebookEditorAdapter.PasteText(Preview, text, _model.ValidateDocument);
            EditorFeedback.Text = "Pasted as readable text; external formatting was not imported.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is ArgumentException or System.Runtime.InteropServices.ExternalException or IOException or InvalidDataException or InvalidOperationException or NotSupportedException or FormatException)
        { if (_model == owner) EditorFeedback.Text = "Paste rejected. Use supported image content or Unicode text within the document limits; the draft was not changed."; }
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
