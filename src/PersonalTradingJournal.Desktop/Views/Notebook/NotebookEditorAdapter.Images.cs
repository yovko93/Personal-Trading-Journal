using System.Windows.Controls;
using System.Windows.Documents;
using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Desktop.Views.Notebook;

internal static partial class NotebookEditorAdapter
{
    internal static void InsertImage(RichTextBox editor, NotebookNode image, TextPointer start, TextPointer end,
        Action<NotebookDocument> validate, Func<NotebookNode, BlockUIContainer>? factory = null, bool trialOnly = false)
    {
        // Trial uses the exact text-container shape; no selection replacement before full admission.
        var copy = new FlowDocument { FontSize = editor.Document.FontSize };
        foreach (var block in editor.Document.Blocks) copy.Blocks.Add(CloneBlock(block));
        var trial = new RichTextBox { Document = copy };
        int a = editor.Document.ContentStart.GetOffsetToPosition(start), b = editor.Document.ContentStart.GetOffsetToPosition(end);
        trial.Selection.Select(copy.ContentStart.GetPositionAtOffset(a)!, copy.ContentStart.GetPositionAtOffset(b)!);
        InsertCore(trial, image, null); validate(Read(copy));
        if (trialOnly) return;
        editor.Selection.Select(start, end); InsertCore(editor, image, factory);
    }
    private static void InsertCore(RichTextBox editor, NotebookNode image, Func<NotebookNode, BlockUIContainer>? factory)
    {
        editor.BeginChange();
        try
        {
            if (!editor.Selection.IsEmpty) editor.Selection.Text = "";
            var caret = editor.CaretPosition;
            // A caret after a final image has no paragraph. Give the insertion its own
            // editable paragraph rather than rejecting a consecutive paste at that boundary.
            var paragraph = caret.Paragraph;
            if (paragraph is null)
            {
                var parentItem = Ancestor<ListItem>(caret);
                if (parentItem is null && Ancestor<System.Windows.Documents.List>(caret) is not null)
                    throw new ArgumentException("Place the caret inside a list item to insert an image.");
                var owner = parentItem?.Blocks ?? editor.Document.Blocks;
                var next = owner.FirstOrDefault(b => b.ElementStart.CompareTo(caret) >= 0);
                paragraph = new Paragraph();
                if (next is null) owner.Add(paragraph); else owner.InsertBefore(next, paragraph);
                caret = paragraph.ContentStart;
            }
            bool before = new TextRange(paragraph.ContentStart, caret).Text.Length == 0;
            bool after = new TextRange(caret, paragraph.ContentEnd).Text.Length == 0;
            if (!before && !after)
            {
                paragraph = caret.InsertParagraphBreak().Paragraph!; before = true;
            }
            var blocks = paragraph.Parent is FlowDocument doc ? doc.Blocks : paragraph.Parent is ListItem item ? item.Blocks
                : throw new ArgumentException("Unsupported image placement.");
            var block = (BlockUIContainer)CreateBlock(image, factory);
            if (before) blocks.InsertBefore(paragraph, block); else blocks.InsertAfter(paragraph, block);
            if (block.NextBlock is not Paragraph following)
            { following = new Paragraph(); blocks.InsertAfter(block, following); }
            editor.CaretPosition = following.ContentStart;
        }
        finally { editor.EndChange(); }
    }
    internal static IEnumerable<BlockUIContainer> Images(FlowDocument document) => Images(document.Blocks);
    private static IEnumerable<BlockUIContainer> Images(BlockCollection blocks)
    {
        foreach (var block in blocks)
        {
            if (block is BlockUIContainer { Tag: NotebookNode { Kind: NotebookNodeKind.Image } } image) yield return image;
            if (block is System.Windows.Documents.List list)
                foreach (var item in list.ListItems)
                    foreach (var child in Images(item.Blocks)) yield return child;
        }
    }
    private static BlockCollection Owner(Block block) => block.Parent is FlowDocument doc ? doc.Blocks :
        block.Parent is ListItem item ? item.Blocks : throw new ArgumentException("This image is no longer in the document.");
    internal static void RemoveImage(RichTextBox editor, BlockUIContainer block)
    {
        editor.BeginChange();
        try { Owner(block).Remove(block); }
        finally { editor.EndChange(); }
    }
    internal static void CaptionImage(RichTextBox editor, BlockUIContainer block, string caption, string alternative,
        Func<NotebookNode, BlockUIContainer>? factory = null)
    {
        var old = (NotebookNode)block.Tag;
        var replacement = NotebookNode.Image(old.Id, old.ImageId!.Value, caption, alternative);
        editor.BeginChange();
        try { var blocks = Owner(block); blocks.InsertBefore(block, CreateBlock(replacement, factory)); blocks.Remove(block); }
        finally { editor.EndChange(); }
    }
    internal static void MoveImage(RichTextBox editor, BlockUIContainer block, bool earlier)
    {
        var sibling = earlier ? block.PreviousBlock : block.NextBlock;
        if (sibling is null) return;
        editor.BeginChange();
        try
        {
            var owner = Owner(block); owner.Remove(block);
            if (earlier) owner.InsertBefore(sibling, block); else owner.InsertAfter(sibling, block);
        }
        finally { editor.EndChange(); }
    }
}
