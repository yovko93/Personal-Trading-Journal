using PersonalTradingJournal.Application.Accounts;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Accounts;
using PersonalTradingJournal.Domain.Accounts;

namespace PersonalTradingJournal.Desktop.Tests.Accounts;

public sealed class AccountsViewModelTests
{
    [Fact]
    public async Task EnsureLoadedAsync_AfterSuccessfulLoad_DoesNotQueryReaderAgain()
    {
        var accounts = new[] { CreateListItem(isActive: true) };
        var reader = new FakeTradingAccountReader();
        reader.EnqueueResult(accounts);
        AccountsViewModel viewModel = CreateViewModel(reader);

        await viewModel.EnsureLoadedAsync();
        await viewModel.EnsureLoadedAsync();

        Assert.Same(accounts, viewModel.Accounts);
        Assert.True(viewModel.HasAccounts);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal(1, reader.CallCount);
    }

    [Fact]
    public async Task RefreshAsync_WhenReaderFails_RetainsExistingRowsAndShowsListError()
    {
        var accounts = new[] { CreateListItem(isActive: true) };
        var reader = new FakeTradingAccountReader();
        reader.EnqueueResult(accounts);
        reader.EnqueueException(new InvalidOperationException("Read failed."));
        AccountsViewModel viewModel = CreateViewModel(reader);
        await viewModel.EnsureLoadedAsync();

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Same(accounts, viewModel.Accounts);
        Assert.Equal("Accounts could not be loaded.", viewModel.ErrorMessage);
        Assert.True(viewModel.HasError);
    }

    [Fact]
    public async Task RefreshAsync_AfterFailure_ClearsErrorAndReplacesRows()
    {
        var initialAccounts = new[] { CreateListItem(isActive: true) };
        var refreshedAccounts = new[]
        {
            CreateListItem(isActive: true),
            CreateListItem(isActive: false),
        };
        var reader = new FakeTradingAccountReader();
        reader.EnqueueResult(initialAccounts);
        reader.EnqueueException(new InvalidOperationException("Read failed."));
        reader.EnqueueResult(refreshedAccounts);
        AccountsViewModel viewModel = CreateViewModel(reader);
        await viewModel.EnsureLoadedAsync();
        await viewModel.RefreshCommand.ExecuteAsync(null);

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Same(refreshedAccounts, viewModel.Accounts);
        Assert.Null(viewModel.ErrorMessage);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task CreateAccountAsync_WhenNameIsBlank_KeepsFormOpenAndDoesNotWrite()
    {
        var store = new FakeTradingAccountStore();
        AccountsViewModel viewModel = CreateViewModel(store: store);
        viewModel.ShowCreateFormCommand.Execute(null);
        viewModel.AccountName = "   ";
        viewModel.Currency = "USD";

        await viewModel.CreateAccountCommand.ExecuteAsync(null);

        Assert.Equal("Account name is required.", viewModel.CreateErrorMessage);
        Assert.True(viewModel.IsCreateFormVisible);
        Assert.Equal(0, store.AddCallCount);
    }

    [Fact]
    public async Task CreateAccountAsync_WhenWriteFails_RetainsFormValuesAndShowsCreateError()
    {
        var store = new FakeTradingAccountStore
        {
            AddException = new InvalidOperationException("Write failed."),
        };
        AccountsViewModel viewModel = CreateViewModel(store: store);
        OpenValidCreateForm(viewModel);

        await viewModel.CreateAccountCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsCreateFormVisible);
        Assert.Equal("Primary", viewModel.AccountName);
        Assert.Equal(TradingAccountType.PropFunded, viewModel.SelectedAccountType);
        Assert.Equal("Provider", viewModel.ProviderName);
        Assert.Equal("EXT-42", viewModel.ExternalAccountId);
        Assert.Equal("USD", viewModel.Currency);
        Assert.Equal("1000", viewModel.StartingBalanceText);
        Assert.Equal("Account could not be created.", viewModel.CreateErrorMessage);
    }

