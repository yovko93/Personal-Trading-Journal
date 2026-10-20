using System.Windows;
using System.Windows.Documents;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Desktop.Views.Notebook;

/// <summary>Explicit one-way projection of validated domain nodes; never loads serialized WPF or fetches a URL/file.</summary>
internal static class NotebookDocumentPreview
{
    internal static FlowDocument Create(NotebookDocument? source)
    {
        var document = new FlowDocument { PagePadding = new Thickness(0), FontSize = 15, LineHeight = 23 };
        document.SetResourceReference(TextElement.ForegroundProperty, "PtjTextPrimaryBrush");
        if (source is not null)
            foreach (var node in source.Blocks) document.Blocks.Add(Block(node));
        return document;
    }
    private static Block Block(NotebookNode node)
    {
        if (node.Kind is NotebookNodeKind.BulletedList or NotebookNodeKind.NumberedList or NotebookNodeKind.Checklist)
        {
            var list = new System.Windows.Documents.List { Margin = new(0, 0, 0, 14), Padding = new(22, 0, 0, 0),
                MarkerStyle = node.Kind == NotebookNodeKind.NumberedList ? TextMarkerStyle.Decimal :
                    node.Kind == NotebookNodeKind.Checklist ? TextMarkerStyle.None : TextMarkerStyle.Disc };
            foreach (var child in node.Children)
            {
                var item = new ListItem();
                if (node.Kind == NotebookNodeKind.Checklist)
                    item.Blocks.Add(new Paragraph(new Run(child.IsChecked == true ? "☑ Checked" : "☐ Not checked")) { Margin = new(0) });
                foreach (var nested in child.Children) item.Blocks.Add(Block(nested));
                list.ListItems.Add(item);
            }
            return list;
        }
        var paragraph = new Paragraph { Margin = new(0, 0, 0, 14) };
        if (node.Kind == NotebookNodeKind.Image)
        {
            paragraph.Inlines.Add(new Run($"Image · {node.Caption}\n{node.AlternativeText}\nImage preview unavailable."));
            paragraph.SetResourceReference(TextElement.ForegroundProperty, "PtjTextSecondaryBrush");
            return paragraph;
        }
        if (node.Kind == NotebookNodeKind.Heading)
        { paragraph.FontSize = node.HeadingLevel switch { 1 => 24, 2 => 20, _ => 17 }; paragraph.FontWeight = FontWeights.SemiBold; }
        foreach (var inline in node.Children) paragraph.Inlines.Add(Inline(inline));
        return paragraph;
    }
    private static Inline Inline(NotebookNode node)
    {
        if (node.Kind == NotebookNodeKind.LineBreak) return new LineBreak();
        if (node.Kind == NotebookNodeKind.Hyperlink)
        {
            // Informational URL text, not an inactive link control pretending navigation is implemented.
            var span = new Span { ToolTip = node.Url };
            foreach (var child in node.Children) span.Inlines.Add(Inline(child));
            span.Inlines.Add(new Run($" ({node.Url})")); return span;
        }
        var run = new Run(node.Text);
        if (node.Marks.HasFlag(NotebookTextMarks.Bold)) run.FontWeight = FontWeights.Bold;
        if (node.Marks.HasFlag(NotebookTextMarks.Italic)) run.FontStyle = FontStyles.Italic;
        if (node.Marks.HasFlag(NotebookTextMarks.Underline)) run.TextDecorations = TextDecorations.Underline;
        return run;
    }
}
