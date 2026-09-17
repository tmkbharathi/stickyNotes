using System.Text.Json.Serialization;

namespace StickyNotes.Core.Models.Update;

/// <summary>
/// User and system configuration for automatic updates.
/// </summary>
public sealed class UpdateSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("autoCheckUpdates")]
    public bool AutoCheckUpdates { get; set; } = true;

    [JsonPropertyName("autoDownloadUpdates")]
    public bool AutoDownloadUpdates { get; set; } = true;

    [JsonPropertyName("autoInstallWhenSafe")]
    public bool AutoInstallWhenSafe { get; set; } = true;

    [JsonPropertyName("startWithWindows")]
    public bool StartWithWindows { get; set; } = true;

    [JsonPropertyName("channel")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UpdateChannel Channel { get; set; } = UpdateChannel.Stable;

    [JsonPropertyName("checkIntervalHours")]
    public int CheckIntervalHours { get; set; } = 6;

    [JsonPropertyName("cooldownMinutes")]
    public int CooldownMinutes { get; set; } = 15;

    [JsonPropertyName("lastCheckTimestamp")]
    public DateTimeOffset? LastCheckTimestamp { get; set; }

    [JsonPropertyName("lastSuccessfulVersion")]
    public string? LastSuccessfulVersion { get; set; }

    [JsonPropertyName("updateFeedUrlTemplate")]
    public string UpdateFeedUrlTemplate { get; set; } = "https://api.github.com/repos/tmkbharathi/stickyNotes/releases/latest";

    [JsonPropertyName("hasInitializedStartup")]
    public bool HasInitializedStartup { get; set; } = false;
}
