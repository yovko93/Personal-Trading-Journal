using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Application.Tests.Notebook;

public sealed class NotebookTemplateTests
{
    [Theory]
    [InlineData(NotebookTemplate.BlankNote, "")]
    [InlineData(NotebookTemplate.MarketStudy, "Context\nObservation\nExamples\nConclusion\nOpen questions\n")]
    [InlineData(NotebookTemplate.TradingIdea, "Idea\nConditions\nInvalidation\nEvidence to collect\n")]
    [InlineData(NotebookTemplate.LessonLearned, "Situation\nObservation\nLesson\nNext action\n")]
    [InlineData(NotebookTemplate.Psychology, "Trigger\nThoughts\nBehavior\nAlternative response\n")]
    public void TemplatesHaveExactAgreedSectionsAndFreshIndependentIdentities(NotebookTemplate template, string text)
    {
        var first = NotebookTemplates.Create(template); var second = NotebookTemplates.Create(template);
        Assert.NotSame(first, second); Assert.Equal(text, first.PlainText); Assert.Equal(text, second.PlainText);
        var nodes = first.Blocks.SelectMany(x => x.Children.Prepend(x)).ToArray();
        var other = second.Blocks.SelectMany(x => x.Children.Prepend(x)).ToArray();
        Assert.Empty(nodes.Select(x => x.Id).Intersect(other.Select(x => x.Id)));
        Assert.All(first.Blocks, x => { Assert.Equal(NotebookNodeKind.Heading, x.Kind); Assert.Equal(2, x.HeadingLevel); });
        Assert.Empty(first.ImageNodes);
        var a = new NotebookNote(DateTimeOffset.UtcNow, document: first); var b = new NotebookNote(DateTimeOffset.UtcNow, document: second);
        Assert.NotEqual(a.Id, b.Id); Assert.Equal(1, a.Version); Assert.Empty(a.Links); Assert.Empty(a.Images); Assert.Empty(a.TagIds);
        a.UpdateContent(NotebookDocument.Empty, [], DateTimeOffset.UtcNow); Assert.Equal(text, b.Document.PlainText);
    }
    [Fact] public void UnknownTemplateIsRejected() => Assert.Throws<ArgumentOutOfRangeException>(() => NotebookTemplates.Create((NotebookTemplate)99));
}
