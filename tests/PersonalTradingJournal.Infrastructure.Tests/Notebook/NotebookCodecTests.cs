using System.Text;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;
using PersonalTradingJournal.Infrastructure.Notebook;

namespace PersonalTradingJournal.Infrastructure.Tests.Notebook;

public sealed class NotebookCodecTests
{
    private readonly NotebookJsonCodec codec = new();
    internal static NotebookNode P(string text) => NotebookNode.Paragraph(Guid.NewGuid(), [NotebookNode.Run(Guid.NewGuid(), text)]);
    internal static NotebookDocument Rich(Guid? image = null) => new([
        NotebookNode.Heading(Guid.NewGuid(), 3, [NotebookNode.Run(Guid.NewGuid(), "Изследване café 😀", NotebookTextMarks.Bold | NotebookTextMarks.Italic | NotebookTextMarks.Underline)]),
        NotebookNode.Paragraph(Guid.NewGuid(), [NotebookNode.Run(Guid.NewGuid(), "First\r\n"), NotebookNode.Break(Guid.NewGuid()),
            NotebookNode.Hyperlink(Guid.NewGuid(), "https://example.org/research?a=1&b=2", [NotebookNode.Run(Guid.NewGuid(), "Visible link")])]),
        NotebookNode.List(Guid.NewGuid(), NotebookNodeKind.BulletedList, [NotebookNode.Item(Guid.NewGuid(), [
            P("Bullet"), NotebookNode.List(Guid.NewGuid(), NotebookNodeKind.NumberedList, [NotebookNode.Item(Guid.NewGuid(), [P("Numbered")])])])]),
        NotebookNode.List(Guid.NewGuid(), NotebookNodeKind.Checklist, [NotebookNode.Item(Guid.NewGuid(), [P("Checked")], true),
            NotebookNode.Item(Guid.NewGuid(), [P("Unchecked")], false)]),
        .. image is { } id ? new[] { NotebookNode.Image(Guid.NewGuid(), id, "Chart", "Alt") } : []
    ]);
    [Fact]
    public void EverySupportedNodeRoundTripsWithStableIdentityOrderAndProjection()
    {
        var original = Rich(Guid.NewGuid()); var encoded = codec.Encode(original);
        var read = codec.Decode(encoded.Json);
        Assert.Equal(NotebookContentStatus.Success, read.Status);
        Assert.Equal(encoded, codec.Encode(read.Document!));
        Assert.Equal(original.PlainText, read.Document!.PlainText);
        Assert.DoesNotContain("example.org", encoded.PlainText); Assert.DoesNotContain("Alt", encoded.PlainText);
        Assert.Equal(original.Blocks.Select(x => x.Id), read.Document.Blocks.Select(x => x.Id));
    }
    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"format\":\"ptj.notebook.document\",\"version\":1,\"blocks\":[],\"$type\":\"System.Object\"}")]
    [InlineData("{\"format\":\"ptj.notebook.document\",\"version\":1,\"version\":1,\"blocks\":[]}")]
    [InlineData("{\"format\":\"other\",\"version\":1,\"blocks\":[]}")]
    [InlineData("{\"format\":\"ptj.notebook.document\",\"version\":1,\"blocks\":null}")]
    public void MalformedAndUnknownStructureNeverBecomesAnEmptyDocument(string input)
    {
        var result = codec.Decode(input); Assert.Equal(NotebookContentStatus.Invalid, result.Status); Assert.Null(result.Document);
    }
    [Fact]
    public void FutureVersionIsExplicitAndNeverInterpreted()
    {
        var input = codec.Encode(NotebookDocument.Empty).Json.Replace("\"version\":1", "\"version\":2");
        Assert.Equal(NotebookContentStatus.UnsupportedVersion, codec.Decode(input).Status);
    }
    [Theory]
    [InlineData("text", "\"text\":\"x\",\"marks\":8")]
    [InlineData("heading", "\"level\":4,\"children\":[]")]
    [InlineData("hyperlink", "\"url\":\"file:///secret\",\"children\":[]")]
    [InlineData("xaml", "\"text\":\"unsafe\"")]
    public void InvalidNodeValuesAreRejected(string kind, string fields)
    {
        var input = "{\"format\":\"ptj.notebook.document\",\"version\":1,\"blocks\":[{\"id\":\"" + Guid.NewGuid() + "\",\"kind\":\"" + kind + "\"," + fields + "}]}";
        Assert.Equal(NotebookContentStatus.Invalid, codec.Decode(input).Status);
    }
    [Fact]
    public void ExactByteLimitAndEscapingAreEnforcedOnBothPaths()
    {
        string empty = codec.Encode(NotebookDocument.Empty).Json;
        string atLimit = empty + new string(' ', NotebookLimits.DocumentBytes - Encoding.UTF8.GetByteCount(empty));
        Assert.Equal(NotebookContentStatus.Success, codec.Decode(atLimit).Status);
        Assert.Equal(NotebookContentStatus.TooLarge, codec.Decode(atLimit + " ").Status);
        Assert.Equal(NotebookContentStatus.Invalid, codec.Decode("\ud800").Status);
        Assert.Throws<ArgumentOutOfRangeException>(() => codec.Encode(new([P(new string('中', 199_999))])));
    }
    [Fact]
    public void DuplicateIdentitiesAndDeepJsonAreRejected()
    {
        var one = codec.Encode(new([P("A")])).Json;
        var node = System.Text.Json.Nodes.JsonNode.Parse(one)!["blocks"]![0]!.DeepClone();
        var root = System.Text.Json.Nodes.JsonNode.Parse(one)!;
        root["blocks"]!.AsArray().Add(node);
        Assert.Equal(NotebookContentStatus.Invalid, codec.Decode(root.ToJsonString()).Status);
        Assert.Equal(NotebookContentStatus.Invalid, codec.Decode(new string('[', 80) + new string(']', 80)).Status);
    }
}
