using System.Text;

namespace PersonalTradingJournal.Domain.Notebook;

[Flags]
public enum NotebookTextMarks { None = 0, Bold = 1, Italic = 2, Underline = 4 }
public enum NotebookNodeKind { Text, LineBreak, Hyperlink, Paragraph, Heading, BulletedList, NumberedList, Checklist, ListItem, ChecklistItem, Image }

/// <summary>Allowlisted, immutable document data, not a serialized WPF object. Factories exclude irrelevant attributes.</summary>
public sealed class NotebookNode
{
    private NotebookNode(Guid id, NotebookNodeKind kind, IEnumerable<NotebookNode>? children = null,
        string? text = null, NotebookTextMarks marks = NotebookTextMarks.None, int? level = null,
        bool? isChecked = null, string? url = null, Guid? imageId = null, string? caption = null, string? alternativeText = null)
    {
        Id = NotebookLimits.Id(id, nameof(id)); Kind = kind;
        Children = NotebookLimits.Copy(children ?? [], NotebookLimits.Nodes, nameof(children));
        Text = text; Marks = marks; HeadingLevel = level; IsChecked = isChecked; Url = url;
        ImageId = imageId; Caption = caption; AlternativeText = alternativeText;
    }
    public Guid Id { get; }
    public NotebookNodeKind Kind { get; }
    public IReadOnlyList<NotebookNode> Children { get; }
    public string? Text { get; }
    public NotebookTextMarks Marks { get; }
    public int? HeadingLevel { get; }
    public bool? IsChecked { get; }
    public string? Url { get; }
    public Guid? ImageId { get; }
    public string? Caption { get; }
    public string? AlternativeText { get; }
    public static NotebookNode Run(Guid id, string text, NotebookTextMarks marks = NotebookTextMarks.None)
    {
        if ((marks & ~(NotebookTextMarks.Bold | NotebookTextMarks.Italic | NotebookTextMarks.Underline)) != 0)
            throw new ArgumentOutOfRangeException(nameof(marks));
        return new(id, NotebookNodeKind.Text, text: NotebookLimits.Text(text, NotebookLimits.PlainText, nameof(text)), marks: marks);
    }
    public static NotebookNode Break(Guid id) => new(id, NotebookNodeKind.LineBreak);
    public static NotebookNode Paragraph(Guid id, IEnumerable<NotebookNode> inlines) => new(id, NotebookNodeKind.Paragraph, inlines);
    public static NotebookNode Heading(Guid id, int level, IEnumerable<NotebookNode> inlines)
    {
        if (level is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(level));
        return new(id, NotebookNodeKind.Heading, inlines, level: level);
    }
    public static NotebookNode Hyperlink(Guid id, string url, IEnumerable<NotebookNode> runs)
    {
        NotebookLimits.Text(url, NotebookLimits.Url, nameof(url), true);
        if (url != url.Trim() || !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("Use an absolute HTTP/HTTPS URL.", nameof(url));
        return new(id, NotebookNodeKind.Hyperlink, runs, url: url);
    }
    public static NotebookNode List(Guid id, NotebookNodeKind kind, IEnumerable<NotebookNode> items)
    {
        if (kind is not (NotebookNodeKind.BulletedList or NotebookNodeKind.NumberedList or NotebookNodeKind.Checklist))
            throw new ArgumentOutOfRangeException(nameof(kind));
        return new(id, kind, items);
    }
    public static NotebookNode Item(Guid id, IEnumerable<NotebookNode> blocks, bool? isChecked = null) =>
        new(id, isChecked.HasValue ? NotebookNodeKind.ChecklistItem : NotebookNodeKind.ListItem, blocks, isChecked: isChecked);
    public static NotebookNode Image(Guid id, Guid imageId, string? caption = null, string? alternativeText = null) =>
        new(id, NotebookNodeKind.Image, imageId: NotebookLimits.Id(imageId, nameof(imageId)),
            caption: NotebookLimits.Text(caption ?? "", NotebookLimits.Caption, nameof(caption)),
            alternativeText: NotebookLimits.Text(alternativeText ?? "", NotebookLimits.AlternativeText, nameof(alternativeText)));
}

/// <summary>The search projection is derived, never accepted as independently editable input.
/// The future codec additionally enforces the exact serialized UTF-8 size before a write.</summary>
public sealed class NotebookDocument
{
    public const string Format = "ptj.notebook.document";
    public const int CurrentVersion = 1;
    public NotebookDocument(IEnumerable<NotebookNode> blocks, int version = CurrentVersion)
    {
        if (version != CurrentVersion) throw new ArgumentOutOfRangeException(nameof(version), "Unsupported Notebook content version.");
        Blocks = NotebookLimits.Copy(blocks, NotebookLimits.Nodes, nameof(blocks));
        var ids = new HashSet<Guid>(); var images = new List<NotebookNode>(); var text = new StringBuilder();
        foreach (var block in Blocks)
        {
            if (!IsBlock(block.Kind)) throw new ArgumentException("Document roots must be blocks.", nameof(blocks));
            Visit(block, 1, 0, ids, images, text);
        }
        PlainText = text.ToString();
        ImageNodes = images.AsReadOnly();
    }
    public int Version => CurrentVersion;
    public IReadOnlyList<NotebookNode> Blocks { get; }
    public string PlainText { get; }
    public IReadOnlyList<NotebookNode> ImageNodes { get; }
    public static NotebookDocument Empty { get; } = new([]);