    [Fact]
    public async Task CreateAccountAsync_WhenWriteAndReloadSucceed_ClosesAndResetsFormUsingReaderProjection()
    {
        var initialAccounts = new[] { CreateListItem(isActive: true) };
        var refreshedAccounts = new[]
        {
            CreateListItem(isActive: true),
            CreateListItem(isActive: false),
        };
        var reader = new FakeTradingAccountReader();
        reader.EnqueueResult(initialAccounts);
        reader.EnqueueResult(refreshedAccounts);
        var store = new FakeTradingAccountStore();
        AccountsViewModel viewModel = CreateViewModel(reader, store);
        await viewModel.EnsureLoadedAsync();
        OpenValidCreateForm(viewModel);

        await viewModel.CreateAccountCommand.ExecuteAsync(null);

        Assert.Equal(1, store.AddCallCount);
        Assert.Same(refreshedAccounts, viewModel.Accounts);
        Assert.False(viewModel.IsCreateFormVisible);
        AssertCreateFormIsReset(viewModel);
        Assert.Null(viewModel.CreateErrorMessage);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task CreateAccountAsync_WhenWriteSucceedsAndReloadFails_ClosesFormAndShowsRefreshMessage()
    {
        var initialAccounts = new[] { CreateListItem(isActive: true) };
        var reader = new FakeTradingAccountReader();
        reader.EnqueueResult(initialAccounts);
        reader.EnqueueException(new InvalidOperationException("Read failed."));
        var store = new FakeTradingAccountStore();
        AccountsViewModel viewModel = CreateViewModel(reader, store);
        await viewModel.EnsureLoadedAsync();
        OpenValidCreateForm(viewModel);

        await viewModel.CreateAccountCommand.ExecuteAsync(null);

        Assert.Equal(1, store.AddCallCount);
        Assert.False(viewModel.IsCreateFormVisible);
        AssertCreateFormIsReset(viewModel);
        Assert.Null(viewModel.CreateErrorMessage);
        Assert.Equal(
            "Account was created, but the list could not be refreshed. Refresh to see the latest data.",
            viewModel.ErrorMessage);
        Assert.Same(initialAccounts, viewModel.Accounts);
    }

    [Fact]
    public async Task DeactivateAccountAsync_WhenWriteFails_ShowsLifecycleErrorOnly()
    {
        AccountListItem activeItem = CreateListItem(isActive: true);
        var accounts = new[] { activeItem };
        var reader = new FakeTradingAccountReader();
        reader.EnqueueResult(accounts);
        var store = new FakeTradingAccountStore
        {
            AccountToReturn = CreateAggregate(activeItem.Id, isActive: true),
            UpdateException = new InvalidOperationException("Write failed."),
        };
        AccountsViewModel viewModel = CreateViewModel(reader, store);
        await viewModel.EnsureLoadedAsync();

        await viewModel.DeactivateAccountCommand.ExecuteAsync(activeItem);

        Assert.Equal("Account status could not be changed.", viewModel.LifecycleErrorMessage);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Same(accounts, viewModel.Accounts);
    }

    [Fact]
    public async Task DeactivateAccountAsync_WhenWriteSucceedsAndReloadFails_DoesNotReportLifecycleFailure()
    {
        AccountListItem activeItem = CreateListItem(isActive: true);
        var accounts = new[] { activeItem };
        var reader = new FakeTradingAccountReader();
        reader.EnqueueResult(accounts);
        reader.EnqueueException(new InvalidOperationException("Read failed."));
        var store = new FakeTradingAccountStore
        {
            AccountToReturn = CreateAggregate(activeItem.Id, isActive: true),
        };
        AccountsViewModel viewModel = CreateViewModel(reader, store);
        await viewModel.EnsureLoadedAsync();

        await viewModel.DeactivateAccountCommand.ExecuteAsync(activeItem);

        Assert.Equal(1, store.UpdateCallCount);
        Assert.False(Assert.IsType<TradingAccount>(store.UpdatedAccount).IsActive);
        Assert.Null(viewModel.LifecycleErrorMessage);
        Assert.Equal(
            "Account status changed, but the list could not be refreshed. Refresh to see the latest status.",
            viewModel.ErrorMessage);
        Assert.Same(accounts, viewModel.Accounts);
    }

    [Fact]
    public async Task DeactivateAccountAsync_WhenAccountIsMissing_ShowsNotFoundMessage()
    {
        AccountListItem activeItem = CreateListItem(isActive: true);
        var reader = new FakeTradingAccountReader();
        reader.EnqueueResult([activeItem]);
        var store = new FakeTradingAccountStore();
        AccountsViewModel viewModel = CreateViewModel(reader, store);
        await viewModel.EnsureLoadedAsync();

        await viewModel.DeactivateAccountCommand.ExecuteAsync(activeItem);

        Assert.Equal(
            "Account no longer exists. Refresh the list.",
            viewModel.LifecycleErrorMessage);
        Assert.Equal(0, store.UpdateCallCount);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public void LifecycleCommands_ReflectRowStateAndAreDisabledWhileCreateFormIsOpen()
    {
        AccountListItem activeItem = CreateListItem(isActive: true);
        AccountListItem inactiveItem = CreateListItem(isActive: false);
        AccountsViewModel viewModel = CreateViewModel();

        Assert.True(viewModel.DeactivateAccountCommand.CanExecute(activeItem));
        Assert.False(viewModel.ActivateAccountCommand.CanExecute(activeItem));
        Assert.True(viewModel.ActivateAccountCommand.CanExecute(inactiveItem));
        Assert.False(viewModel.DeactivateAccountCommand.CanExecute(inactiveItem));

        viewModel.ShowCreateFormCommand.Execute(null);

        Assert.False(viewModel.ActivateAccountCommand.CanExecute(inactiveItem));
        Assert.False(viewModel.DeactivateAccountCommand.CanExecute(activeItem));
    }

    private static AccountsViewModel CreateViewModel(
        FakeTradingAccountReader? reader = null,
        FakeTradingAccountStore? store = null)
    {
        reader ??= new FakeTradingAccountReader();
        store ??= new FakeTradingAccountStore();
        var timeProvider = new FixedTimeProvider();

        return new AccountsViewModel(
            reader,
            new CreateTradingAccountUseCase(store, timeProvider),
            new TradingAccountLifecycleUseCase(store, timeProvider));
    }

    private static void OpenValidCreateForm(AccountsViewModel viewModel)
    {
        viewModel.ShowCreateFormCommand.Execute(null);
        viewModel.AccountName = "Primary";
        viewModel.SelectedAccountType = TradingAccountType.PropFunded;
        viewModel.ProviderName = "Provider";
        viewModel.ExternalAccountId = "EXT-42";
        viewModel.Currency = "USD";
        viewModel.StartingBalanceText = "1000";
    }

    private static void AssertCreateFormIsReset(AccountsViewModel viewModel)
    {
        Assert.Equal(string.Empty, viewModel.AccountName);
        Assert.Equal(TradingAccountType.Personal, viewModel.SelectedAccountType);
        Assert.Equal(string.Empty, viewModel.ProviderName);
        Assert.Equal(string.Empty, viewModel.ExternalAccountId);
        Assert.Equal(string.Empty, viewModel.Currency);
        Assert.Equal(string.Empty, viewModel.StartingBalanceText);
    }

    private static AccountListItem CreateListItem(bool isActive)
    {
        return new AccountListItem(
            Guid.NewGuid(),
            "Primary",
            TradingAccountType.PropFunded,
            "Provider",
            "EXT-42",
            "USD",
            1000m,
            isActive);
    }

    private static TradingAccount CreateAggregate(Guid id, bool isActive)
    {
        DateTimeOffset createdAtUtc = FixedTimeProvider.FixedUtcNow.AddDays(-2);
        DateTimeOffset updatedAtUtc = FixedTimeProvider.FixedUtcNow.AddDays(-1);

        return TradingAccount.Rehydrate(
            id,
            "Primary",
            TradingAccountType.PropFunded,
            "Provider",
            "EXT-42",
            "USD",
            1000m,
            isActive,
            createdAtUtc,
            updatedAtUtc);
    }
}
