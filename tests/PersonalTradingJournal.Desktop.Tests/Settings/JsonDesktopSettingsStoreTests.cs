using Microsoft.Extensions.Logging.Abstractions;
using PersonalTradingJournal.Application.Common.Storage;
using PersonalTradingJournal.Desktop.Settings;
using PersonalTradingJournal.Desktop.Theming;

namespace PersonalTradingJournal.Desktop.Tests.Settings;

public sealed class JsonDesktopSettingsStoreTests
{
    [Fact]
    public async Task MissingFileReturnsDarkDefault()
    {
        using var directory = new TemporaryDirectory();
        JsonDesktopSettingsStore store = CreateStore(directory.SettingsPath);

        DesktopSettings result = await store.LoadAsync();

        Assert.Equal(AppTheme.Dark, result.Theme);
    }

    [Theory]
    [InlineData(AppTheme.Dark)]
    [InlineData(AppTheme.Light)]
    public async Task ValidThemeLoads(AppTheme theme)
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllTextAsync(
            directory.SettingsPath,
            $$"""{"Theme":"{{theme}}"}""");
        JsonDesktopSettingsStore store = CreateStore(directory.SettingsPath);

        DesktopSettings result = await store.LoadAsync();

        Assert.Equal(theme, result.Theme);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"Theme\":\"Sepia\"}")]
    [InlineData("null")]
    public async Task InvalidContentReturnsDarkFallback(string content)
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllTextAsync(directory.SettingsPath, content);
        JsonDesktopSettingsStore store = CreateStore(directory.SettingsPath);

        DesktopSettings result = await store.LoadAsync();

        Assert.Equal(AppTheme.Dark, result.Theme);
    }

    [Theory]
    [InlineData(AppTheme.Dark)]
    [InlineData(AppTheme.Light)]
    public async Task SavePersistsThemeAtConfiguredPath(AppTheme theme)
    {
        using var directory = new TemporaryDirectory("preferred-theme.json");
        JsonDesktopSettingsStore store = CreateStore(directory.SettingsPath);

        await store.SaveAsync(new DesktopSettings(theme));

        Assert.True(File.Exists(directory.SettingsPath));
        DesktopSettings reloaded = await store.LoadAsync();
        Assert.Equal(theme, reloaded.Theme);
        Assert.Empty(Directory.EnumerateFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task LoadHonorsPreCanceledToken()
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllTextAsync(directory.SettingsPath, "{\"Theme\":\"Light\"}");
        JsonDesktopSettingsStore store = CreateStore(directory.SettingsPath);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.LoadAsync(cancellation.Token));
    }

    [Fact]
    public async Task SaveHonorsPreCanceledTokenAndLeavesNoSettingsFile()
    {
        using var directory = new TemporaryDirectory();
        JsonDesktopSettingsStore store = CreateStore(directory.SettingsPath);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.SaveAsync(
                new DesktopSettings(AppTheme.Light),
                cancellation.Token));

        Assert.False(File.Exists(directory.SettingsPath));
    }

    private static JsonDesktopSettingsStore CreateStore(string settingsPath) => new(
        new TestApplicationPaths(settingsPath),
        NullLogger<JsonDesktopSettingsStore>.Instance);

    private sealed class TestApplicationPaths(string settingsPath) : IApplicationPaths
    {
        public string DataDirectory { get; } = Path.GetDirectoryName(settingsPath)!;

        public string DatabasePath => Path.Combine(DataDirectory, "journal.db");

        public string SettingsPath { get; } = settingsPath;

        public string ScreenshotsDirectory => Path.Combine(DataDirectory, "screenshots");

        public string LogsDirectory => Path.Combine(DataDirectory, "logs");

        public string BackupsDirectory => Path.Combine(DataDirectory, "backups");
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory(string fileName = "settings.json")
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                nameof(JsonDesktopSettingsStoreTests),
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
            SettingsPath = System.IO.Path.Combine(Path, fileName);
        }

        public string Path { get; }

        public string SettingsPath { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
