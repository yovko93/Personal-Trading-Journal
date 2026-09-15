using PersonalTradingJournal.Desktop.Settings;

namespace PersonalTradingJournal.Desktop.Tests.TestDoubles;

public sealed class FakeDesktopSettingsStore : IDesktopSettingsStore
{
    public DesktopSettings LoadResult { get; set; } = DesktopSettings.Default;

    public Exception? SaveException { get; set; }

    public List<DesktopSettings> SavedSettings { get; } = [];

    public Task<DesktopSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(LoadResult);
    }

    public Task SaveAsync(
        DesktopSettings settings,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SavedSettings.Add(settings);
        return SaveException is null
            ? Task.CompletedTask
            : Task.FromException(SaveException);
    }
}
