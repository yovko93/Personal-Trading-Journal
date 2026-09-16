using PersonalTradingJournal.Desktop.Dialogs;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

internal sealed class FakeDialogService : IDialogService
{
    public bool ConfirmationResult { get; set; }

    public ConfirmationDialogRequest? ConfirmationRequest { get; private set; }

    public InformationDialogRequest? InformationRequest { get; private set; }

    public bool Confirm(ConfirmationDialogRequest request)
    {
        ConfirmationRequest = request;
        return ConfirmationResult;
    }

    public void ShowInformation(InformationDialogRequest request)
    {
        InformationRequest = request;
    }
}
