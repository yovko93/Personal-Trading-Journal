using System.Text;
using System.Text.Json;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Infrastructure.Notebook;

public sealed class NotebookJsonCodec : INotebookContentCodec
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    public NotebookDecodedContent Decode(string originalJson)
    {
        if (originalJson is null) return new(NotebookContentStatus.Invalid);
        try
        {
            if (originalJson.Length > NotebookLimits.DocumentBytes || StrictUtf8.GetByteCount(originalJson) > NotebookLimits.DocumentBytes)
                return new(NotebookContentStatus.TooLarge);
            using var json = JsonDocument.Parse(originalJson, new JsonDocumentOptions { MaxDepth = 40 });
            var root = json.RootElement;
            Properties(root, "format", "version", "blocks");
            if (String(root, "format") != NotebookDocument.Format) return new(NotebookContentStatus.Invalid);
            if (root.GetProperty("version").GetInt32() != NotebookDocument.CurrentVersion) return new(NotebookContentStatus.UnsupportedVersion);
            int count = 0;
            return new(NotebookContentStatus.Success, new NotebookDocument(ReadNodes(root.GetProperty("blocks"), 1, ref count)));
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException or FormatException or OverflowException or KeyNotFoundException)
        { return new(NotebookContentStatus.Invalid); }
    }

    public NotebookEncodedContent Encode(NotebookDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject(); writer.WriteString("format", NotebookDocument.Format); writer.WriteNumber("version", document.Version);
            writer.WriteStartArray("blocks");
            foreach (var node in document.Blocks) WriteNode(writer, node);
            writer.WriteEndArray(); writer.WriteEndObject(); CheckSize(writer); writer.Flush();
        }
        return new(StrictUtf8.GetString(buffer.GetBuffer(), 0, checked((int)buffer.Length)), document.PlainText, document.Version);
    }
    private static void CheckSize(Utf8JsonWriter writer)
    {
        if (writer.BytesCommitted + writer.BytesPending > NotebookLimits.DocumentBytes)
            throw new ArgumentOutOfRangeException("document", "Notebook JSON exceeds 1 MiB.");
    }
    private static void Properties(JsonElement node, params string[] names)
    {
        if (node.ValueKind != JsonValueKind.Object) throw new JsonException("Expected object.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in node.EnumerateObject())
            if (!names.Contains(property.Name, StringComparer.Ordinal) || !seen.Add(property.Name)) throw new JsonException("Unknown or duplicate property.");
        if (seen.Count != names.Length) throw new JsonException("Missing property.");
    }
    private static string String(JsonElement node, string name) => node.GetProperty(name).GetString() ?? throw new JsonException("Null string.");
    private static NotebookNode[] ReadNodes(JsonElement array, int depth, ref int count)
    {
        if (depth > NotebookLimits.Depth || array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > NotebookLimits.Nodes)
            throw new JsonException("Invalid node array.");
        var result = new List<NotebookNode>();
        foreach (var node in array.EnumerateArray())
        {
            if (++count > NotebookLimits.Nodes) throw new JsonException("Node limit.");
            var kind = String(node, "kind"); var id = node.GetProperty("id").GetGuid();
            string[] fields = kind switch
            {
                "text" => ["text", "marks"], "lineBreak" => [],
                "heading" => ["level", "children"], "hyperlink" => ["url", "children"],
                "checklistItem" => ["checked", "children"],
                "image" => ["imageId", "caption", "alternativeText"],
                "paragraph" or "bulletedList" or "numberedList" or "checklist" or "listItem" => ["children"],
                _ => throw new JsonException("Unsupported node.")
            };
            Properties(node, ["id", "kind", .. fields]);
            var children = fields.Contains("children") ? ReadNodes(node.GetProperty("children"), depth + 1, ref count) : [];
            result.Add(kind switch
            {
                "text" => NotebookNode.Run(id, String(node, "text"), (NotebookTextMarks)node.GetProperty("marks").GetInt32()),
                "lineBreak" => NotebookNode.Break(id),
                "paragraph" => NotebookNode.Paragraph(id, children),
                "heading" => NotebookNode.Heading(id, node.GetProperty("level").GetInt32(), children),
                "hyperlink" => NotebookNode.Hyperlink(id, String(node, "url"), children),
                "bulletedList" => NotebookNode.List(id, NotebookNodeKind.BulletedList, children),
                "numberedList" => NotebookNode.List(id, NotebookNodeKind.NumberedList, children),
                "checklist" => NotebookNode.List(id, NotebookNodeKind.Checklist, children),
                "listItem" => NotebookNode.Item(id, children),
                "checklistItem" => NotebookNode.Item(id, children, node.GetProperty("checked").GetBoolean()),
                "image" => NotebookNode.Image(id, node.GetProperty("imageId").GetGuid(), String(node, "caption"), String(node, "alternativeText")),
                _ => throw new JsonException()
            });
        }
        return result.ToArray();
    }
    private static void WriteNode(Utf8JsonWriter writer, NotebookNode node)
    {
        writer.WriteStartObject(); writer.WriteString("id", node.Id);
        string kind = node.Kind switch
        {
            NotebookNodeKind.LineBreak => "lineBreak", NotebookNodeKind.BulletedList => "bulletedList",
            NotebookNodeKind.NumberedList => "numberedList", NotebookNodeKind.ListItem => "listItem",
            NotebookNodeKind.ChecklistItem => "checklistItem",
            _ => node.Kind.ToString().ToLowerInvariant()
        };
        writer.WriteString("kind", kind);
        switch (node.Kind)
        {
            case NotebookNodeKind.Text: writer.WriteString("text", node.Text); writer.WriteNumber("marks", (int)node.Marks); break;
            case NotebookNodeKind.LineBreak: break;
            case NotebookNodeKind.Image:
                writer.WriteString("imageId", node.ImageId!.Value); writer.WriteString("caption", node.Caption);
                writer.WriteString("alternativeText", node.AlternativeText); break;
            default:
                if (node.HeadingLevel is { } level) writer.WriteNumber("level", level);
                if (node.Url is { } url) writer.WriteString("url", url);
                if (node.IsChecked is { } check) writer.WriteBoolean("checked", check);
                writer.WriteStartArray("children"); foreach (var child in node.Children) WriteNode(writer, child); writer.WriteEndArray(); break;
        }
        writer.WriteEndObject(); CheckSize(writer);
    }
}
