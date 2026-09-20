namespace PersonalTradingJournal.Desktop.Dialogs;

public interface IDialogService
{
    bool Confirm(ConfirmationDialogRequest request);

    void ShowInformation(InformationDialogRequest request);
}
