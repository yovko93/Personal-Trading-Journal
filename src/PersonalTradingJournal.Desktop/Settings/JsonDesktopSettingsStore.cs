using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PersonalTradingJournal.Application.Common.Storage;

namespace PersonalTradingJournal.Desktop.Settings;

public sealed class JsonDesktopSettingsStore : IDesktopSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger<JsonDesktopSettingsStore> _logger;

    public JsonDesktopSettingsStore(
        IApplicationPaths applicationPaths,
        ILogger<JsonDesktopSettingsStore> logger)
    {
        ArgumentNullException.ThrowIfNull(applicationPaths);
        ArgumentNullException.ThrowIfNull(logger);

        _applicationPaths = applicationPaths;
        _logger = logger;
    }

    public async Task<DesktopSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_applicationPaths.SettingsPath))
        {
            return DesktopSettings.Default;
        }

        try
        {
            await using FileStream stream = new(
                _applicationPaths.SettingsPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            DesktopSettings? settings = await JsonSerializer.DeserializeAsync<DesktopSettings>(
                stream,
                SerializerOptions,
                cancellationToken);

            if (settings is null || !Enum.IsDefined(settings.Theme))
            {
                _logger.LogWarning("Desktop settings are invalid; using the Dark theme fallback");
                return DesktopSettings.Default;
            }

            return settings;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException
            or NotSupportedException
            or IOException
            or UnauthorizedAccessException)
        {
            _logger.LogWarning(
                exception,
                "Desktop settings could not be loaded; using the Dark theme fallback");
            return DesktopSettings.Default;
        }
    }

    public async Task SaveAsync(
        DesktopSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!Enum.IsDefined(settings.Theme))
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.Theme,
                "Unsupported application theme.");
        }

        string settingsPath = _applicationPaths.SettingsPath;
        string directory = Path.GetDirectoryName(settingsPath)
            ?? throw new InvalidOperationException("The settings path has no parent directory.");
        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(settingsPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    settings,
                    SerializerOptions,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, settingsPath, overwrite: true);
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (IOException)
            {
                // A failed cleanup must not hide the original persistence failure.
            }
            catch (UnauthorizedAccessException)
            {
                // A failed cleanup must not hide the original persistence failure.
            }
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        return options;
    }
}
