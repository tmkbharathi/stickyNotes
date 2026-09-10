namespace StickyNotes.Core.Models.Update;

/// <summary>
/// Encapsulates the evaluation result of an update check.
/// </summary>
public sealed class UpdateInfo
{
    public bool IsUpdateAvailable { get; init; }
    public Version CurrentVersion { get; init; } = new(1, 0, 0);
    public Version? AvailableVersion { get; init; }
    public ReleaseMetadata? ReleaseMetadata { get; init; }
    public PackageCompatibilityResult Compatibility { get; init; } = PackageCompatibilityResult.Success();
    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? ErrorMessage { get; init; }

    public static UpdateInfo NoUpdate(Version currentVersion) => new()
    {
        IsUpdateAvailable = false,
        CurrentVersion = currentVersion,
        AvailableVersion = currentVersion,
        CheckedAt = DateTimeOffset.UtcNow
    };

    public static UpdateInfo UpdateFound(Version currentVersion, ReleaseMetadata metadata, PackageCompatibilityResult compatibility) => new()
    {
        IsUpdateAvailable = true,
        CurrentVersion = currentVersion,
        AvailableVersion = metadata.ParsedVersion,
        ReleaseMetadata = metadata,
        Compatibility = compatibility,
        CheckedAt = DateTimeOffset.UtcNow
    };

    public static UpdateInfo Error(Version currentVersion, string errorMessage) => new()
    {
        IsUpdateAvailable = false,
        CurrentVersion = currentVersion,
        ErrorMessage = errorMessage,
        CheckedAt = DateTimeOffset.UtcNow
    };
}
