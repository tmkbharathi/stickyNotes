using System.Collections.Concurrent;

namespace StickyNotes.Core.Services.Logging;

/// <summary>
/// Production implementation of IUpdateLogger writing structured diagnostics to in-memory ring buffer
/// and local AppData diagnostic log file.
/// </summary>
public sealed class UpdateLogger : IUpdateLogger
{
    private readonly ConcurrentQueue<string> _logBuffer = new();
    private const int MaxMemoryEntries = 200;
    private readonly string? _logFilePath;
    private readonly object _fileLock = new();

    public UpdateLogger(string? localAppDataFolder = null)
    {
        if (!string.IsNullOrEmpty(localAppDataFolder))
        {
            try
            {
                Directory.CreateDirectory(localAppDataFolder);
                _logFilePath = Path.Combine(localAppDataFolder, "updates.log");
            }
            catch
            {
                // Fallback to memory-only if filesystem permissions restricted
            }
        }
    }

    private void Append(string level, string message)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff 'UTC'");
        var entry = $"[{timestamp}] [{level}] {message}";

        _logBuffer.Enqueue(entry);
        while (_logBuffer.Count > MaxMemoryEntries && _logBuffer.TryDequeue(out _)) { }

        if (_logFilePath != null)
        {
            lock (_fileLock)
            {
                try
                {
                    File.AppendAllText(_logFilePath, entry + Environment.NewLine);
                }
                catch
                {
                    // Non-fatal, keep memory buffer
                }
            }
        }
    }

    public void LogCheckStarted(string channel, bool forced) =>
        Append("INFO", $"Update check started. Channel: {channel}, Forced: {forced}");

    public void LogCheckCompleted(string currentVersion, string? availableVersion, bool isUpdateAvailable) =>
        Append("INFO", $"Update check completed. Current: {currentVersion}, Available: {availableVersion ?? "None"}, UpdateFound: {isUpdateAvailable}");

    public void LogUpdateDetected(string availableVersion, string channel, long packageSizeBytes) =>
        Append("INFO", $"Update detected. TargetVersion: {availableVersion}, Channel: {channel}, Size: {packageSizeBytes} bytes");

    public void LogDownloadStarted(string version, string packageUri) =>
        Append("INFO", $"Download started for package version {version} from URI: {packageUri}");

    public void LogDownloadProgress(string version, double percentage) =>
        Append("DEBUG", $"Download progress for {version}: {percentage:F1}%");

    public void LogDownloadCompleted(string version) =>
        Append("INFO", $"Download completed successfully for package version {version}");

    public void LogDownloadFailed(string version, string reason) =>
        Append("ERROR", $"Download failed for package version {version}. Reason: {reason}");

    public void LogPreUpdateSafetyFlush(bool success, int noteCount) =>
        Append(success ? "INFO" : "ERROR", $"Pre-update state persistence flush. Succeeded: {success}, ActiveNotesCount: {noteCount}");

    public void LogInstallationStarted(string version) =>
        Append("INFO", $"MSIX package deployment started for version {version}");

    public void LogInstallationCompleted(string version) =>
        Append("INFO", $"MSIX package deployment completed for version {version}");

    public void LogInstallationFailed(string version, string reason) =>
        Append("ERROR", $"MSIX package deployment failed for version {version}. Reason: {reason}");

    public void LogUpdateDeferred(string version, string reason) =>
        Append("INFO", $"Update {version} was deferred by user policy. Reason: {reason}");

    public void LogRestartRequired(string newVersion) =>
        Append("INFO", $"Application restart required to activate new package version {newVersion}");

    public void LogInfo(string message) => Append("INFO", message);
    public void LogWarning(string message) => Append("WARN", message);
    public void LogError(string message, Exception? ex = null) =>
        Append("ERROR", ex != null ? $"{message} | Exception: {ex.GetType().Name} - {ex.Message}" : message);

    public IReadOnlyList<string> GetRecentLogEntries() => _logBuffer.ToArray();
}
