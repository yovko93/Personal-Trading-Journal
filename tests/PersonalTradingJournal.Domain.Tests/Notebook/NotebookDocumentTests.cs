using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Domain.Tests.Notebook;

public sealed class NotebookDocumentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static Guid Id() => Guid.NewGuid();
    private static NotebookNode Paragraph(string text) => NotebookNode.Paragraph(Id(), [NotebookNode.Run(Id(), text)]);

    [Fact]
    public void ProjectionIncludesVisibleTextAndCaptionsNotUrlsOrAlternativeText()
    {
        var document = new NotebookDocument([
            NotebookNode.Heading(Id(), 2, [NotebookNode.Run(Id(), "Observation", NotebookTextMarks.Bold)]),
            NotebookNode.Paragraph(Id(), [
                NotebookNode.Run(Id(), "Before "), NotebookNode.Hyperlink(Id(), "https://example.org/hidden", [NotebookNode.Run(Id(), "link")]),
                NotebookNode.Break(Id()), NotebookNode.Run(Id(), "After", NotebookTextMarks.Italic | NotebookTextMarks.Underline)]),
            NotebookNode.List(Id(), NotebookNodeKind.Checklist, [NotebookNode.Item(Id(), [Paragraph("Checked")], true)]),
            NotebookNode.Image(Id(), Id(), "Chart caption", "Alternative")]);
        Assert.Equal("Observation\nBefore link\nAfter\nChecked\nChart caption\n", document.PlainText);
        Assert.Equal(1, document.Version);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/secret")]
    [InlineData("data:text/html,test")]
    [InlineData("/relative")]
    public void UnsupportedLinkSchemesAreRejected(string url) =>
        Assert.Throws<ArgumentException>(() => NotebookNode.Hyperlink(Id(), url, []));

    [Fact]
    public void NodesAreDefensivelyCopiedAndHaveUniqueDocumentIdentities()
    {
        var nodes = new List<NotebookNode> { Paragraph("Original") };
        var document = new NotebookDocument(nodes); nodes.Clear();
        Assert.Equal("Original\n", document.PlainText); Assert.Single(document.Blocks);
        Assert.Throws<NotSupportedException>(() => ((IList<NotebookNode>)document.Blocks).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<NotebookNode>)document.Blocks[0].Children).Clear());
        Assert.Throws<ArgumentException>(() => new NotebookDocument([document.Blocks[0], document.Blocks[0]]));
        Assert.Throws<ArgumentException>(() => new NotebookDocument([NotebookNode.Run(Id(), "Not a block")]));
    }

    [Fact]
    public void ContentVersionStructureAndFormattingAreValidated()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NotebookDocument([], 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => NotebookNode.Heading(Id(), 4, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => NotebookNode.Run(Id(), "Text", (NotebookTextMarks)8));
        Assert.Throws<ArgumentException>(() => new NotebookDocument([NotebookNode.Paragraph(Id(), [Paragraph("Nested")])]));
        Assert.Throws<ArgumentException>(() => new NotebookDocument([NotebookNode.List(Id(), NotebookNodeKind.Checklist, [NotebookNode.Item(Id(), [])])]));
        Assert.Throws<ArgumentException>(() => new NotebookDocument([NotebookNode.List(Id(), NotebookNodeKind.BulletedList, [NotebookNode.Item(Id(), [], true)])]));
        Assert.Throws<ArgumentException>(() => new NotebookDocument([NotebookNode.Paragraph(Id(), [
            NotebookNode.Hyperlink(Id(), "https://example.org", [NotebookNode.Hyperlink(Id(), "https://example.org", [])])])]));
    }

    [Fact]
    public void PlainTextNodeAndListNestingBoundsAreEnforced()
    {
        Assert.Equal(200_000, new NotebookDocument([Paragraph(new string('x', 199_999))]).PlainText.Length);
        Assert.Throws<ArgumentException>(() => new NotebookDocument([Paragraph(new string('x', 200_000))]));
        Assert.Equal(4096, new NotebookDocument(Enumerable.Range(0, 4096).Select(_ => NotebookNode.Paragraph(Id(), []))).Blocks.Count);
        Assert.Throws<ArgumentException>(() => new NotebookDocument(Enumerable.Range(0, 4097).Select(_ => NotebookNode.Paragraph(Id(), []))));
        NotebookNode nested = Paragraph("Text");
        for (int i = 0; i < 3; i++) nested = NotebookNode.List(Id(), NotebookNodeKind.NumberedList, [NotebookNode.Item(Id(), [nested])]);
        Assert.Equal("Text\n", new NotebookDocument([nested]).PlainText);
        nested = NotebookNode.List(Id(), NotebookNodeKind.BulletedList, [NotebookNode.Item(Id(), [nested])]);
        Assert.Throws<ArgumentException>(() => new NotebookDocument([nested]));
    }

    [Fact]
    public void OwnedImagesAndTradeReferencesHaveDisjointOwnership()
    {
        var owned = NotebookImage.Owned(Id(), 0, "note.webp", NotebookLimits.ImageBytes, new string('b', 64), "Caption");
        var reference = NotebookImage.TradeReference(Id(), 1, Id(), Id());
        Assert.Equal(NotebookImageOwnership.NoteOwned, owned.Ownership); Assert.Null(owned.TradeId);
        Assert.Equal(new string('B', 64), owned.Sha256);
        Assert.Equal(NotebookImageOwnership.TradeScreenshotReference, reference.Ownership);
        Assert.Null(reference.StorageKey); Assert.Null(reference.SizeBytes); Assert.Null(reference.Sha256);
        Assert.NotNull(reference.TradeScreenshotId);
        Assert.Throws<ArgumentException>(() => NotebookImage.TradeReference(Id(), 0, Guid.Empty, Id()));
    }

    [Theory]
    [InlineData("../note.png")]
    [InlineData("C:\\note.png")]
    [InlineData("note.gif")]
    [InlineData("https://example.org/note.png")]
    public void ImageMetadataRejectsPathsAndUnsupportedExtensions(string key) =>
        Assert.ThrowsAny<ArgumentException>(() => NotebookImage.Owned(Id(), 0, key, 1, new string('b', 64)));

    [Fact]
    public void ImageMetadataRejectsUnboundedAndUnverifiableValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NotebookImage.Owned(Id(), 0, "a.png", 0, new string('b', 64)));
        Assert.Throws<ArgumentOutOfRangeException>(() => NotebookImage.Owned(Id(), 0, "a.png", NotebookLimits.ImageBytes + 1L, new string('b', 64)));
        Assert.Throws<ArgumentException>(() => NotebookImage.Owned(Id(), 0, "a.png", 1, "not a hash"));
        Assert.Throws<ArgumentOutOfRangeException>(() => NotebookImage.TradeReference(Id(), 32, Id(), Id()));
        Assert.Throws<ArgumentOutOfRangeException>(() => NotebookNode.Image(Id(), Id(), new string('c', 2001)));
        Assert.Throws<ArgumentOutOfRangeException>(() => NotebookNode.Image(Id(), Id(), alternativeText: new string('c', 501)));
    }

    [Fact]
    public void ImageEditsMustMatchDocumentAndCannotPartiallyReplaceContent()
    {
        var note = new NotebookNote(Now);
        var image = NotebookImage.Owned(Id(), 0, "a.png", 1, new string('a', 64), "Caption");
        var document = new NotebookDocument([NotebookNode.Image(Id(), image.Id, image.Caption)]);
        Assert.Throws<ArgumentException>(() => note.UpdateContent(document, [], Now));
        Assert.Equal(1, note.Version); Assert.Same(NotebookDocument.Empty, note.Document);
        note.UpdateContent(document, [image], Now);
        Assert.Throws<ArgumentException>(() => note.UpdateContent(new([NotebookNode.Image(Id(), image.Id, "Wrong caption")]), [image], Now));
        Assert.Same(document, note.Document); Assert.Equal(2, note.Version);
        Assert.Throws<NotSupportedException>(() => ((IList<NotebookImage>)note.Images).Clear());
        Assert.Throws<ArgumentException>(() => new NotebookDocument([NotebookNode.Image(Id(), image.Id), NotebookNode.Image(Id(), image.Id)]));
        note.UpdateContent(NotebookDocument.Empty, [], Now);
        Assert.Empty(note.Images); Assert.Empty(note.Document.PlainText);
    }

    [Fact]
    public void AttachmentOrderAndOwnedKeysCannotBeAliased()
    {
        var note = new NotebookNote(Now);
        var first = NotebookImage.Owned(Id(), 0, "a.png", 1, new string('a', 64));
        var second = NotebookImage.Owned(Id(), 1, "A.PNG", 1, new string('a', 64));
        var document = new NotebookDocument([NotebookNode.Image(Id(), first.Id), NotebookNode.Image(Id(), second.Id)]);
        Assert.Throws<ArgumentException>(() => note.UpdateContent(document, [first, second], Now));
        second = NotebookImage.Owned(second.Id, 1, "b.png", 1, new string('a', 64));
        Assert.Throws<ArgumentException>(() => note.UpdateContent(document, [second, first], Now));
        note.UpdateContent(document, [first, second], Now);
        Assert.Equal(2, note.Images.Count);
    }
}
