using Microsoft.Extensions.Logging.Abstractions;
using PersonalTradingJournal.Desktop.Settings;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Theming;
using PersonalTradingJournal.Desktop.ViewModels.Settings;

namespace PersonalTradingJournal.Desktop.Tests.Settings;

public sealed class SettingsViewModelTests
{
    [Theory]
    [InlineData(AppTheme.System, AppTheme.Dark)]
    [InlineData(AppTheme.System, AppTheme.Light)]
    [InlineData(AppTheme.Dark, AppTheme.Dark)]
    [InlineData(AppTheme.Light, AppTheme.Light)]
    public void ConstructorReflectsPreferredRatherThanEffectiveTheme(
        AppTheme preferredTheme,
        AppTheme effectiveTheme)
    {
        using var viewModel = CreateViewModel(
            new FakeThemeService(preferredTheme, effectiveTheme),
            new FakeDesktopSettingsStore());

        Assert.Equal(preferredTheme, viewModel.SelectedTheme);
        Assert.Equal(preferredTheme == AppTheme.System, viewModel.IsSystemSelected);
        Assert.Equal(preferredTheme == AppTheme.Dark, viewModel.IsDarkSelected);
        Assert.Equal(preferredTheme == AppTheme.Light, viewModel.IsLightSelected);
    }

    [Fact]
    public void AvailableThemesContainsSystemDarkAndLight()
    {
        using SettingsViewModel viewModel = CreateViewModel(
            new FakeThemeService(),
            new FakeDesktopSettingsStore());

        Assert.Equal(
            [AppTheme.System, AppTheme.Dark, AppTheme.Light],
            viewModel.AvailableThemes);
    }

    [Theory]
    [InlineData(AppTheme.System, AppTheme.Dark)]
    [InlineData(AppTheme.Dark, AppTheme.Light)]
    [InlineData(AppTheme.Light, AppTheme.System)]
    public async Task SelectionAppliesAndPersistsPreferredTheme(
        AppTheme initialTheme,
        AppTheme selectedTheme)
    {
        var themeService = new FakeThemeService(initialTheme);
        var settingsStore = new FakeDesktopSettingsStore();
        using SettingsViewModel viewModel = CreateViewModel(themeService, settingsStore);

        await viewModel.ChangeThemeCommand.ExecuteAsync(selectedTheme);

        Assert.Equal(selectedTheme, viewModel.SelectedTheme);
        Assert.Equal(selectedTheme, Assert.Single(themeService.SetPreferences));
        Assert.Equal(selectedTheme, Assert.Single(settingsStore.SavedSettings).Theme);
        Assert.False(viewModel.IsSaving);
        Assert.False(viewModel.HasSaveError);
    }

    [Fact]
    public void ExternalPreferredThemeChangeUpdatesSelection()
    {
        var themeService = new FakeThemeService(AppTheme.System, AppTheme.Dark);
        using SettingsViewModel viewModel = CreateViewModel(
            themeService,
            new FakeDesktopSettingsStore());

        themeService.SetPreferredTheme(AppTheme.Light);

        Assert.Equal(AppTheme.Light, viewModel.SelectedTheme);
        Assert.True(viewModel.IsLightSelected);
    }

    [Fact]
    public void SystemEffectiveThemeChangeKeepsSystemSelected()
    {
        var themeService = new FakeThemeService(AppTheme.System, AppTheme.Dark);
        using SettingsViewModel viewModel = CreateViewModel(
            themeService,
            new FakeDesktopSettingsStore());

        themeService.SimulateSystemThemeChange(AppTheme.Light);

        Assert.Equal(AppTheme.System, viewModel.SelectedTheme);
        Assert.True(viewModel.IsSystemSelected);
    }

    [Fact]
    public async Task PersistenceFailureLeavesThemeAppliedAndShowsSafeError()
    {
        var themeService = new FakeThemeService();
        var settingsStore = new FakeDesktopSettingsStore
        {
            SaveException = new IOException("Sensitive persistence detail"),
        };
        using SettingsViewModel viewModel = CreateViewModel(themeService, settingsStore);

        await viewModel.ChangeThemeCommand.ExecuteAsync(AppTheme.Light);

        Assert.Equal(AppTheme.Light, themeService.EffectiveTheme);
        Assert.Equal(AppTheme.Light, viewModel.SelectedTheme);
        Assert.True(viewModel.HasSaveError);
        Assert.DoesNotContain("Sensitive", viewModel.SaveErrorMessage, StringComparison.Ordinal);
        Assert.False(viewModel.IsSaving);
    }

    [Fact]
    public async Task SelectingCurrentPreferenceDoesNotApplyOrPersistAgain()
    {
        var themeService = new FakeThemeService(AppTheme.System, AppTheme.Dark);
        var settingsStore = new FakeDesktopSettingsStore();
        using SettingsViewModel viewModel = CreateViewModel(themeService, settingsStore);

        await viewModel.ChangeThemeCommand.ExecuteAsync(AppTheme.System);

        Assert.Empty(themeService.SetPreferences);
        Assert.Empty(settingsStore.SavedSettings);
    }

    [Fact]
    public void DisposeUnsubscribesFromThemeService()
    {
        var themeService = new FakeThemeService();
        var viewModel = CreateViewModel(themeService, new FakeDesktopSettingsStore());
        Assert.Equal(1, themeService.SubscriberCount);

        viewModel.Dispose();

        Assert.Equal(0, themeService.SubscriberCount);
    }

    private static SettingsViewModel CreateViewModel(
        FakeThemeService themeService,
        FakeDesktopSettingsStore settingsStore) => new(
            themeService,
            settingsStore,
            NullLogger<SettingsViewModel>.Instance);
}
