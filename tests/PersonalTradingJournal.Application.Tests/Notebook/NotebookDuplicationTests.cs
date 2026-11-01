using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Application.Tests.Notebook;

public sealed class NotebookDuplicationTests
{
    [Fact]
    public void CopyPreservesAllNodeKindsAndMarksWithFreshIdentities()
    {
        var image=Guid.NewGuid(); var copyImage=Guid.NewGuid();
        var source=new NotebookDocument([
            NotebookNode.Heading(Guid.NewGuid(),3,[NotebookNode.Run(Guid.NewGuid(),"Title",NotebookTextMarks.Bold|NotebookTextMarks.Italic|NotebookTextMarks.Underline)]),
            NotebookNode.Paragraph(Guid.NewGuid(),[NotebookNode.Hyperlink(Guid.NewGuid(),"https://example.org",[NotebookNode.Run(Guid.NewGuid(),"Link"),NotebookNode.Break(Guid.NewGuid())])]),
            ..new[]{NotebookNodeKind.Checklist,NotebookNodeKind.BulletedList,NotebookNodeKind.NumberedList}.Select(kind=>NotebookNode.List(Guid.NewGuid(),kind,
                [NotebookNode.Item(Guid.NewGuid(),[NotebookNode.Paragraph(Guid.NewGuid(),[NotebookNode.Run(Guid.NewGuid(),"Item")])],kind==NotebookNodeKind.Checklist?true:null)])),
            NotebookNode.Image(Guid.NewGuid(),image,"Caption","Alternative")]);
        var copy=NotebookDuplication.Document(source,new Dictionary<Guid,Guid>{{image,copyImage}});
        Assert.Equal(source.PlainText,copy.PlainText);Assert.Equal(copyImage,Assert.Single(copy.ImageNodes).ImageId);
        var before=Nodes(source.Blocks).ToArray();var after=Nodes(copy.Blocks).ToArray();
        Assert.Empty(before.Select(n=>n.Id).Intersect(after.Select(n=>n.Id)));Assert.Equal(before.Length,after.Length);
        for(int i=0;i<before.Length;i++){Assert.Equal(before[i].Kind,after[i].Kind);Assert.Equal(before[i].Text,after[i].Text);Assert.Equal(before[i].Marks,after[i].Marks);Assert.Equal(before[i].IsChecked,after[i].IsChecked);Assert.Equal(before[i].Url,after[i].Url);}
    }
    [Theory]
    [InlineData("Research","Copy of Research")]
    [InlineData(" Copy ","Copy of Copy")]
    public void TitlePolicy(string source,string expected)=>Assert.Equal(expected,NotebookDuplication.Title(source));
    [Fact]
    public void LongTitleIsBoundedWithoutSplittingSurrogate()
    { var title=NotebookDuplication.Title(new string('x',191)+"😀"+new string('y',7));Assert.Equal("Copy of "+new string('x',191),title); }
    private static IEnumerable<NotebookNode> Nodes(IEnumerable<NotebookNode> roots)=>roots.SelectMany(n=>new[]{n}.Concat(Nodes(n.Children)));
}
