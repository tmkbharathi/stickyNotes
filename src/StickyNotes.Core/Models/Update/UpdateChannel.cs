namespace StickyNotes.Core.Models.Update;

/// <summary>
/// Defines the release channels available for automatic updates.
/// </summary>
public enum UpdateChannel
{
    /// <summary>
    /// Production-ready stable channel (default for all end-users).
    /// </summary>
    Stable,

    /// <summary>
    /// Prerelease beta channel for testing upcoming features.
    /// </summary>
    Beta,

    /// <summary>
    /// Fast-ring development channel with bleeding-edge builds.
    /// </summary>
    Development
}
