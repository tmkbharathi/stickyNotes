using System.Text.Json;
using StickyNotes.Core.Models.Update;
using StickyNotes.Core.Services.Logging;

namespace StickyNotes.Core.Services.Persistence;

/// <summary>
/// Thread-safe file-backed implementation of ISettingsService.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private readonly IUpdateLogger _logger;
    private UpdateSettings _cachedSettings = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public SettingsService(string storageDirectory, IUpdateLogger logger)
    {
        _logger = logger;
        Directory.CreateDirectory(storageDirectory);
        _settingsFilePath = Path.Combine(storageDirectory, "settings.json");
        LoadInitialSettings();
    }

    private void LoadInitialSettings()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                _cachedSettings = JsonSerializer.Deserialize<UpdateSettings>(json) ?? new UpdateSettings();
            }
            else
            {
                _cachedSettings = new UpdateSettings();
                File.WriteAllText(_settingsFilePath, JsonSerializer.Serialize(_cachedSettings, JsonOptions));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to load settings file. Using default settings.", ex);
            _cachedSettings = new UpdateSettings();
        }
    }

    public UpdateSettings GetUpdateSettings() => _cachedSettings;

    public async Task<bool> SaveUpdateSettingsAsync(UpdateSettings settings, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _cachedSettings = settings;
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            await File.WriteAllTextAsync(_settingsFilePath, json, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to save update settings to disk.", ex);
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> UpdateLastCheckTimestampAsync(DateTimeOffset timestamp, CancellationToken cancellationToken = default)
    {
        _cachedSettings.LastCheckTimestamp = timestamp;
        return await SaveUpdateSettingsAsync(_cachedSettings, cancellationToken);
    }

    public async Task<bool> SetUpdateChannelAsync(UpdateChannel channel, CancellationToken cancellationToken = default)
    {
        _cachedSettings.Channel = channel;
        return await SaveUpdateSettingsAsync(_cachedSettings, cancellationToken);
    }
}
