namespace StickyNotes.Core.Services.Logging;

/// <summary>
/// Dedicated audit logger for the Auto-Update lifecycle.
/// STRICT PRIVACY POLICY: Never logs user note contents or snippet data.
/// </summary>
public interface IUpdateLogger
{
    void LogCheckStarted(string channel, bool forced);
    void LogCheckCompleted(string currentVersion, string? availableVersion, bool isUpdateAvailable);
    void LogUpdateDetected(string availableVersion, string channel, long packageSizeBytes);
    void LogDownloadStarted(string version, string packageUri);
    void LogDownloadProgress(string version, double percentage);
    void LogDownloadCompleted(string version);
    void LogDownloadFailed(string version, string reason);
    void LogPreUpdateSafetyFlush(bool success, int noteCount);
    void LogInstallationStarted(string version);
    void LogInstallationCompleted(string version);
    void LogInstallationFailed(string version, string reason);
    void LogUpdateDeferred(string version, string reason);
    void LogRestartRequired(string newVersion);
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message, Exception? ex = null);
    
    IReadOnlyList<string> GetRecentLogEntries();
}