    private static bool IsBlock(NotebookNodeKind kind) => kind is NotebookNodeKind.Paragraph or NotebookNodeKind.Heading
        or NotebookNodeKind.BulletedList or NotebookNodeKind.NumberedList or NotebookNodeKind.Checklist or NotebookNodeKind.Image;
    private static bool IsInline(NotebookNodeKind kind) => kind is NotebookNodeKind.Text or NotebookNodeKind.LineBreak or NotebookNodeKind.Hyperlink;
    private static void Visit(NotebookNode node, int depth, int lists, HashSet<Guid> ids, List<NotebookNode> images, StringBuilder text)
    {
        if (depth > NotebookLimits.Depth || !ids.Add(node.Id) || ids.Count > NotebookLimits.Nodes)
            throw new ArgumentException("Document depth/node limit or duplicate node identity.");
        bool isList = node.Kind is NotebookNodeKind.BulletedList or NotebookNodeKind.NumberedList or NotebookNodeKind.Checklist;
        if (isList && ++lists > NotebookLimits.ListDepth) throw new ArgumentException("List nesting exceeds three.");
        foreach (var child in node.Children)
        {
            bool valid = node.Kind switch
            {
                NotebookNodeKind.Paragraph or NotebookNodeKind.Heading => IsInline(child.Kind),
                NotebookNodeKind.Hyperlink => child.Kind is NotebookNodeKind.Text or NotebookNodeKind.LineBreak,
                NotebookNodeKind.BulletedList or NotebookNodeKind.NumberedList => child.Kind == NotebookNodeKind.ListItem,
                NotebookNodeKind.Checklist => child.Kind == NotebookNodeKind.ChecklistItem,
                NotebookNodeKind.ListItem or NotebookNodeKind.ChecklistItem => IsBlock(child.Kind),
                _ => false
            };
            if (!valid) throw new ArgumentException("Invalid child for Notebook node.");
            Visit(child, depth + 1, lists, ids, images, text);
        }
        string addition = node.Kind switch
        {
            NotebookNodeKind.Text => node.Text!,
            NotebookNodeKind.LineBreak => "\n",
            NotebookNodeKind.Image => node.Caption + "\n",
            NotebookNodeKind.Paragraph or NotebookNodeKind.Heading => "\n",
            _ => ""
        };
        if (text.Length + addition.Length > NotebookLimits.PlainText) throw new ArgumentException("Searchable content exceeds its limit.");
        text.Append(addition);
        if (node.Kind == NotebookNodeKind.Image)
        {
            if (images.Count >= NotebookLimits.Images || images.Any(x => x.ImageId == node.ImageId))
                throw new ArgumentException("Too many images or duplicate image reference.");
            images.Add(node);
        }
    }
}
