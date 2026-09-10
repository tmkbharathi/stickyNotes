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
    public bool AutoInstallWhenSafe { get; set; } = false;

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
    public string UpdateFeedUrlTemplate { get; set; } = "https://updates.stickynotes.fluent/releases/{channel}/latest.json";
}
