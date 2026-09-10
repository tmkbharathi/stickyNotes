using StickyNotes.Core.Models.Update;

namespace StickyNotes.Core.Services.Update;

/// <summary>
/// Event arguments fired when the update state machine transitions.
/// </summary>
public sealed class UpdateStateChangedEventArgs : EventArgs
{
    public UpdateState PreviousState { get; }
    public UpdateState NewState { get; }
    public UpdateInfo? UpdateInfo { get; }
    public string? StatusMessage { get; }

    public UpdateStateChangedEventArgs(UpdateState previousState, UpdateState newState, UpdateInfo? updateInfo = null, string? message = null)
    {
        PreviousState = previousState;
        NewState = newState;
        UpdateInfo = updateInfo;
        StatusMessage = message;
    }
}

/// <summary>
/// Result of an installation attempt.
/// </summary>
public sealed class UpdateInstallationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool RestartPending { get; init; }
    public bool PreUpdateSaveSucceeded { get; init; }

    public static UpdateInstallationResult Succeeded(bool restartPending) => new()
    {
        Success = true,
        RestartPending = restartPending,
        PreUpdateSaveSucceeded = true,
        Message = "Update successfully applied."
    };

    public static UpdateInstallationResult SaveFailed(string reason) => new()
    {
        Success = false,
        RestartPending = false,
        PreUpdateSaveSucceeded = false,
        Message = $"Your changes could not be saved. The update has been postponed: {reason}"
    };

    public static UpdateInstallationResult Failed(string reason) => new()
    {
        Success = false,
        RestartPending = false,
        PreUpdateSaveSucceeded = true,
        Message = $"We couldn't install the update. Your current version is still installed: {reason}"
    };
}

/// <summary>
/// Main application contract for the Auto-Update system.
/// Keeps all update logic decoupled from UI and ViewModels.
/// </summary>
public interface IUpdateService
{
    UpdateState CurrentState { get; }
    UpdateInfo? CurrentUpdateInfo { get; }
    double DownloadProgress { get; }

    Task<UpdateInfo> CheckForUpdatesAsync(bool force = false, CancellationToken cancellationToken = default);
    Task<bool> DownloadUpdateAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    Task<UpdateInstallationResult> InstallUpdateAsync(bool restartAfterInstall = true);
    void DeferUpdate(string reason = "User requested Later");

    Version GetCurrentVersion();
    Version? GetAvailableVersion();
    bool IsUpdateAvailable();

    event EventHandler<UpdateStateChangedEventArgs>? StateChanged;
    event EventHandler<double>? DownloadProgressChanged;
}
