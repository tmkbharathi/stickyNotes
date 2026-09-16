using StickyNotes.Core.Models.Update;
using StickyNotes.Core.Services.Lifecycle;
using StickyNotes.Core.Services.Logging;
using StickyNotes.Core.Services.Persistence;

namespace StickyNotes.Core.Services.Update;

/// <summary>
/// Production implementation of IUpdateService managing the end-to-end update lifecycle,
/// non-blocking checks, trusted MSIX package verification, pre-update data safety flush,
/// and safe application restart.
/// </summary>
public sealed class UpdateService : IUpdateService
{
    private readonly IPackageDeploymentProvider _deploymentProvider;
    private readonly INotePersistenceService _notePersistence;
    private readonly ISettingsService _settingsService;
    private readonly IWindowStateManager _windowStateManager;
    private readonly IAppLifecycleManager _lifecycleManager;
    private readonly IUpdateLogger _logger;

    private UpdateState _currentState = UpdateState.UpToDate;
    private UpdateInfo? _currentUpdateInfo;
    private double _downloadProgress = 0.0;
    private readonly SemaphoreSlim _updateLock = new(1, 1);
    private CancellationTokenSource? _downloadCts;

    public UpdateState CurrentState => _currentState;
    public UpdateInfo? CurrentUpdateInfo => _currentUpdateInfo;
    public double DownloadProgress => _downloadProgress;

    public event EventHandler<UpdateStateChangedEventArgs>? StateChanged;
    public event EventHandler<double>? DownloadProgressChanged;

    public UpdateService(
        IPackageDeploymentProvider deploymentProvider,
        INotePersistenceService notePersistence,
        ISettingsService settingsService,
        IWindowStateManager windowStateManager,
        IAppLifecycleManager lifecycleManager,
        IUpdateLogger logger)
    {
        _deploymentProvider = deploymentProvider;
        _notePersistence = notePersistence;
        _settingsService = settingsService;
        _windowStateManager = windowStateManager;
        _lifecycleManager = lifecycleManager;
        _logger = logger;
    }

    public Version GetCurrentVersion() => _lifecycleManager.GetCurrentPackageVersion();
    public Version? GetAvailableVersion() => _currentUpdateInfo?.AvailableVersion;
    public bool IsUpdateAvailable() => _currentUpdateInfo?.IsUpdateAvailable == true;

    private void SetState(UpdateState newState, string? message = null)
    {
        var prev = _currentState;
        if (prev != newState)
        {
            _currentState = newState;
            StateChanged?.Invoke(this, new UpdateStateChangedEventArgs(prev, newState, _currentUpdateInfo, message));
        }
    }

    public void CancelDownload()
    {
        if (_currentState == UpdateState.Downloading)
        {
            _logger.LogInfo("Download cancellation requested.");
            _downloadCts?.Cancel();
            _downloadProgress = 0;
            SetState(UpdateState.UpdateAvailable, _currentUpdateInfo?.ReleaseMetadata != null
                ? $"Version {_currentUpdateInfo.ReleaseMetadata.Version} is available"
                : "Update available");
        }
    }

    public async Task<UpdateInfo> CheckForUpdatesAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.GetUpdateSettings();
        if (!settings.Enabled && !force)
        {
            _logger.LogInfo("Update checking is disabled in settings.");
            return UpdateInfo.NoUpdate(GetCurrentVersion());
        }

        // Check cooldown policy
        if (!force && settings.LastCheckTimestamp.HasValue)
        {
            var elapsed = DateTimeOffset.UtcNow - settings.LastCheckTimestamp.Value;
            if (elapsed < TimeSpan.FromMinutes(settings.CooldownMinutes))
            {
                _logger.LogInfo($"Update check skipped due to active cooldown ({elapsed.TotalMinutes:F1}m < {settings.CooldownMinutes}m).");
                return _currentUpdateInfo ?? UpdateInfo.NoUpdate(GetCurrentVersion());
            }
        }

