using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Application.Notebook;

public static class NotebookDuplication
{
    public static string Title(string source)
    {
        source = NotebookLimits.NoteTitle(source);
        const string prefix = "Copy of ";
        int length = Math.Min(source.Length, NotebookLimits.Title - prefix.Length);
        if (length < source.Length && char.IsHighSurrogate(source[length - 1])) length--;
        return prefix + source[..length];
    }
    public static NotebookDocument Document(NotebookDocument source, IReadOnlyDictionary<Guid, Guid> images) =>
        new(source.Blocks.Select(n => Clone(n, images)));
    private static NotebookNode Clone(NotebookNode node, IReadOnlyDictionary<Guid, Guid> images)
    {
        var id = Guid.NewGuid(); var children = node.Children.Select(n => Clone(n, images));
        return node.Kind switch
        {
            NotebookNodeKind.Text => NotebookNode.Run(id, node.Text!, node.Marks),
            NotebookNodeKind.LineBreak => NotebookNode.Break(id),
            NotebookNodeKind.Paragraph => NotebookNode.Paragraph(id, children),
            NotebookNodeKind.Heading => NotebookNode.Heading(id, node.HeadingLevel!.Value, children),
            NotebookNodeKind.Hyperlink => NotebookNode.Hyperlink(id, node.Url!, children),
            NotebookNodeKind.ListItem or NotebookNodeKind.ChecklistItem => NotebookNode.Item(id, children, node.IsChecked),
            NotebookNodeKind.Image => NotebookNode.Image(id, images[node.ImageId!.Value], node.Caption, node.AlternativeText),
            _ => NotebookNode.List(id, node.Kind, children)
        };
    }
}
