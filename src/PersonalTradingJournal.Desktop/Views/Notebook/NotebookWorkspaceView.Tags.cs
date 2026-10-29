using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;

namespace PersonalTradingJournal.Desktop.Views.Notebook;

public partial class NotebookWorkspaceView
{
    private bool _ownsTagForm;
    private ContextMenu? _tagMenu;
    private Action? _releaseTagMenu;
    private Func<bool>? _tagMenuOwns;
    private Button? _tagFormOrigin;
    internal Task TagMenuTask { get; private set; } = Task.CompletedTask;
    private void OpenTagCreation(object sender, RoutedEventArgs e)
    {
        if (_model?.BeginTagCreation() != true) return;
        _ownsTagForm = true; _tagFormOrigin = NewTagButton;
        _folderFocus.Request(() => NewTagName);
    }
    private void CancelTagForm(object sender, RoutedEventArgs e)
    {
        if (_model is not { } model) return;
        bool restore = TagCreation.IsKeyboardFocusWithin || TagRename.IsKeyboardFocusWithin;
        model.CancelTagForm(); _ownsTagForm = false; _folderFocus.Invalidate();
        if (restore) _folderFocus.Request(() => _tagFormOrigin is { IsVisible: true } origin ? origin : NewTagButton);
    }
    private async void TagFormKeyDown(object sender, KeyEventArgs e)
    {
        if (_model is not { } model) return;
        if (e.Key == Key.Escape) { e.Handled = true; CancelTagForm(sender, e); }
        else if (e.Key == Key.Enter && (NewTagName.IsKeyboardFocusWithin || RenameTagName.IsKeyboardFocusWithin))
        {
            e.Handled = true;
            var command = model.IsTagCreationOpen ? model.CreateTagCommand : model.RenameTagCommand;
            if (command.CanExecute(null)) await command.ExecuteAsync(null);
            // Completion never requests focus; a newer interaction owns it.
        }
    }
    private void TagMenuKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Apps || e.Key == Key.F10 && Keyboard.Modifiers == ModifierKeys.Shift)
        { e.Handled = true; _dismissedOverflowTrigger = null; OpenTagMenu(sender, e); }
    }
    private void OpenTagMenu(object sender, RoutedEventArgs e)
    {
        if (_model is not { CanManageTags: true } model || sender is not Button { DataContext: NotebookTagOption tag } trigger) return;
        e.Handled = true;
        bool dismissedByThisPress = _dismissedOverflowTrigger == trigger;
        _dismissedOverflowTrigger = null;
        if (_tagMenu?.PlacementTarget == trigger && (dismissedByThisPress || _tagMenu.IsOpen))
        { _tagMenu.IsOpen = false; return; }
        ReleaseTagMenu();
        CloseOverflowMenus();
        // Capture both identity/version and workspace; popup DataContext is not a live selected-tag binding.
        var menu = new ContextMenu { PlacementTarget = trigger, Placement = PlacementMode.Bottom };
        menu.SetResourceReference(StyleProperty, "PtjEntityActionMenuStyle");
        var rename = new MenuItem { Header = "Rename", ToolTip = $"Rename global tag: {tag.Name}" };
        var delete = new MenuItem { Header = "Delete globally…", ToolTip = "Remove this tag from every note; keep notes and content." };
        rename.SetResourceReference(StyleProperty, "PtjLifecycleMenuItemStyle");
        delete.SetResourceReference(StyleProperty, "PtjLifecycleMenuItemStyle");
        AutomationProperties.SetName(rename, $"Rename global tag: {tag.Name}");
        AutomationProperties.SetName(delete, $"Delete global tag: {tag.Name}");
        bool used = false;
        bool Owns() => !used && ReferenceEquals(_tagMenu, menu) && ReferenceEquals(_model, model) &&
            model.CanManageTags && model.Tags.Any(t => ReferenceEquals(t, tag)) && ReferenceEquals(trigger.DataContext, tag);
        RoutedEventHandler renameClick = (_, args) =>
        {
            args.Handled = true;
            if (!Owns()) return;
            used = true; menu.IsOpen = false;
            if (!model.BeginTagRename(tag)) return;
            _ownsTagForm = true; _tagFormOrigin = trigger;
            _folderFocus.Request(() => RenameTagName);
        };
        RoutedEventHandler deleteClick = async (_, args) =>
        {
            args.Handled = true;
            if (!Owns()) return;
            used = true; menu.IsOpen = false;
            await (TagMenuTask = model.DeleteGlobalTagAsync(tag));
        };
        rename.Click += renameClick; delete.Click += deleteClick;
        menu.Items.Add(rename); menu.Items.Add(delete);
        trigger.ContextMenu = menu; _tagMenu = menu; _tagMenuOwns = Owns;
        var releaseTracking = TrackOverflowDismissal(trigger, menu);
        _releaseTagMenu = () =>
        {
            releaseTracking(); rename.Click -= renameClick; delete.Click -= deleteClick;
            if (ReferenceEquals(trigger.ContextMenu, menu)) trigger.ContextMenu = null;
        };
        menu.IsOpen = true;
    }
    private void RefreshTagMenuOwnership()
    {
        if (_tagMenu is not null && _tagMenuOwns?.Invoke() != true) ReleaseTagMenu();
    }
    private void ReleaseTagMenu()
    {
        var menu = _tagMenu; _tagMenu = null; _tagMenuOwns = null;
        if (menu is not null) menu.IsOpen = false;
        _releaseTagMenu?.Invoke(); _releaseTagMenu = null;
    }
    private void ReleaseTagControls(NotebookWorkspaceViewModel? model)
    {
        ReleaseTagMenu();
        if (_ownsTagForm) model?.DismissTagForm();
        _ownsTagForm = false; _tagFormOrigin = null;
    }
}
