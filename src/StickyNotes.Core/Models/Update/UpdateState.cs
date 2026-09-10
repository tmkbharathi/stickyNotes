namespace StickyNotes.Core.Models.Update;

/// <summary>
/// Explicit state machine states for the application update lifecycle.
/// </summary>
public enum UpdateState
{
    /// <summary>
    /// The application is at the latest version available for the selected channel.
    /// </summary>
    UpToDate,

    /// <summary>
    /// Background or manual update check is in progress.
    /// </summary>
    Checking,

    /// <summary>
    /// A newer valid package has been detected and is available to download/install.
    /// </summary>
    UpdateAvailable,

    /// <summary>
    /// The MSIX package or package manifest is downloading asynchronously in the background.
    /// </summary>
    Downloading,

    /// <summary>
    /// Windows Deployment service is registering or staging the new package.
    /// </summary>
    Installing,

    /// <summary>
    /// The package has been applied/staged and an application restart is required to switch versions.
    /// </summary>
    RestartRequired,

    /// <summary>
    /// The user opted to postpone/defer the update.
    /// </summary>
    Deferred,

    /// <summary>
    /// An error occurred during check, download, or deployment. Existing installation remains safe.
    /// </summary>
    Failed
}
