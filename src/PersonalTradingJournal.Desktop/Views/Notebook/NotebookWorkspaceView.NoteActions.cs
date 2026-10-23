using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace PersonalTradingJournal.Desktop.Views.Notebook;

public partial class NotebookWorkspaceView
{
    private Func<bool>? _noteMenuOwns, _moveNoteOwns;
    private void NoteOverflowOpened(object sender, RoutedEventArgs e)
    {
        if (_model is not { } model || sender is not ContextMenu menu) return;
        menu.DataContext = model;
        var ownsModel = model.CaptureNoteActionOwnership();
        bool Owns() => ReferenceEquals(_model, model) && IsLoaded && ownsModel();
        _noteMenuOwns = Owns;
        foreach (var (item, command) in new (MenuItem, ICommand)[] {
            (DuplicateNoteButton, model.DuplicateCommand), (ArchiveNoteButton, model.ArchiveCommand),
            (UnarchiveNoteButton, model.UnarchiveCommand), (TrashNoteButton, model.TrashCommand),
            (RestoreNoteButton, model.RestoreCommand), (DeleteNoteButton, model.DeletePermanentlyCommand),
            (SaveNoteMenuItem, model.SaveCommand), (DiscardNoteMenuItem, model.DiscardCommand) })
            item.Command = new OwnedNoteCommand(command, Owns);
        MoveNoteMenuItem.Command = new RelayCommand(() =>
        {
            if (!Owns() || !model.CanEditDraft) return;
            _moveNoteOwns = Owns;
            model.MoveFolder = model.Folders.FirstOrDefault(f => f.Id == model.Note?.FolderId);
            ConfirmMoveNote.Command = new OwnedNoteCommand(model.MoveCommand, Owns, () => ConfirmNoteMove(this, new()));
            MoveNotePicker.Visibility = Visibility.Visible;
            _folderFocus.Request(() => Owns() ? MoveNoteFolder : null);
        }, () => Owns() && model.CanEditDraft);
    }
    private async void ConfirmNoteMove(object sender, RoutedEventArgs e)
    {
        if (_model is not { } model || _moveNoteOwns?.Invoke() != true || !model.MoveCommand.CanExecute(null)) return;
        var owns = _moveNoteOwns; var target = model.MoveFolder; var originalFolder = model.Note?.FolderId;
        await model.MoveCommand.ExecuteAsync(null);
        if (ReferenceEquals(owns, _moveNoteOwns) && owns() && originalFolder != target?.Id && model.Note?.FolderId == target?.Id)
            CloseNoteMove(false);
        // Failure leaves the picker and its message available; completion never takes focus.
    }
    private void DismissNoteMove(object sender, RoutedEventArgs e) => CloseNoteMove(true);
    private void MoveNoteKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { e.Handled = true; CloseNoteMove(true); }
    }
    private void CloseNoteMove(bool restoreFocus)
    {
        bool restore = restoreFocus && MoveNotePicker.IsKeyboardFocusWithin && _moveNoteOwns?.Invoke() == true;
        _moveNoteOwns = null; MoveNotePicker.Visibility = Visibility.Collapsed;
        if (restore) _folderFocus.Request(() => NoteOverflow);
    }
    private void RefreshNoteActionOwnership()
    {
        if (_noteMenuOwns?.Invoke() == false) { NoteOverflow.ContextMenu.IsOpen = false; _noteMenuOwns = null; }
        if (_moveNoteOwns?.Invoke() == false) CloseNoteMove(false);
    }
    private sealed class OwnedNoteCommand(ICommand command, Func<bool> owns, Action? execute = null) : ICommand
    {
        public bool CanExecute(object? parameter) => owns() && command.CanExecute(parameter);
        public void Execute(object? parameter) { if (CanExecute(parameter)) { if (execute is null) command.Execute(parameter); else execute(); } }
        public event EventHandler? CanExecuteChanged { add => command.CanExecuteChanged += value; remove => command.CanExecuteChanged -= value; }
    }
}
