using System.Windows;
using PersonalTradingJournal.Desktop.Theming;

namespace PersonalTradingJournal.Desktop.Tests.Theming;

public sealed class ThemeServiceTests
{
    [Fact]
    public void ConstructorDefaultsToDarkAndAddsOneThemeDictionary()
    {
        var resources = new ResourceDictionary();

        ThemeService service = CreateService(resources);

        Assert.Equal(AppTheme.Dark, service.CurrentTheme);
        Assert.Single(resources.MergedDictionaries);
    }

    [Theory]
    [InlineData(AppTheme.Dark)]
    [InlineData(AppTheme.Light)]
    public void ApplyThemeSetsRequestedTheme(AppTheme theme)
    {
        var resources = new ResourceDictionary();
        ThemeService service = CreateService(resources);

        service.ApplyTheme(theme);

        Assert.Equal(theme, service.CurrentTheme);
        Assert.Single(resources.MergedDictionaries);
    }

    [Fact]
    public void SwitchingThemeReplacesPreviousDictionary()
    {
        var resources = new ResourceDictionary();
        ThemeService service = CreateService(resources);
        ResourceDictionary darkDictionary = Assert.Single(resources.MergedDictionaries);

        service.ApplyTheme(AppTheme.Light);

        ResourceDictionary lightDictionary = Assert.Single(resources.MergedDictionaries);
        Assert.NotSame(darkDictionary, lightDictionary);
    }

    [Fact]
    public void RepeatedApplyDoesNotDuplicateOrReplaceCurrentDictionary()
    {
        var resources = new ResourceDictionary();
        ThemeService service = CreateService(resources);
        service.ApplyTheme(AppTheme.Light);
        ResourceDictionary original = Assert.Single(resources.MergedDictionaries);

        service.ApplyTheme(AppTheme.Light);

        Assert.Same(original, Assert.Single(resources.MergedDictionaries));
    }

    [Fact]
    public void SwitchingThemePreservesSharedDictionariesAndOrdering()
    {
        var resources = new ResourceDictionary();
        ThemeService service = CreateService(resources);
        var shared = new ResourceDictionary { ["SharedSentinel"] = "present" };
        resources.MergedDictionaries.Add(shared);

        service.ApplyTheme(AppTheme.Light);

        Assert.Same(shared, resources.MergedDictionaries[1]);
        Assert.Equal("present", shared["SharedSentinel"]);
        Assert.Equal(2, resources.MergedDictionaries.Count);
    }

    [Fact]
    public void ApplyThemeRejectsUnknownValue()
    {
        var resources = new ResourceDictionary();
        ThemeService service = CreateService(resources);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => service.ApplyTheme((AppTheme)999));
    }

    private static ThemeService CreateService(ResourceDictionary resources) =>
        new(resources, _ => new ResourceDictionary());
}
