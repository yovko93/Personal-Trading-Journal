using System.Globalization;
using PersonalTradingJournal.Application.Notebook;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Notebook;

namespace PersonalTradingJournal.Desktop.Tests.Notebook;

public sealed class NotebookDatePresentationTests
{
    [Theory]
    [InlineData("2024-10-24T16:00:00Z", "en-US", "Oct 24, 2024")]
    [InlineData("2024-10-04T16:00:00Z", "fr-FR", "Oct 04, 2024")]
    [InlineData("2024-10-04T16:00:00Z", "ar-SA", "Oct 04, 2024")]
    [InlineData("2024-10-05T03:59:59Z", "bg-BG", "Oct 04, 2024")]
    [InlineData("2024-10-05T04:00:00Z", "fr-FR", "Oct 05, 2024")]
    [InlineData("2025-01-01T04:59:59Z", "bg-BG", "Dec 31, 2024")]
    [InlineData("2025-01-01T05:00:00Z", "ar-SA", "Jan 01, 2025")]
    [InlineData("2024-03-01T04:59:59Z", "fr-FR", "Feb 29, 2024")]
    [InlineData("2024-03-11T03:59:59Z", "bg-BG", "Mar 10, 2024")]
    [InlineData("2024-03-11T04:00:00Z", "fr-FR", "Mar 11, 2024")]
    [InlineData("2024-11-04T04:59:59Z", "fr-FR", "Nov 03, 2024")]
    [InlineData("2024-11-04T05:00:00Z", "bg-BG", "Nov 04, 2024")]
    public void EnglishDatesFollowNewYorkBoundaryAfterConversion(string utc, string culture, string expected)
    {
        var before = CultureInfo.CurrentCulture; var beforeUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture); CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
            var timestamp = DateTimeOffset.Parse(utc, CultureInfo.InvariantCulture);
            var note = NotebookFake.Note() with { UpdatedAtUtc = timestamp };
            var row = new NotebookRow(NotebookFake.Row(note));
            Assert.Equal(expected, NotebookDatePresentation.Format(timestamp)); Assert.Equal(expected, row.Updated);
            Assert.Contains($"Updated {expected}.", row.AccessibleName); Assert.Equal(timestamp, row.Item.UpdatedAtUtc);
        }
        finally { CultureInfo.CurrentCulture = before; CultureInfo.CurrentUICulture = beforeUi; }
    }

    [Fact]
    public async Task HeaderUsesLoadedUpdatedTimestampRatherThanCreatedOrListTimestamp()
    {
        var data = new NotebookFake();
        var listed = NotebookFake.Note() with { CreatedAtUtc = new(2020, 2, 1, 12, 0, 0, TimeSpan.Zero), UpdatedAtUtc = new(2024, 10, 4, 16, 0, 0, TimeSpan.Zero) };
        var loaded = listed with { UpdatedAtUtc = new(2025, 1, 1, 5, 0, 0, TimeSpan.Zero) };
        data.Notes.Add(listed); data.OnLoad = (_, _) => Task.FromResult(new NotebookLoadResult(NotebookLoadStatus.Success, loaded));
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService()); await vm.ActivateAsync();
        Assert.Equal("", vm.Metadata); vm.SelectedNote = vm.Notes[0]; await vm.LoadTask;
        Assert.Equal("Oct 04, 2024", vm.Notes[0].Updated); Assert.Equal("Active · Updated Jan 01, 2025", vm.Metadata);
        Assert.Equal(loaded.CreatedAtUtc, vm.Note!.CreatedAtUtc); Assert.Equal(loaded.UpdatedAtUtc, vm.Note.UpdatedAtUtc);
        Assert.Equal(0, data.Writes);
    }

    [Theory]
    [InlineData(NotebookLoadStatus.InvalidContent)]
    [InlineData(NotebookLoadStatus.UnsupportedContent)]
    [InlineData(NotebookLoadStatus.TooLarge)]
    [InlineData(NotebookLoadStatus.NotFound)]
    public async Task UnavailableContentRetainsListDateAndEmptySelectionStaysEmpty(NotebookLoadStatus status)
    {
        var data = new NotebookFake(); data.Notes.Add(NotebookFake.Note() with { UpdatedAtUtc = new(2025, 1, 1, 4, 59, 59, TimeSpan.Zero) });
        data.OnLoad = (_, _) => Task.FromResult(new NotebookLoadResult(status, OriginalContent: "opaque"));
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService()); await vm.ActivateAsync();
        Assert.Equal("", vm.Metadata); vm.SelectedNote = vm.Notes[0]; await vm.LoadTask;
        Assert.Null(vm.Note); Assert.Equal("Active · Updated Dec 31, 2024", vm.Metadata); Assert.Equal(0, data.Writes);
        vm.SelectedNote = null; await vm.LoadTask; Assert.Equal("", vm.Metadata);
    }

    [Fact]
    public async Task IdenticalVisibleDatesDoNotCollapseTimestampOrderingPagingOrTitleSort()
    {
        var data = new NotebookFake(); var start = new DateTimeOffset(2025, 1, 1, 5, 0, 0, TimeSpan.Zero);
        for (int i = 0; i < 55; i++) data.Notes.Add(NotebookFake.Note($"Note {i:00}") with { UpdatedAtUtc = start.AddMinutes(i) });
        var timestamps = data.Notes.Select(n => n.UpdatedAtUtc).ToArray();
        using var vm = new NotebookWorkspaceViewModel(data, new FakeDialogService()); await vm.ActivateAsync();
        Assert.Equal(50, vm.Notes.Count); await vm.MoreNotesCommand.ExecuteAsync(null);
        Assert.Equal(data.Notes.OrderByDescending(n => n.UpdatedAtUtc).Select(n => n.Id), vm.Notes.Select(n => n.Id));
        Assert.All(vm.Notes, row => Assert.Equal("Jan 01, 2025", row.Updated));
        vm.Sort = NotebookWorkspaceViewModel.SortOptions[1]; await vm.LoadTask; await vm.MoreNotesCommand.ExecuteAsync(null);
        Assert.Equal(data.Notes.Select(n => n.Id), vm.Notes.Select(n => n.Id));
        Assert.Equal(timestamps, data.Notes.Select(n => n.UpdatedAtUtc)); Assert.Equal(0, data.Writes);
    }
}
