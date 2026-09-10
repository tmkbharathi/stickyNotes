using System.Text.Json.Serialization;

namespace StickyNotes.Core.Models.Update;

/// <summary>
/// Represents structured release metadata published by a trusted update provider/feed.
/// </summary>
public sealed class ReleaseMetadata
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("releaseDate")]
    public DateTimeOffset ReleaseDate { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("releaseNotes")]
    public string ReleaseNotes { get; set; } = string.Empty;

    [JsonPropertyName("channel")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UpdateChannel Channel { get; set; } = UpdateChannel.Stable;

    [JsonPropertyName("architecture")]
    public string Architecture { get; set; } = "x64"; // x64, arm64, neutral

    [JsonPropertyName("minWindowsVersion")]
    public string MinWindowsVersion { get; set; } = "10.0.19041.0";

    [JsonPropertyName("packageIdentityName")]
    public string PackageIdentityName { get; set; } = "StickyNotes.Fluent";

    [JsonPropertyName("publisherId")]
    public string PublisherId { get; set; } = "CN=StickyNotesDev";

    [JsonPropertyName("packageUri")]
    public string PackageUri { get; set; } = string.Empty;

    [JsonPropertyName("appInstallerUri")]
    public string? AppInstallerUri { get; set; }

    [JsonPropertyName("packageSizeBytes")]
    public long PackageSizeBytes { get; set; }

    [JsonPropertyName("sha256Hash")]
    public string? Sha256Hash { get; set; }

    [JsonPropertyName("isMandatory")]
    public bool IsMandatory { get; set; }

    /// <summary>
    /// Parses the semantic/system version safely.
    /// </summary>
    [JsonIgnore]
    public Version ParsedVersion => System.Version.TryParse(Version, out var v) ? v : new Version(1, 0, 0);
}
