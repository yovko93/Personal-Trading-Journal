using Microsoft.Extensions.Logging.Abstractions;
using PersonalTradingJournal.Desktop.Settings;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Theming;
using PersonalTradingJournal.Desktop.ViewModels.Settings;

namespace PersonalTradingJournal.Desktop.Tests.Settings;

public sealed class SettingsViewModelTests
{
    [Theory]
    [InlineData(AppTheme.Dark)]
    [InlineData(AppTheme.Light)]
    public void ConstructorReflectsCurrentTheme(AppTheme theme)
    {
        var viewModel = CreateViewModel(new FakeThemeService(theme), new FakeDesktopSettingsStore());

        Assert.Equal(theme, viewModel.SelectedTheme);
        Assert.Equal(theme == AppTheme.Dark, viewModel.IsDarkSelected);
        Assert.Equal(theme == AppTheme.Light, viewModel.IsLightSelected);
    }

    [Fact]
    public void AvailableThemesContainsDarkAndLight()
    {
        SettingsViewModel viewModel = CreateViewModel(
            new FakeThemeService(),
            new FakeDesktopSettingsStore());

        Assert.Equal([AppTheme.Dark, AppTheme.Light], viewModel.AvailableThemes);
    }

    [Theory]
    [InlineData(AppTheme.Dark, AppTheme.Light)]
    [InlineData(AppTheme.Light, AppTheme.Dark)]
    public async Task SelectionAppliesAndPersistsTheme(
        AppTheme initialTheme,
        AppTheme selectedTheme)
    {
        var themeService = new FakeThemeService(initialTheme);
        var settingsStore = new FakeDesktopSettingsStore();
        SettingsViewModel viewModel = CreateViewModel(themeService, settingsStore);

        await viewModel.ChangeThemeCommand.ExecuteAsync(selectedTheme);

        Assert.Equal(selectedTheme, viewModel.SelectedTheme);
        Assert.Equal(selectedTheme, Assert.Single(themeService.AppliedThemes));
        Assert.Equal(selectedTheme, Assert.Single(settingsStore.SavedSettings).Theme);
        Assert.False(viewModel.IsSaving);
        Assert.False(viewModel.HasSaveError);
    }

    [Fact]
    public async Task PersistenceFailureLeavesThemeAppliedAndShowsSafeError()
    {
        var themeService = new FakeThemeService();
        var settingsStore = new FakeDesktopSettingsStore
        {
            SaveException = new IOException("Sensitive persistence detail"),
        };
        SettingsViewModel viewModel = CreateViewModel(themeService, settingsStore);

        await viewModel.ChangeThemeCommand.ExecuteAsync(AppTheme.Light);

        Assert.Equal(AppTheme.Light, themeService.CurrentTheme);
        Assert.Equal(AppTheme.Light, viewModel.SelectedTheme);
        Assert.True(viewModel.HasSaveError);
        Assert.DoesNotContain("Sensitive", viewModel.SaveErrorMessage, StringComparison.Ordinal);
        Assert.False(viewModel.IsSaving);
    }

    [Fact]
    public async Task SelectingCurrentThemeDoesNotApplyOrPersistAgain()
    {
        var themeService = new FakeThemeService(AppTheme.Dark);
        var settingsStore = new FakeDesktopSettingsStore();
        SettingsViewModel viewModel = CreateViewModel(themeService, settingsStore);

        await viewModel.ChangeThemeCommand.ExecuteAsync(AppTheme.Dark);

        Assert.Empty(themeService.AppliedThemes);
        Assert.Empty(settingsStore.SavedSettings);
    }

    private static SettingsViewModel CreateViewModel(
        FakeThemeService themeService,
        FakeDesktopSettingsStore settingsStore) => new(
            themeService,
            settingsStore,
            NullLogger<SettingsViewModel>.Instance);
}
