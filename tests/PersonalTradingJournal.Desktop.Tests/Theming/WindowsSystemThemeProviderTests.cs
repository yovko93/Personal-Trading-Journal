using Microsoft.Extensions.Logging.Abstractions;
using PersonalTradingJournal.Desktop.Theming;

namespace PersonalTradingJournal.Desktop.Tests.Theming;

public sealed class WindowsSystemThemeProviderTests
{
    [Theory]
    [InlineData(0, AppTheme.Dark)]
    [InlineData(1, AppTheme.Light)]
    public void RegistryValueMapsToConcreteTheme(int registryValue, AppTheme expected)
    {
        using var provider = CreateProvider(() => registryValue);

        Assert.Equal(expected, provider.GetCurrentTheme());
    }

    [Fact]
    public void MissingRegistryValueFallsBackToDark()
    {
        using var provider = CreateProvider(() => null);

        Assert.Equal(AppTheme.Dark, provider.GetCurrentTheme());
    }

    [Theory]
    [InlineData(2)]
    [InlineData(-1)]
    [InlineData("Light")]
    public void InvalidRegistryValueFallsBackToDark(object registryValue)
    {
        using var provider = CreateProvider(() => registryValue);

        Assert.Equal(AppTheme.Dark, provider.GetCurrentTheme());
    }

    [Fact]
    public void RegistryReadFailureFallsBackToDark()
    {
        using var provider = CreateProvider(
            () => throw new InvalidOperationException("Registry unavailable"));

        Assert.Equal(AppTheme.Dark, provider.GetCurrentTheme());
    }

    [Fact]
    public void RefreshRaisesOnlyWhenConcreteThemeChanges()
    {
        object value = 0;
        using var provider = CreateProvider(() => value);
        int notificationCount = 0;
        provider.SystemThemeChanged += (_, _) => notificationCount++;

        provider.RefreshForTesting();
        value = 1;
        provider.RefreshForTesting();
        provider.RefreshForTesting();

        Assert.Equal(1, notificationCount);
        Assert.Equal(AppTheme.Light, provider.GetCurrentTheme());
    }

    private static WindowsSystemThemeProvider CreateProvider(Func<object?> reader) => new(
        reader,
        NullLogger<WindowsSystemThemeProvider>.Instance);
}
