using System.Windows;
using System.Windows.Controls;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;

namespace PersonalTradingJournal.Desktop.Views.Notebook;

public sealed class NotebookLeaveInteraction : INotebookInteraction
{
    public NotebookLeaveChoice ConfirmLeave()
    {
        var choice = NotebookLeaveChoice.Cancel;
        var content = new StackPanel { Margin = new Thickness(20) };
        var dialog = new Window { Title = "Unsaved note", Width = 440, SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = content };
        dialog.SetResourceReference(FrameworkElement.StyleProperty, "PtjWindowStyle");
        content.Children.Add(new TextBlock { Text = "Save your note before continuing? Discard removes only unsaved edits.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) });
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right }; content.Children.Add(actions);
        foreach (var (label, value) in new[] { ("Keep editing", NotebookLeaveChoice.Cancel), ("Discard", NotebookLeaveChoice.Discard), ("Save", NotebookLeaveChoice.Save) })
        {
            var button = new Button { Content = label, Margin = new Thickness(4), IsCancel = value == NotebookLeaveChoice.Cancel, IsDefault = value == NotebookLeaveChoice.Cancel };
            button.SetResourceReference(FrameworkElement.StyleProperty, value == NotebookLeaveChoice.Save ? "PtjPrimaryButtonStyle" : "PtjSecondaryButtonStyle");
            button.Click += (_, _) => { choice = value; dialog.Close(); }; actions.Children.Add(button);
        }
        var owner = System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        if (owner?.IsVisible == true) dialog.Owner = owner;
        dialog.ShowDialog(); return choice;
    }
}
