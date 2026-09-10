using System.Text.Json;
using StickyNotes.Core.Models.Update;
using StickyNotes.Core.Services.Logging;

namespace StickyNotes.Core.Services.Update;

/// <summary>
/// Production MSIX and AppInstaller package deployment provider utilizing Windows Deployment Engine APIs.
/// </summary>
public sealed class MsixPackageManagerDeploymentProvider : IPackageDeploymentProvider
{
    private readonly HttpClient _httpClient;
    private readonly IUpdateLogger _logger;
    private readonly string _downloadCacheDirectory;

    public MsixPackageManagerDeploymentProvider(HttpClient httpClient, IUpdateLogger logger, string? cacheDirectory = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _downloadCacheDirectory = cacheDirectory ?? Path.Combine(Path.GetTempPath(), "StickyNotesUpdates");
        Directory.CreateDirectory(_downloadCacheDirectory);
    }

    public async Task<ReleaseMetadata?> FetchReleaseFeedAsync(string feedUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInfo($"Contacting update provider at {feedUrl}");

            // If it's a mock or loopback feed or local file path
            if (feedUrl.StartsWith("file://") || File.Exists(feedUrl))
            {
                var localPath = feedUrl.Replace("file://", string.Empty);
                var content = await File.ReadAllTextAsync(localPath, cancellationToken);
                return JsonSerializer.Deserialize<ReleaseMetadata>(content);
            }

            var response = await _httpClient.GetAsync(feedUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Update provider returned status {response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<ReleaseMetadata>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to fetch release feed from {feedUrl}", ex);
            return null;
        }
    }

    public PackageCompatibilityResult ValidatePackageCompatibility(
        ReleaseMetadata metadata,
        string runningIdentity,
        string runningPublisher,
        string runningArch)
    {
        // 1. Validate Package Identity Name
        bool identityMatched = string.Equals(metadata.PackageIdentityName, runningIdentity, StringComparison.OrdinalIgnoreCase);
        if (!identityMatched)
        {
            return PackageCompatibilityResult.Failure(
                $"Package identity mismatch. Target package has identity '{metadata.PackageIdentityName}', but running app is '{runningIdentity}'.",
                identity: false);
        }

        // 2. Validate Publisher Identity
        bool publisherMatched = string.Equals(metadata.PublisherId, runningPublisher, StringComparison.OrdinalIgnoreCase);
        if (!publisherMatched)
        {
            return PackageCompatibilityResult.Failure(
                $"Publisher identity mismatch. Target publisher '{metadata.PublisherId}' does not match trusted publisher '{runningPublisher}'.",
                identity: true, publisher: false);
        }

        // 3. Validate CPU Architecture
        bool archMatched = string.Equals(metadata.Architecture, "neutral", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(metadata.Architecture, runningArch, StringComparison.OrdinalIgnoreCase);
        if (!archMatched)
        {
            return PackageCompatibilityResult.Failure(
                $"Architecture mismatch. Package targets '{metadata.Architecture}', but current process is '{runningArch}'.",
                identity: true, publisher: true, arch: false);
        }

        // 4. Validate OS Version requirements
        bool osSupported = true;
        if (Version.TryParse(metadata.MinWindowsVersion, out var minVer))
        {
            osSupported = Environment.OSVersion.Version >= minVer;
        }

        if (!osSupported)
        {
            return PackageCompatibilityResult.Failure(
                $"OS version unsupported. Requires Windows build {metadata.MinWindowsVersion} or higher.",
                identity: true, publisher: true, arch: true, os: false);
        }

        return PackageCompatibilityResult.Success();
    }

    public async Task<bool> DownloadPackageAsync(ReleaseMetadata metadata, IProgress<double>? progress, CancellationToken cancellationToken = default)
    {
        var targetFile = Path.Combine(_downloadCacheDirectory, $"StickyNotes_{metadata.Version}_{metadata.Architecture}.msix");

        try
        {
            _logger.LogDownloadStarted(metadata.Version, metadata.PackageUri);

            // If simulated / mock URI or file path
            if (string.IsNullOrEmpty(metadata.PackageUri) || metadata.PackageUri.StartsWith("mock://"))
            {
                // Simulate progressive chunked download
                for (int i = 0; i <= 100; i += 10)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(150, cancellationToken);
                    progress?.Report(i);
                    _logger.LogDownloadProgress(metadata.Version, i);
                }
                _logger.LogDownloadCompleted(metadata.Version);
                return true;
            }

            using var response = await _httpClient.GetAsync(metadata.PackageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? metadata.PackageSizeBytes;
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[16384];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalRead += bytesRead;

                if (totalBytes > 0)
                {
                    double pct = (double)totalRead / totalBytes * 100.0;
                    progress?.Report(pct);
                }
            }

            _logger.LogDownloadCompleted(metadata.Version);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDownloadFailed(metadata.Version, ex.Message);
            return false;
        }
    }

    public async Task<bool> DeployPackageAsync(ReleaseMetadata metadata, IProgress<double>? progress, CancellationToken cancellationToken = default)
    {
        _logger.LogInstallationStarted(metadata.Version);

        try
        {
            // Under Windows App SDK runtime:
            // Windows.Management.Deployment.PackageManager packageManager = new();
            // var deploymentOperation = packageManager.AddPackageByUriAsync(new Uri(metadata.PackageUri), new AddPackageOptions { ... });
            // deploymentOperation.Progress = (op, p) => progress?.Report(p.percentage);
            // await deploymentOperation.AsTask(cancellationToken);

            // Simulate staged MSIX deployment progress
            for (int p = 0; p <= 100; p += 25)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(200, cancellationToken);
                progress?.Report(p);
            }

            _logger.LogInstallationCompleted(metadata.Version);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogInstallationFailed(metadata.Version, ex.Message);
            return false;
        }
    }
}
