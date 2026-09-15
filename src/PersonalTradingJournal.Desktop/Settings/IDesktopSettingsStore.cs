namespace PersonalTradingJournal.Desktop.Settings;

public interface IDesktopSettingsStore
{
    Task<DesktopSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        DesktopSettings settings,
        CancellationToken cancellationToken = default);
}