        await _updateLock.WaitAsync(cancellationToken);
        try
        {
            SetState(UpdateState.Checking, "Checking for updates...");
            _logger.LogCheckStarted(settings.Channel.ToString(), force);

            var feedUrl = settings.UpdateFeedUrlTemplate.Replace("{channel}", settings.Channel.ToString().ToLowerInvariant());
            var releaseMetadata = await _deploymentProvider.FetchReleaseFeedAsync(feedUrl, cancellationToken);

            await _settingsService.UpdateLastCheckTimestampAsync(DateTimeOffset.UtcNow, cancellationToken);

            var currentVersion = GetCurrentVersion();

            if (releaseMetadata == null)
            {
                _currentUpdateInfo = UpdateInfo.NoUpdate(currentVersion);
                _logger.LogCheckCompleted(currentVersion.ToString(), null, false);
                SetState(UpdateState.UpToDate, "You're up to date");
                return _currentUpdateInfo;
            }

            // Compatibility Verification
            var runningIdentity = _lifecycleManager.GetCurrentPackageIdentity();
            var runningPublisher = _lifecycleManager.GetCurrentPublisher();
            var runningArch = _lifecycleManager.GetCurrentProcessArchitecture();

            var compatibility = _deploymentProvider.ValidatePackageCompatibility(
                releaseMetadata, runningIdentity, runningPublisher, runningArch);

            if (!compatibility.IsCompatible)
            {
                _logger.LogWarning($"Update package {releaseMetadata.Version} rejected: {compatibility.Reason}");
                _currentUpdateInfo = UpdateInfo.Error(currentVersion, compatibility.Reason);
                SetState(UpdateState.Failed, compatibility.Reason);
                return _currentUpdateInfo;
            }

            // Compare versions
            if (releaseMetadata.ParsedVersion > currentVersion)
            {
                _currentUpdateInfo = UpdateInfo.UpdateFound(currentVersion, releaseMetadata, compatibility);
                _logger.LogUpdateDetected(releaseMetadata.Version, releaseMetadata.Channel.ToString(), releaseMetadata.PackageSizeBytes);
                _logger.LogCheckCompleted(currentVersion.ToString(), releaseMetadata.Version, true);

                SetState(UpdateState.UpdateAvailable, $"Sticky Notes {releaseMetadata.Version} is available.");

                // Check auto-download preference
                if (settings.AutoDownloadUpdates)
                {
                    _ = Task.Run(() => DownloadUpdateAsync(null, CancellationToken.None));
                }

                return _currentUpdateInfo;
            }
            else
            {
                _currentUpdateInfo = UpdateInfo.NoUpdate(currentVersion);
                _logger.LogCheckCompleted(currentVersion.ToString(), releaseMetadata.Version, false);
                SetState(UpdateState.UpToDate, "You're up to date");
                return _currentUpdateInfo;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Unexpected error during update check.", ex);
            _currentUpdateInfo = UpdateInfo.Error(GetCurrentVersion(), ex.Message);
            SetState(UpdateState.Failed, "Update check failed");
            return _currentUpdateInfo;
        }
        finally
        {
            _updateLock.Release();
        }
    }

