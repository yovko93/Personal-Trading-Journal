using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using PersonalTradingJournal.Domain.Notebook;
using List = System.Windows.Documents.List;

namespace PersonalTradingJournal.Desktop.Views.Notebook;

/// <summary>Allowlisted bidirectional Desktop projection. No XAML/RTF deserialization or external fetching.</summary>
internal static partial class NotebookEditorAdapter
{
    internal static FlowDocument Create(NotebookDocument source, Func<NotebookNode, BlockUIContainer>? imageFactory = null)
    {
        var result = new FlowDocument { PagePadding = new(4), FontSize = 15, LineHeight = 23 };
        result.SetResourceReference(TextElement.ForegroundProperty, "PtjTextPrimaryBrush");
        foreach (var node in source.Blocks) result.Blocks.Add(CreateBlock(node, imageFactory));
        return result;
    }
    private static Block CreateBlock(NotebookNode n, Func<NotebookNode, BlockUIContainer>? imageFactory = null)
    {
        if (n.Kind is NotebookNodeKind.BulletedList or NotebookNodeKind.NumberedList or NotebookNodeKind.Checklist)
        {
            var list = new List { Tag = n.Id, Margin = new(0, 0, 0, 10), Padding = new(24, 0, 0, 0),
                MarkerStyle = n.Kind == NotebookNodeKind.NumberedList ? TextMarkerStyle.Decimal : n.Kind == NotebookNodeKind.Checklist ? TextMarkerStyle.None : TextMarkerStyle.Disc };
            foreach (var child in n.Children)
            {
                var item = new ListItem { Tag = child.Id };
                if (n.Kind == NotebookNodeKind.Checklist)
                    item.Blocks.Add(new Paragraph(new Run(child.IsChecked == true ? "☑ Checked" : "☐ Not checked")) { Margin = new(0), Tag = "check" });
                foreach (var block in child.Children) item.Blocks.Add(CreateBlock(block, imageFactory));
                list.ListItems.Add(item);
            }
            return list;
        }
        if (n.Kind == NotebookNodeKind.Image)
        {
            if (imageFactory is not null) return imageFactory(n);
            return new BlockUIContainer(new TextBlock { Text = $"Image · {n.Caption}\n{n.AlternativeText}\nImage preview unavailable.", TextWrapping = TextWrapping.Wrap }) { Tag = n };
        }
        var p = new Paragraph { Tag = n.Id, Margin = new(0, 0, 0, 12),
            LineHeight = n.Kind == NotebookNodeKind.Heading ? n.HeadingLevel switch { 1 => 32, 2 => 28, _ => 25 } : 23,
            FontSize = n.Kind == NotebookNodeKind.Heading ? n.HeadingLevel switch { 1 => 24, 2 => 20, _ => 17 } : 15 };
        foreach (var inline in n.Children) p.Inlines.Add(CreateInline(inline));
        return p;
    }
    private static Inline CreateInline(NotebookNode n)
    {
        if (n.Kind == NotebookNodeKind.LineBreak) return new LineBreak { Tag = n.Id };
        if (n.Kind == NotebookNodeKind.Hyperlink)
        {
            var link = new Hyperlink { Tag = n.Id, NavigateUri = new(n.Url!), TextDecorations = null };
            link.SetResourceReference(TextElement.ForegroundProperty, "PtjInformationBrush");
            foreach (var child in n.Children) link.Inlines.Add(CreateInline(child));
            return link;
        }
        return new Run(n.Text) { Tag = n.Id, FontWeight = n.Marks.HasFlag(NotebookTextMarks.Bold) ? FontWeights.Bold : FontWeights.Normal,
            FontStyle = n.Marks.HasFlag(NotebookTextMarks.Italic) ? FontStyles.Italic : FontStyles.Normal,
            TextDecorations = n.Marks.HasFlag(NotebookTextMarks.Underline) ? TextDecorations.Underline : null };
    }
    internal static NotebookDocument Read(FlowDocument document)
    {
        var ids = new HashSet<Guid>();
        return new(document.Blocks.Select(b => ReadBlock(b, ids)));
    }
    private static Guid Id(FrameworkContentElement element, HashSet<Guid> ids)
    {
        var id = element.Tag is Guid existing && !ids.Contains(existing) ? existing : Guid.NewGuid();
        element.Tag = id; ids.Add(id); return id;
    }
    private static NotebookNode ReadBlock(Block block, HashSet<Guid> ids)
    {
        if (block is BlockUIContainer { Tag: NotebookNode image } && image.Kind == NotebookNodeKind.Image)
        { if (!ids.Add(image.Id)) throw new ArgumentException("Duplicate protected image."); return image; }
        if (block is List list)
        {
            var kind = list.MarkerStyle == TextMarkerStyle.None ? NotebookNodeKind.Checklist :
                list.MarkerStyle is TextMarkerStyle.Decimal or TextMarkerStyle.LowerLatin or TextMarkerStyle.UpperLatin ? NotebookNodeKind.NumberedList : NotebookNodeKind.BulletedList;
            return NotebookNode.List(Id(list, ids), kind, list.ListItems.Select(item =>
            {
                var blocks = item.Blocks.ToArray();
                bool check = kind == NotebookNodeKind.Checklist;
                if (check && (blocks.Length == 0 || blocks[0] is not Paragraph)) throw new ArgumentException("Checklist marker is missing. Undo to restore it.");
                // TextRange.Text adds list-prefix/tab characters even for this paragraph.
                // The marker is our own allowlisted runs, not the range's export representation.
                var marker = check ? MarkerText((Paragraph)blocks[0]) : "";
                if (check && marker != "☑ Checked" && marker != "☐ Not checked") throw new ArgumentException("Use Toggle check to change a checklist marker, or Undo to restore it.");
                return NotebookNode.Item(Id(item, ids), blocks.Skip(check ? 1 : 0).Select(b => ReadBlock(b, ids)), check ? marker.StartsWith("☑") : null);
            }));
        }
        if (block is not Paragraph p) throw new ArgumentException("Unsupported editor block. Undo the change.");
        var children = p.Inlines.SelectMany(i => ReadInline(i, ids)).ToArray();
        // LineHeight is a native undoable paragraph property; the local toolbar owns these
        // presentation values. Font-size runs may be mixed during selection/undo.
        int level = p.LineHeight == 32 ? 1 : p.LineHeight == 28 ? 2 : p.LineHeight == 25 ? 3 : 0;
        return level == 0 ? NotebookNode.Paragraph(Id(p, ids), children) : NotebookNode.Heading(Id(p, ids), level, children);
    }
    private static IEnumerable<NotebookNode> ReadInline(Inline inline, HashSet<Guid> ids)
    {
        if (inline is Run r)
        {
            var marks = NotebookTextMarks.None;
            if (r.FontWeight >= FontWeights.SemiBold) marks |= NotebookTextMarks.Bold;
            if (r.FontStyle == FontStyles.Italic) marks |= NotebookTextMarks.Italic;
            if (r.TextDecorations?.Any(d => d.Location == TextDecorationLocation.Underline) == true) marks |= NotebookTextMarks.Underline;
            yield return NotebookNode.Run(Id(r, ids), r.Text, marks);
        }
        else if (inline is LineBreak b) yield return NotebookNode.Break(Id(b, ids));
        else if (inline is Hyperlink h)
            yield return NotebookNode.Hyperlink(Id(h, ids), h.NavigateUri?.AbsoluteUri ?? "", h.Inlines.SelectMany(i => ReadInline(i, ids)));
        else if (inline is Span s)
            foreach (var child in s.Inlines.SelectMany(i => ReadInline(i, ids))) yield return child;
        else throw new ArgumentException("Unsupported inline content. Undo the change.");
    }
    internal static T? Ancestor<T>(TextPointer position) where T : FrameworkContentElement
    {
        for (DependencyObject? node = position.Parent; node is not null; node = node is FrameworkContentElement f ? f.Parent : null)
            if (node is T match) return match;
        return null;
    }
    internal static void Heading(RichTextBox editor, int level)
    {
        editor.BeginChange();
        try
        {
            var p = editor.Selection.Start.Paragraph;
            while (p is not null && p.ContentStart.CompareTo(editor.Selection.End) <= 0)
            {
                new TextRange(p.ContentStart, p.ContentEnd).ApplyPropertyValue(TextElement.FontSizeProperty, level switch { 1 => 24d, 2 => 20d, 3 => 17d, _ => 15d });
                new TextRange(p.ContentStart, p.ContentEnd).ApplyPropertyValue(Block.LineHeightProperty, level switch { 1 => 32d, 2 => 28d, 3 => 25d, _ => 23d });
                p = p.NextBlock as Paragraph;
            }
        }
        finally { editor.EndChange(); }
    }
    internal static void Checklist(RichTextBox editor)
    {
        var p = editor.CaretPosition.Paragraph;
        if (p is null || p.Parent is not FlowDocument doc) throw new ArgumentException("Place the caret in a top-level paragraph to create a checklist.");
        editor.BeginChange();
        try
        {
            var list = new List { MarkerStyle = TextMarkerStyle.None, Padding = new(24, 0, 0, 0) };
            doc.Blocks.InsertBefore(p, list); doc.Blocks.Remove(p);
            var item = new ListItem(new Paragraph(new Run("☐ Not checked")) { Margin = new(0) });
            item.Blocks.Add(p); list.ListItems.Add(item);
            editor.CaretPosition = p.ContentStart;
        }
        finally { editor.EndChange(); }
    }
    internal static void ToggleCheck(RichTextBox editor)
    {
        var item = Ancestor<ListItem>(editor.CaretPosition);
        if (item?.Parent is not List { MarkerStyle: TextMarkerStyle.None } || item.Blocks.FirstBlock is not Paragraph marker)
            throw new ArgumentException("Place the caret in a checklist item.");
        var range = new TextRange(marker.ContentStart, marker.ContentEnd);
        range.Text = MarkerText(marker).StartsWith("☑") ? "☐ Not checked" : "☑ Checked";
    }
    internal static bool AddChecklistItem(RichTextBox editor)
    {
        var item = Ancestor<ListItem>(editor.CaretPosition);
        if (item?.Parent is not List { MarkerStyle: TextMarkerStyle.None } list || !editor.Selection.IsEmpty) return false;
        if (editor.CaretPosition.Paragraph == item.Blocks.FirstBlock) return false;
        if (editor.CaretPosition.Paragraph is { } current &&
            new TextRange(editor.CaretPosition, current.ContentEnd).Text.TrimEnd('\r', '\n').Length != 0)
        { EditingCommands.EnterLineBreak.Execute(null, editor); return true; }
        editor.BeginChange();
        try
        {
            var next = new ListItem(new Paragraph(new Run("☐ Not checked")) { Margin = new(0) });
            var body = new Paragraph(); next.Blocks.Add(body);
            list.ListItems.InsertAfter(item, next); editor.CaretPosition = body.ContentStart;
        }
        finally { editor.EndChange(); }
        return true;
    }
    private static string MarkerText(Paragraph p) => string.Concat(p.Inlines.Select(i => i is Run r ? r.Text : "\0"));
    internal static Hyperlink? SelectedLink(TextSelection selection)
    {
        var ancestor = Ancestor<Hyperlink>(selection.Start);
        if (ancestor is not null) return ancestor;
        return selection.Start.Paragraph is { } paragraph ? Links(paragraph.Inlines).FirstOrDefault(h =>
            h.ElementStart.CompareTo(selection.End) <= 0 && h.ElementEnd.CompareTo(selection.Start) >= 0) : null;
    }
    private static IEnumerable<Hyperlink> Links(InlineCollection inlines)
    {
        foreach (var inline in inlines)
            if (inline is Hyperlink link) yield return link;
            else if (inline is Span span) foreach (var nested in Links(span.Inlines)) yield return nested;
    }
    internal static void SetLink(RichTextBox editor, string? url)
    {
        var existing = SelectedLink(editor.Selection);
        if (url is not null) _ = NotebookNode.Hyperlink(Guid.NewGuid(), url, []);
        editor.BeginChange();
        try
        {
            if (existing is not null)
            {
                var children = existing.Inlines.ToArray();
                var parent = existing.Parent is Paragraph p ? p.Inlines : ((Span)existing.Parent).Inlines;
                foreach (var child in children) { existing.Inlines.Remove(child); parent.InsertBefore(existing, child); }
                parent.Remove(existing);
                if (url is not null && children.Length > 0)
                {
                    var replacement = new Hyperlink(children[0].ElementStart, children[^1].ElementEnd) { NavigateUri = new(url), TextDecorations = null };
                    editor.Selection.Select(replacement.ContentStart, replacement.ContentEnd);
                }
            }
            else if (url is not null)
            {
                if (editor.Selection.IsEmpty || editor.Selection.Start.Paragraph != editor.Selection.End.Paragraph)
                    throw new ArgumentException("Select text in one paragraph for the link.");
                var link = new Hyperlink(editor.Selection.Start, editor.Selection.End) { NavigateUri = new(url), TextDecorations = null };
                editor.Selection.Select(link.ContentStart, link.ContentEnd);
            }
        }
        finally { editor.EndChange(); }
    }
    internal static void PasteText(RichTextBox editor, string text, Action<NotebookDocument> validate)
    {
        NotebookLimits.Text(text, NotebookLimits.PlainText, nameof(text));
        _ = Read(editor.Document);
        // Preserve the live text-container shape (including Span wrappers and split Runs)
        // so selection offsets mean the same thing in the admission candidate.
        var copy = new FlowDocument { FontSize = editor.Document.FontSize };
        foreach (var block in editor.Document.Blocks) copy.Blocks.Add(CloneBlock(block));
        var trial = new RichTextBox { Document = copy };
        int start = editor.Document.ContentStart.GetOffsetToPosition(editor.Selection.Start);
        int end = editor.Document.ContentStart.GetOffsetToPosition(editor.Selection.End);
        var a = trial.Document.ContentStart.GetPositionAtOffset(start);
        var b = trial.Document.ContentStart.GetPositionAtOffset(end);
        if (a is null || b is null) throw new ArgumentException("This selection cannot be pasted safely.");
        trial.Selection.Select(a, b); trial.Selection.Text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        validate(Read(trial.Document)); // Validate the entire resulting draft before changing the real editor.
        editor.Selection.Text = text.Replace("\r\n", "\n").Replace('\r', '\n');
    }
    private static T Formatting<T>(TextElement source, T target) where T : TextElement
    {
        target.FontSize = source.FontSize; target.FontWeight = source.FontWeight; target.FontStyle = source.FontStyle; target.Tag = source.Tag;
        if (source is Inline a && target is Inline b) b.TextDecorations = a.TextDecorations;
        return target;
    }
    private static Block CloneBlock(Block source)
    {
        if (source is BlockUIContainer { Tag: NotebookNode image } && image.Kind == NotebookNodeKind.Image)
            return CreateBlock(image);
        if (source is Paragraph p)
        {
            var copy = Formatting(p, new Paragraph { LineHeight = p.LineHeight });
            foreach (var child in p.Inlines) copy.Inlines.Add(CloneInline(child));
            return copy;
        }
        if (source is List list)
        {
            var copy = Formatting(list, new List { MarkerStyle = list.MarkerStyle });
            foreach (var item in list.ListItems)
            {
                var clone = Formatting(item, new ListItem());
                foreach (var block in item.Blocks) clone.Blocks.Add(CloneBlock(block));
                copy.ListItems.Add(clone);
            }
            return copy;
        }
        throw new ArgumentException("This protected block cannot accept pasted text.");
    }
    private static Inline CloneInline(Inline source)
    {
        if (source is Run r) return Formatting(r, new Run(r.Text));
        if (source is LineBreak b) return Formatting(b, new LineBreak());
        if (source is Span span)
        {
            Span copy = source is Hyperlink h ? new Hyperlink { NavigateUri = h.NavigateUri } : new Span();
            Formatting(span, copy);
            foreach (var child in span.Inlines) copy.Inlines.Add(CloneInline(child));
            return copy;
        }
        throw new ArgumentException("Unsupported pasted structure.");
    }
}
