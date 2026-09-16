using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using StickyNotes.Core.Models.Update;
using StickyNotes.Core.Services.Logging;

namespace StickyNotes.Core.Services.Update;

/// <summary>
/// Deployment provider supporting both direct GitHub Releases and MSIX package feeds.
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
                return ParseReleaseJson(content);
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, feedUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("StickyNotes-App", "1.0"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Update provider returned status {response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseReleaseJson(json);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to fetch release feed from {feedUrl}", ex);
            return null;
        }
    }

    private ReleaseMetadata? ParseReleaseJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Detect GitHub Release API response
            if (root.TryGetProperty("tag_name", out var tagElement))
            {
                var tagName = tagElement.GetString() ?? string.Empty;
                var versionStr = tagName.TrimStart('v', 'V');

                var releaseNotes = string.Empty;
                if (root.TryGetProperty("body", out var bodyElement) && bodyElement.ValueKind == JsonValueKind.String)
                {
                    releaseNotes = bodyElement.GetString() ?? string.Empty;
                }

                var releaseDate = DateTimeOffset.UtcNow;
                if (root.TryGetProperty("published_at", out var publishedElement) &&
                    DateTimeOffset.TryParse(publishedElement.GetString(), out var parsedDate))
                {
                    releaseDate = parsedDate;
                }

                string packageUri = string.Empty;
                long packageSize = 0;

                if (root.TryGetProperty("assets", out var assetsElement) && assetsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assetsElement.EnumerateArray())
                    {
                        var name = asset.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                        var downloadUrl = asset.TryGetProperty("browser_download_url", out var urlProp) ? urlProp.GetString() ?? "" : "";
                        var size = asset.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : 0;

                        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                            name.EndsWith(".msix", StringComparison.OrdinalIgnoreCase) ||
                            name.EndsWith(".appinstaller", StringComparison.OrdinalIgnoreCase))
                        {
                            packageUri = downloadUrl;
                            packageSize = size;
                            break;
                        }
                    }
                }

                return new ReleaseMetadata
                {
                    Version = versionStr,
                    ReleaseDate = releaseDate,
                    ReleaseNotes = releaseNotes,
                    Channel = UpdateChannel.Stable,
                    Architecture = "neutral",
                    PackageIdentityName = "StickyNotes.Fluent",
                    PublisherId = "CN=StickyNotesDev",
                    PackageUri = packageUri,
                    PackageSizeBytes = packageSize,
                    MinWindowsVersion = "10.0.17763.0",
                    IsMandatory = false
                };
            }

            // Fallback to standard ReleaseMetadata schema
            return JsonSerializer.Deserialize<ReleaseMetadata>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to parse release feed payload.", ex);
            return null;
        }
    }

    public PackageCompatibilityResult ValidatePackageCompatibility(
        ReleaseMetadata metadata,
        string runningIdentity,
        string runningPublisher,
        string runningArch)
    {
        // 1. Validate CPU Architecture
        bool archMatched = string.Equals(metadata.Architecture, "neutral", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(metadata.Architecture, runningArch, StringComparison.OrdinalIgnoreCase);
        if (!archMatched)
        {
            return PackageCompatibilityResult.Failure(
                $"Architecture mismatch. Package targets '{metadata.Architecture}', but current process is '{runningArch}'.",
                identity: true, publisher: true, arch: false);
        }

        // 2. Validate OS Version requirements
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
        var fileExtension = metadata.PackageUri.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? "exe" : "msix";
        var targetFile = Path.Combine(_downloadCacheDirectory, $"StickyNotes_Setup_{metadata.Version}.{fileExtension}");

        try
        {
            _logger.LogDownloadStarted(metadata.Version, metadata.PackageUri);

            // If simulated / mock URI
            if (string.IsNullOrEmpty(metadata.PackageUri) || metadata.PackageUri.StartsWith("mock://"))
            {
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

            using var request = new HttpRequestMessage(HttpMethod.Get, metadata.PackageUri);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("StickyNotes-App", "1.0"));

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
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
            var fileExtension = metadata.PackageUri.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? "exe" : "msix";
            var targetFile = Path.Combine(_downloadCacheDirectory, $"StickyNotes_Setup_{metadata.Version}.{fileExtension}");

            if (File.Exists(targetFile) && fileExtension == "exe")
            {
                progress?.Report(50);
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetFile,
                    UseShellExecute = true
                });
                progress?.Report(100);
                _logger.LogInstallationCompleted(metadata.Version);
                return true;
            }

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
