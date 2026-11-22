using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Desktop.Dialogs;

namespace PersonalTradingJournal.Desktop.ViewModels.Accounts;

public sealed partial class AccountsViewModel
{
    private readonly IAccountPayoutStore? _payoutStore;
    private bool _isSavingPayout;
    private string _payoutAmountText = string.Empty;
    private string? _payoutError;
    private IReadOnlyList<AccountPayout> _payouts = [];
    public string PayoutAmountText { get => _payoutAmountText; set => SetProperty(ref _payoutAmountText, value); }
    public string? PayoutError { get => _payoutError; private set => SetProperty(ref _payoutError, value); }
    public IReadOnlyList<AccountPayout> Payouts { get => _payouts; private set => SetProperty(ref _payouts, value); }
    public IAsyncRelayCommand AddPayoutCommand { get; }
    public IAsyncRelayCommand<AccountPayout> RemovePayoutCommand { get; }
    private bool CanWritePayout() => _payoutStore is not null && SelectedAccount is not null && CanUseAccountAction(SelectedAccount.Id);

    private async Task AddPayoutAsync()
    {
        if (!CanWritePayout()) return;
        if (!decimal.TryParse(PayoutAmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) || amount <= 0)
        { PayoutError = "Enter a positive payout amount."; return; }
        var account = SelectedAccount!;
        await WritePayoutAsync(account.Id, () => _payoutStore!.AddAsync(account.Id, account.Currency, amount));
    }

    private async Task RemovePayoutAsync(AccountPayout? payout)
    {
        if (!CanWritePayout() || payout is null || payout.AccountId != SelectedAccount!.Id) return;
        if (!_dialogService.Confirm(new ConfirmationDialogRequest("Remove erroneous payout?",
            $"Remove {payout.Amount:N2} {payout.Currency} recorded {payout.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC from {SelectedAccount.Name}? Current Balance will increase by this amount.",
            "Remove payout", isDestructive: true))) return;
        await WritePayoutAsync(payout.AccountId, () => _payoutStore!.RemoveAsync(payout.AccountId, payout.Id));
    }

    private async Task WritePayoutAsync(Guid accountId, Func<Task> write)
    {
        _isSavingPayout = true; NotifyOperationCanExecuteChanged(); PayoutError = null;
        bool saved = false;
        try
        {
            await write(); saved = true; PayoutAmountText = string.Empty;
            Payouts = await _payoutStore!.GetAsync(accountId);
            if (!await LoadAsync(forceRefresh: true, CancellationToken.None))
                PayoutError = "Payout saved, but balances could not be refreshed. Refresh before recording another entry.";
        }
        catch (Exception)
        {
            PayoutError = saved ? "Payout saved, but history could not be refreshed. Reopen Account details."
                : "Payout could not be saved. Check the amount and refresh Account details.";
        }
        finally { _isSavingPayout = false; NotifyOperationCanExecuteChanged(); }
    }
}
