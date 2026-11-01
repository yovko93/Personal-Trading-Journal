using PersonalTradingJournal.Domain.Notebook;

namespace PersonalTradingJournal.Domain.Tests.Notebook;

public sealed class NotebookNoteTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    [Fact]
    public void EditorDraftValidatesEverythingBeforeAdvancingOneVersion()
    {
        var note = new NotebookNote(Now);
        var body = new NotebookDocument([NotebookNode.Paragraph(Guid.NewGuid(), [NotebookNode.Run(Guid.NewGuid(), "Retained body")])]);
        Assert.ThrowsAny<ArgumentException>(() => note.SaveDraft(" ", body, [], Now.AddSeconds(1)));
        Assert.Equal("Untitled", note.Title); Assert.Empty(note.Document.Blocks); Assert.Equal(1, note.Version);
        var missingImage = new NotebookDocument([NotebookNode.Image(Guid.NewGuid(), Guid.NewGuid())]);
        Assert.ThrowsAny<ArgumentException>(() => note.SaveDraft("Valid title", missingImage, [], Now.AddSeconds(1)));
        Assert.Equal("Untitled", note.Title); Assert.Equal(1, note.Version);
        note.SaveDraft(" Research ", body, [], Now.AddSeconds(1));
        Assert.Equal("Research", note.Title); Assert.Same(body, note.Document); Assert.Equal(2, note.Version);
        Assert.Equal("Retained body\n", note.Document.PlainText);
    }

    [Fact]
    public void NewNoteIsUntitledActiveUnfiledAndIndependentlyIdentified()
    {
        var note = new NotebookNote(Now);
        Assert.Equal("Untitled", note.Title); Assert.Null(note.FolderId);
        Assert.Equal(NotebookNoteState.Active, note.State); Assert.False(note.IsPinned);
        Assert.Empty(note.Document.PlainText); Assert.Equal(1, note.Version);
        Assert.Equal(Now, note.CreatedAtUtc); Assert.Equal(Now, note.UpdatedAtUtc);
        Assert.NotEqual(note.Id, new NotebookNote(Now).Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t")]
    [InlineData("\0")]
    public void InvalidTitleDoesNotMutateNote(string value)
    {
        var note = new NotebookNote(Now);
        Assert.ThrowsAny<ArgumentException>(() => note.UpdateTitle(value, Now.AddSeconds(1)));
        Assert.Equal("Untitled", note.Title); Assert.Equal(1, note.Version); Assert.Equal(Now, note.UpdatedAtUtc);
    }

    [Fact]
    public void TitleBoundIsAfterTrimmingAndMalformedUnicodeIsRejected()
    {
        var note = new NotebookNote(Now, title: " " + new string('a', 200) + " ");
        Assert.Equal(200, note.Title.Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => note.UpdateTitle(new string('a', 201), Now));
        Assert.Throws<ArgumentException>(() => note.UpdateTitle("\ud800", Now));
        Assert.Throws<ArgumentException>(() => note.UpdateTitle("\udc00", Now));
        note.UpdateTitle("Research \U0001F4D3", Now);
        Assert.Equal("Research \U0001F4D3", note.Title);
    }

    [Fact]
    public void CatalogNamesNormalizeCanonicalUnicodeAndCompareWithoutCase()
    {
        var folder = new NotebookFolder("  Cafe\u0301  ", Now);
        var tag = new NotebookTag(" CAFÉ ", Now);
        Assert.Equal("Café", folder.Name);
        Assert.True(NotebookFolder.NameComparer.Equals(folder.Name, tag.Name));
        Assert.True(NotebookTag.NameComparer.Equals("NQ", "nq"));
        folder.Rename("café", Now.AddSeconds(1)); Assert.Equal(2, folder.Version);
        Assert.Equal("café", folder.Name);
        Assert.Throws<ArgumentException>(() => new NotebookTag("\ud800", Now));
        Assert.Throws<ArgumentException>(() => new NotebookFolder("  ", Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NotebookFolder(new string('x', 129), Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NotebookTag(new string('x', 65), Now));
        Assert.Equal(128, new NotebookFolder(new string('x', 128), Now).Name.Length);
        Assert.Equal(64, new NotebookTag(new string('x', 64), Now).Name.Length);
    }

    [Fact]
    public void AuditFailureAndVersionOverflowCannotPartiallyApplyAnEdit()
    {
        Assert.Throws<ArgumentException>(() => new NotebookNote(Now.ToOffset(TimeSpan.FromHours(1))));
        var note = new NotebookNote(Now);
        note.SetPinned(true, Now.AddMinutes(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => note.UpdateTitle("Earlier", Now));
        Assert.Equal("Untitled", note.Title); Assert.Equal(2, note.Version);
        var exhausted = Rehydrate(version: long.MaxValue);
        Assert.Throws<OverflowException>(() => exhausted.UpdateTitle("Overflow", Now));
        Assert.Equal("Untitled", exhausted.Title);
        var folder = new NotebookFolder("Before", Now);
        Assert.Throws<ArgumentOutOfRangeException>(() => folder.Rename("After", Now.AddTicks(-1)));
        Assert.Equal("Before", folder.Name); Assert.Equal(1, folder.Version);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void TrashRestoresPriorStateAndFolderWithoutLosingAssignments(bool archived, bool folderExists)
    {
        var folder = Guid.NewGuid(); var tag = Guid.NewGuid(); var target = Guid.NewGuid();
        var note = new NotebookNote(Now, folder);
        note.SetPinned(true, Now); note.AssignTag(tag, Now);
        note.AddLink(new(NotebookTargetKind.Trade, target, "Synthetic Trade"), Now);
        var image = NotebookImage.Owned(Guid.NewGuid(), 0, "note.png", 10, new string('a', 64), "Observation");
        var document = new NotebookDocument([NotebookNode.Image(Guid.NewGuid(), image.Id, image.Caption)]);
        note.UpdateContent(document, [image], Now);
        if (archived) note.Archive(Now);
        note.Trash(Now);
        Assert.True(note.CanPermanentlyDelete);
        Assert.Equal(folder, note.RestorePoint!.FolderId);
        Assert.Equal(archived ? NotebookNoteState.Archived : NotebookNoteState.Active, note.RestorePoint.PreviousState);
        note.Restore(folderExists, Now.AddMinutes(1));
        Assert.Equal(archived ? NotebookNoteState.Archived : NotebookNoteState.Active, note.State);
        Assert.Equal(folderExists ? folder : (Guid?)null, note.FolderId);
        Assert.Null(note.RestorePoint); Assert.False(note.CanPermanentlyDelete);
        Assert.Equal(tag, Assert.Single(note.TagIds)); Assert.Equal(target, Assert.Single(note.Links).TargetId);
        Assert.Same(document, note.Document); Assert.Equal(image, Assert.Single(note.Images)); Assert.True(note.IsPinned);
    }

    [Theory]
    [InlineData(NotebookNoteState.Archived)]
    [InlineData(NotebookNoteState.Trashed)]
    public void ReadOnlyStatesRejectEveryOrdinaryEditingPath(NotebookNoteState state)
    {
        var note = new NotebookNote(Now);
        if (state == NotebookNoteState.Archived) note.Archive(Now); else note.Trash(Now);
        var version = note.Version;
        Action[] edits = [
            () => note.UpdateTitle("changed", Now),
            () => note.UpdateContent(NotebookDocument.Empty, [], Now),
            () => note.Move(Guid.NewGuid(), Now), () => note.SetPinned(true, Now),
            () => note.AssignTag(Guid.NewGuid(), Now), () => note.RemoveTag(Guid.NewGuid(), Now),
            () => note.AddLink(new(NotebookTargetKind.Trade, Guid.NewGuid(), "Trade"), Now),
            () => note.RemoveLink(NotebookTargetKind.TradingSetup, Guid.NewGuid(), Now)
        ];
        foreach (var edit in edits) Assert.Throws<InvalidOperationException>(edit);
        Assert.Equal(version, note.Version); Assert.Equal("Untitled", note.Title);
    }

    [Fact]
    public void InvalidTransitionsDoNotChangeStateAndUnarchiveEnablesEditing()
    {
        var note = new NotebookNote(Now);
        Assert.Throws<InvalidOperationException>(() => note.Restore(true, Now));
        Assert.Throws<InvalidOperationException>(() => note.Unarchive(Now));
        note.Archive(Now);
        Assert.Throws<InvalidOperationException>(() => note.Archive(Now));
        note.Unarchive(Now); note.UpdateTitle("Editable again", Now);
        note.Trash(Now);
        Assert.Throws<InvalidOperationException>(() => note.Trash(Now));
        Assert.Throws<InvalidOperationException>(() => note.Unarchive(Now));
        Assert.Equal("Editable again", note.Title);
    }

    [Fact]
    public void CatalogDeletionClearsTrashedRestorationReferencesButNotContentOrLinks()
    {
        var folder = Guid.NewGuid(); var tag = Guid.NewGuid();
        var note = new NotebookNote(Now, folder);
        note.AssignTag(tag, Now); note.AddLink(new(NotebookTargetKind.TradingSetup, Guid.NewGuid(), "Setup"), Now);
        note.Archive(Now); note.Trash(Now);
        var version = note.Version;
        note.ApplyFolderDeletion(folder, Now); note.ApplyTagDeletion(tag, Now);
        Assert.Null(note.FolderId); Assert.Null(note.RestorePoint!.FolderId);
        Assert.Empty(note.TagIds); Assert.Single(note.Links); Assert.Equal(version + 2, note.Version);
        note.Restore(true, Now); Assert.Equal(NotebookNoteState.Archived, note.State); Assert.Null(note.FolderId);
    }

    [Fact]
    public void TagsAndTypedLinksRejectDuplicatesEvenWithDifferentLabels()
    {
        var note = new NotebookNote(Now); var id = Guid.NewGuid();
        note.AssignTag(id, Now);
        Assert.Throws<ArgumentException>(() => note.AssignTag(id, Now));
        note.AddLink(new(NotebookTargetKind.Trade, id, "First label"), Now);
        Assert.Throws<ArgumentException>(() => note.AddLink(new(NotebookTargetKind.Trade, id, "Renamed"), Now));
        note.AddLink(new(NotebookTargetKind.TradingSetup, id, "Different typed target"), Now);
        note.RemoveLink(NotebookTargetKind.Trade, id, Now);
        Assert.Equal(NotebookTargetKind.TradingSetup, Assert.Single(note.Links).Kind);
        Assert.Throws<ArgumentOutOfRangeException>(() => new NotebookLink((NotebookTargetKind)3, id, "Unsupported"));
        Assert.Throws<ArgumentException>(() => new NotebookLink(NotebookTargetKind.Trade, Guid.Empty, "Empty"));
    }

    [Fact]
    public void AssignmentBoundsAndReadOnlyCollectionViewsPreventBypass()
    {
        var note = new NotebookNote(Now);
        for (int i = 0; i < 32; i++) note.AssignTag(Guid.NewGuid(), Now);
        Assert.Throws<ArgumentException>(() => note.AssignTag(Guid.NewGuid(), Now));
        for (int i = 0; i < 100; i++) note.AddLink(new(NotebookTargetKind.Trade, Guid.NewGuid(), "Trade"), Now);
        Assert.Throws<ArgumentException>(() => note.AddLink(new(NotebookTargetKind.Trade, Guid.NewGuid(), "Trade"), Now));
        Assert.Throws<NotSupportedException>(() => ((IList<Guid>)note.TagIds).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<NotebookLink>)note.Links).Clear());
        var oldTags = note.TagIds; note.RemoveTag(note.TagIds[0], Now);
        Assert.Equal(32, oldTags.Count); Assert.Equal(31, note.TagIds.Count);
    }

    [Fact]
    public void RehydrationRejectsImpossibleRestorationAndAuditState()
    {
        Assert.Throws<ArgumentException>(() => Rehydrate(state: NotebookNoteState.Trashed));
        Assert.Throws<ArgumentException>(() => Rehydrate(point: new(NotebookNoteState.Active, null)));
        Assert.Throws<ArgumentException>(() => Rehydrate(state: NotebookNoteState.Trashed, point: new(NotebookNoteState.Active, Guid.NewGuid())));
        Assert.Throws<ArgumentOutOfRangeException>(() => Rehydrate(version: 0));
        Assert.Throws<ArgumentException>(() => new NotebookNote(Now, Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => NotebookFolder.Rehydrate(Guid.NewGuid(), "Folder", 1, Now, Now.AddTicks(-1)));
    }

    private static NotebookNote Rehydrate(long version = 1, NotebookNoteState state = NotebookNoteState.Active, NotebookRestorePoint? point = null) =>
        NotebookNote.Rehydrate(Guid.NewGuid(), "Untitled", NotebookDocument.Empty, null, state, false, point, [], [], [], version, Now, Now);
}
