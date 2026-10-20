using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;

namespace PersonalTradingJournal.Desktop.Views.Notebook;

public partial class NotebookWorkspaceView : UserControl
{
    private NotebookWorkspaceViewModel? _model;
    public NotebookWorkspaceView()
    {
        InitializeComponent();
        InputBindings.Add(new KeyBinding(new CommunityToolkit.Mvvm.Input.RelayCommand(() => SearchBox.Focus()), Key.F, ModifierKeys.Control));
    }
    private void OnLoaded(object sender, RoutedEventArgs e) => Attach();
    private void OnUnloaded(object sender, RoutedEventArgs e) { if (_model is not null) _model.PropertyChanged -= Changed; _model = null; }
    private void OnContextChanged(object sender, DependencyPropertyChangedEventArgs e) { if (IsLoaded) Attach(); }
    private void Attach()
    {
        if (_model is not null) _model.PropertyChanged -= Changed;
        _model = DataContext as NotebookWorkspaceViewModel;
        if (_model is not null) _model.PropertyChanged += Changed;
        UpdatePreview(); LayoutPanels();
    }
    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NotebookWorkspaceViewModel.Note)) UpdatePreview();
        if (e.PropertyName == nameof(NotebookWorkspaceViewModel.Panel)) LayoutPanels();
    }
    private void UpdatePreview() => Preview.Document = NotebookDocumentPreview.Create(_model?.Note?.Document);
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
