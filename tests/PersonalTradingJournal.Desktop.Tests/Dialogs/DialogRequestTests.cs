using PersonalTradingJournal.Desktop.Dialogs;

namespace PersonalTradingJournal.Desktop.Tests.Dialogs;

public sealed class DialogRequestTests
{
    [Fact]
    public void ConfirmationRequestRetainsContextAndDestructiveIntent()
    {
        var request = new ConfirmationDialogRequest(
            " Delete instrument \"MNQ\"? ",
            " This action permanently deletes the instrument. ",
            " Delete Instrument ",
            isDestructive: true);

        Assert.Equal("Delete instrument \"MNQ\"?", request.Title);
        Assert.Equal(
            "This action permanently deletes the instrument.",
            request.Message);
        Assert.Equal("Delete Instrument", request.ConfirmButtonText);
        Assert.Equal("Cancel", request.CancelButtonText);
        Assert.True(request.IsDestructive);
    }

    [Fact]
    public void InformationRequestSupportsDeleteBlockedMessage()
    {
        var request = new InformationDialogRequest(
            "Cannot delete this instrument",
            "This instrument is used by existing trades. Deactivate it instead.");

        Assert.Equal("Cannot delete this instrument", request.Title);
        Assert.Contains("Deactivate it instead", request.Message);
        Assert.Equal("Close", request.CloseButtonText);
    }

    [Theory]
    [InlineData("", "Message", "Confirm", "Cancel")]
    [InlineData("Title", " ", "Confirm", "Cancel")]
    [InlineData("Title", "Message", "", "Cancel")]
    [InlineData("Title", "Message", "Confirm", " ")]
    public void ConfirmationRequestRejectsBlankRequiredText(
        string title,
        string message,
        string confirmButtonText,
        string cancelButtonText)
    {
        Assert.Throws<ArgumentException>(() => new ConfirmationDialogRequest(
            title,
            message,
            confirmButtonText,
            cancelButtonText));
    }
}
