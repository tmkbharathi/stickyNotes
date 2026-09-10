using StickyNotes.Core.Models.Update;

namespace StickyNotes.Core.Services.Persistence;

/// <summary>
/// Service contract for loading, updating, and saving user configuration and update preferences.
/// </summary>
public interface ISettingsService
{
    UpdateSettings GetUpdateSettings();
    Task<bool> SaveUpdateSettingsAsync(UpdateSettings settings, CancellationToken cancellationToken = default);
    Task<bool> UpdateLastCheckTimestampAsync(DateTimeOffset timestamp, CancellationToken cancellationToken = default);
    Task<bool> SetUpdateChannelAsync(UpdateChannel channel, CancellationToken cancellationToken = default);
}
