using System.Windows;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.Theming;

namespace PersonalTradingJournal.Desktop.Tests.Theming;

public sealed class ThemeServiceTests
{
    [Theory]
    [InlineData(AppTheme.Dark)]
    [InlineData(AppTheme.Light)]
    public void ConstructorDefaultsToSystemAndResolvesEffectiveTheme(AppTheme systemTheme)
    {
        var resources = new ResourceDictionary();
        var provider = new FakeSystemThemeProvider(systemTheme);

        using ThemeService service = CreateService(resources, provider);

        Assert.Equal(AppTheme.System, service.PreferredTheme);
        Assert.Equal(systemTheme, service.EffectiveTheme);
        Assert.Single(resources.MergedDictionaries);
        Assert.Equal(1, provider.SubscriberCount);
    }

    [Theory]
    [InlineData(AppTheme.Dark)]
    [InlineData(AppTheme.Light)]
    public void ExplicitPreferenceOverridesSystemTheme(AppTheme theme)
    {
        var resources = new ResourceDictionary();
        var provider = new FakeSystemThemeProvider(
            theme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);
        using ThemeService service = CreateService(resources, provider);

        service.SetPreferredTheme(theme);

        Assert.Equal(theme, service.PreferredTheme);
        Assert.Equal(theme, service.EffectiveTheme);
        Assert.Single(resources.MergedDictionaries);
    }

    [Fact]
    public void SystemPreferenceRespondsToSystemThemeChanges()
    {
        var resources = new ResourceDictionary();
        var provider = new FakeSystemThemeProvider(AppTheme.Dark);
        using ThemeService service = CreateService(resources, provider);
        var changes = new List<ThemeChangedEventArgs>();
        service.ThemeChanged += (_, change) => changes.Add(change);

        provider.SetTheme(AppTheme.Light);

        Assert.Equal(AppTheme.System, service.PreferredTheme);
        Assert.Equal(AppTheme.Light, service.EffectiveTheme);
        ThemeChangedEventArgs change = Assert.Single(changes);
        Assert.Equal(AppTheme.System, change.PreferredTheme);
        Assert.Equal(AppTheme.Light, change.EffectiveTheme);
    }

    [Theory]
    [InlineData(AppTheme.Dark, AppTheme.Light)]
    [InlineData(AppTheme.Light, AppTheme.Dark)]
    public void ExplicitPreferenceIgnoresSystemThemeChanges(
        AppTheme explicitTheme,
        AppTheme changedSystemTheme)
    {
        var resources = new ResourceDictionary();
        var provider = new FakeSystemThemeProvider(explicitTheme);
        using ThemeService service = CreateService(resources, provider);
        service.SetPreferredTheme(explicitTheme);
        var changes = new List<ThemeChangedEventArgs>();
        service.ThemeChanged += (_, change) => changes.Add(change);

        provider.SetTheme(changedSystemTheme);

        Assert.Equal(explicitTheme, service.PreferredTheme);
        Assert.Equal(explicitTheme, service.EffectiveTheme);
        Assert.Empty(changes);
    }

    [Fact]
    public void ReturningToSystemResolvesCurrentSystemThemeImmediately()
    {
        var resources = new ResourceDictionary();
        var provider = new FakeSystemThemeProvider(AppTheme.Dark);
        using ThemeService service = CreateService(resources, provider);
        service.SetPreferredTheme(AppTheme.Light);
        provider.SetTheme(AppTheme.Dark);

        service.SetPreferredTheme(AppTheme.System);

        Assert.Equal(AppTheme.System, service.PreferredTheme);
        Assert.Equal(AppTheme.Dark, service.EffectiveTheme);
    }

    [Fact]
    public void SwitchingThemeReplacesPreviousDictionaryWithoutDuplicates()
    {
        var resources = new ResourceDictionary();
        var provider = new FakeSystemThemeProvider(AppTheme.Dark);
        using ThemeService service = CreateService(resources, provider);
        ResourceDictionary darkDictionary = Assert.Single(resources.MergedDictionaries);

        service.SetPreferredTheme(AppTheme.Light);

        ResourceDictionary lightDictionary = Assert.Single(resources.MergedDictionaries);
        Assert.NotSame(darkDictionary, lightDictionary);

        service.SetPreferredTheme(AppTheme.Light);

        Assert.Same(lightDictionary, Assert.Single(resources.MergedDictionaries));
    }

    [Fact]
    public void SwitchingThemePreservesSharedDictionariesAndOrdering()
    {
        var resources = new ResourceDictionary();
        var shared = new ResourceDictionary { ["SharedSentinel"] = "present" };
        resources.MergedDictionaries.Add(shared);
        var provider = new FakeSystemThemeProvider(AppTheme.Dark);
        using ThemeService service = CreateService(resources, provider);

        service.SetPreferredTheme(AppTheme.Light);

        Assert.Same(shared, resources.MergedDictionaries[1]);
        Assert.Equal("present", shared["SharedSentinel"]);
        Assert.Equal(2, resources.MergedDictionaries.Count);
    }

    [Fact]
    public void ReapplyingUnchangedPreferenceDoesNotRaiseDuplicateNotification()
    {
        var resources = new ResourceDictionary();
        var provider = new FakeSystemThemeProvider(AppTheme.Dark);
        using ThemeService service = CreateService(resources, provider);
        int notificationCount = 0;
        service.ThemeChanged += (_, _) => notificationCount++;

        service.SetPreferredTheme(AppTheme.System);

        Assert.Equal(0, notificationCount);
        Assert.Equal(1, provider.SubscriberCount);
    }

    [Fact]
    public void DisposeUnsubscribesFromSystemThemeProvider()
    {
        var resources = new ResourceDictionary();
        var provider = new FakeSystemThemeProvider(AppTheme.Dark);
        ThemeService service = CreateService(resources, provider);

        service.Dispose();

        Assert.Equal(0, provider.SubscriberCount);
    }

    [Fact]
    public void SetPreferredThemeRejectsUnknownValue()
    {
        var resources = new ResourceDictionary();
        var provider = new FakeSystemThemeProvider(AppTheme.Dark);
        using ThemeService service = CreateService(resources, provider);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => service.SetPreferredTheme((AppTheme)999));
    }

    private static ThemeService CreateService(
        ResourceDictionary resources,
        ISystemThemeProvider provider) => new(
            resources,
            provider,
            _ => new ResourceDictionary(),
            action => action());
}