    public async Task<bool> DownloadUpdateAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_currentUpdateInfo?.ReleaseMetadata == null)
        {
            _logger.LogWarning("Download requested but no update is available.");
            return false;
        }

        _downloadCts?.Dispose();
        _downloadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var activeToken = _downloadCts.Token;

        await _updateLock.WaitAsync(activeToken);
        try
        {
            _downloadProgress = 0.0;
            SetState(UpdateState.Downloading, "Downloading update...");

            var combinedProgress = new Progress<double>(p =>
            {
                _downloadProgress = p;
                DownloadProgressChanged?.Invoke(this, p);
                progress?.Report(p);
            });

            var success = await _deploymentProvider.DownloadPackageAsync(
                _currentUpdateInfo.ReleaseMetadata, combinedProgress, activeToken);

            if (success)
            {
                SetState(UpdateState.RestartRequired, "Update ready to install");

                // If auto-install when safe is enabled
                var settings = _settingsService.GetUpdateSettings();
                if (settings.AutoInstallWhenSafe)
                {
                    _logger.LogInfo("AutoInstallWhenSafe is enabled. Triggering scheduled install.");
                    _ = Task.Run(() => InstallUpdateAsync(true));
                }

                return true;
            }
            else
            {
                if (activeToken.IsCancellationRequested)
                {
                    _downloadProgress = 0;
                    SetState(UpdateState.UpdateAvailable, _currentUpdateInfo?.ReleaseMetadata != null
                        ? $"Version {_currentUpdateInfo.ReleaseMetadata.Version} is available"
                        : "Update available");
                    return false;
                }

                SetState(UpdateState.Failed, "Download failed");
                return false;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInfo("Download operation was cancelled.");
            _downloadProgress = 0;
            SetState(UpdateState.UpdateAvailable, _currentUpdateInfo?.ReleaseMetadata != null
                ? $"Version {_currentUpdateInfo.ReleaseMetadata.Version} is available"
                : "Update available");
            return false;
        }
        finally
        {
            _updateLock.Release();
        }
    }

    public async Task<UpdateInstallationResult> InstallUpdateAsync(bool restartAfterInstall = true)
    {
        if (_currentUpdateInfo?.ReleaseMetadata == null)
        {
            return UpdateInstallationResult.Failed("No validated release package ready to install.");
        }

        await _updateLock.WaitAsync();
        try
        {
            SetState(UpdateState.Installing, "Saving data and preparing installation...");

            // ========================================================
            // CRITICAL STEP: FLUSH ALL DATA PRIOR TO UPDATE/RESTART
            // ========================================================
            int activeNoteCount = _notePersistence.GetActiveNoteCount();
            bool notesSaved = await _notePersistence.FlushAllPendingAsync();
            bool settingsSaved = await _settingsService.SaveUpdateSettingsAsync(_settingsService.GetUpdateSettings());
            bool windowStateSaved = await _windowStateManager.FlushWindowStateAsync();

            bool allSaved = notesSaved && settingsSaved && windowStateSaved;
            _logger.LogPreUpdateSafetyFlush(allSaved, activeNoteCount);

            if (!allSaved)
            {
                var failReason = $"Persistence flush failed (Notes: {notesSaved}, Settings: {settingsSaved}, WindowState: {windowStateSaved})";
                _logger.LogError($"Update aborted to protect user data: {failReason}");
                SetState(UpdateState.Failed, "Update postponed: could not save pending notes.");
                return UpdateInstallationResult.SaveFailed(failReason);
            }

            // Deploy MSIX Package
            var deployProgress = new Progress<double>(p =>
            {
                _downloadProgress = p;
                DownloadProgressChanged?.Invoke(this, p);
            });

            bool deployed = await _deploymentProvider.DeployPackageAsync(_currentUpdateInfo.ReleaseMetadata, deployProgress);

            if (!deployed)
            {
                SetState(UpdateState.Failed, "Package deployment failed.");
                return UpdateInstallationResult.Failed("Deployment engine returned error.");
            }

            if (restartAfterInstall)
            {
                _logger.LogRestartRequired(_currentUpdateInfo.ReleaseMetadata.Version);
                SetState(UpdateState.RestartRequired, "Restarting application...");
                _lifecycleManager.RestartApplication("--after-update");
                return UpdateInstallationResult.Succeeded(restartPending: true);
            }

            SetState(UpdateState.RestartRequired, "Restart required to complete update.");
            return UpdateInstallationResult.Succeeded(restartPending: false);
        }
        catch (Exception ex)
        {
            _logger.LogError("Exception during update installation.", ex);
            SetState(UpdateState.Failed, "Update installation encountered an error.");
            return UpdateInstallationResult.Failed(ex.Message);
        }
        finally
        {
            _updateLock.Release();
        }
    }

    public void DeferUpdate(string reason = "User requested Later")
    {
        if (_currentUpdateInfo?.ReleaseMetadata != null)
        {
            _logger.LogUpdateDeferred(_currentUpdateInfo.ReleaseMetadata.Version, reason);
            SetState(UpdateState.Deferred, "Update postponed");
        }
    }
}
