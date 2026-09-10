using StickyNotes.Core.Models.Update;

namespace StickyNotes.Core.Services.Update;

/// <summary>
/// Abstraction over the native Windows MSIX / AppInstaller package deployment engine.
/// </summary>
public interface IPackageDeploymentProvider
{
    Task<ReleaseMetadata?> FetchReleaseFeedAsync(string feedUrl, CancellationToken cancellationToken = default);
    Task<bool> DownloadPackageAsync(ReleaseMetadata metadata, IProgress<double>? progress, CancellationToken cancellationToken = default);
    Task<bool> DeployPackageAsync(ReleaseMetadata metadata, IProgress<double>? progress, CancellationToken cancellationToken = default);
    PackageCompatibilityResult ValidatePackageCompatibility(ReleaseMetadata metadata, string runningIdentity, string runningPublisher, string runningArch);
}
