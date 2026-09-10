using Moq;
using StickyNotes.Core.Models;
using StickyNotes.Core.Models.Note;
using StickyNotes.Core.Models.Update;
using StickyNotes.Core.Services.Lifecycle;
using StickyNotes.Core.Services.Logging;
using StickyNotes.Core.Services.Persistence;
using StickyNotes.Core.Services.Update;
using Xunit;

namespace StickyNotes.Tests;

public class UpdateServiceTests
{
    private readonly Mock<IPackageDeploymentProvider> _deploymentProviderMock = new();
    private readonly Mock<INotePersistenceService> _notePersistenceMock = new();
    private readonly Mock<ISettingsService> _settingsServiceMock = new();
    private readonly Mock<IWindowStateManager> _windowStateManagerMock = new();
    private readonly Mock<IAppLifecycleManager> _lifecycleManagerMock = new();
    private readonly Mock<IUpdateLogger> _loggerMock = new();

    private readonly Version _currentVersion = new(1, 0, 0);
    private const string PackageIdentity = "StickyNotes.Fluent";
    private const string PublisherId = "CN=StickyNotesDev";
    private const string Architecture = "x64";

    public UpdateServiceTests()
    {
        _lifecycleManagerMock.Setup(l => l.GetCurrentPackageVersion()).Returns(_currentVersion);
        _lifecycleManagerMock.Setup(l => l.GetCurrentPackageIdentity()).Returns(PackageIdentity);
        _lifecycleManagerMock.Setup(l => l.GetCurrentPublisher()).Returns(PublisherId);
        _lifecycleManagerMock.Setup(l => l.GetCurrentProcessArchitecture()).Returns(Architecture);

        _settingsServiceMock.Setup(s => s.GetUpdateSettings()).Returns(new UpdateSettings
        {
            Enabled = true,
            Channel = UpdateChannel.Stable,
            CooldownMinutes = 15,
            LastCheckTimestamp = null
        });

        _notePersistenceMock.Setup(n => n.FlushAllPendingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _settingsServiceMock.Setup(s => s.SaveUpdateSettingsAsync(It.IsAny<UpdateSettings>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _windowStateManagerMock.Setup(w => w.FlushWindowStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private UpdateService CreateService()
    {
        return new UpdateService(
            _deploymentProviderMock.Object,
            _notePersistenceMock.Object,
            _settingsServiceMock.Object,
            _windowStateManagerMock.Object,
            _lifecycleManagerMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenNewerVersionAvailable_ReturnsUpdateFoundAndSetsState()
    {
        // Arrange
        var service = CreateService();
        var release = new ReleaseMetadata
        {
            Version = "1.1.0",
            PackageIdentityName = PackageIdentity,
            PublisherId = PublisherId,
            Architecture = "x64",
            PackageUri = "https://updates.stickynotes.fluent/v1.1.0.msix"
        };

        _deploymentProviderMock
            .Setup(d => d.FetchReleaseFeedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(release);

        _deploymentProviderMock
            .Setup(d => d.ValidatePackageCompatibility(release, PackageIdentity, PublisherId, Architecture))
            .Returns(PackageCompatibilityResult.Success());

        // Act
        var result = await service.CheckForUpdatesAsync(force: true);

        // Assert
        Assert.True(result.IsUpdateAvailable);
        Assert.Equal(new Version(1, 1, 0), result.AvailableVersion);
        Assert.Equal(UpdateState.UpdateAvailable, service.CurrentState);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenPackageIdentityMismatched_RejectsUpdate()
    {
        // Arrange
        var service = CreateService();
        var release = new ReleaseMetadata
        {
            Version = "1.2.0",
            PackageIdentityName = "Malicious.FakeApp",
            PublisherId = PublisherId
        };

        _deploymentProviderMock
            .Setup(d => d.FetchReleaseFeedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(release);

        _deploymentProviderMock
            .Setup(d => d.ValidatePackageCompatibility(release, PackageIdentity, PublisherId, Architecture))
            .Returns(PackageCompatibilityResult.Failure("Package identity mismatch", identity: false));

        // Act
        var result = await service.CheckForUpdatesAsync(force: true);

        // Assert
        Assert.False(result.IsUpdateAvailable);
        Assert.Equal(UpdateState.Failed, service.CurrentState);
    }

    [Fact]
    public async Task InstallUpdateAsync_WhenNotePersistenceFails_AbortsUpdateToProtectUserData()
    {
        // Arrange
        var service = CreateService();
        var release = new ReleaseMetadata
        {
            Version = "1.1.0",
            PackageIdentityName = PackageIdentity,
            PublisherId = PublisherId
        };

        _deploymentProviderMock
            .Setup(d => d.FetchReleaseFeedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(release);

        _deploymentProviderMock
            .Setup(d => d.ValidatePackageCompatibility(release, PackageIdentity, PublisherId, Architecture))
            .Returns(PackageCompatibilityResult.Success());

        await service.CheckForUpdatesAsync(force: true);

        // Simulate save failure
        _notePersistenceMock.Setup(n => n.FlushAllPendingAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var installResult = await service.InstallUpdateAsync(restartAfterInstall: true);

        // Assert
        Assert.False(installResult.Success);
        Assert.False(installResult.PreUpdateSaveSucceeded);
        Assert.Contains("Your changes could not be saved", installResult.Message);
        Assert.Equal(UpdateState.Failed, service.CurrentState);

        // Ensure deployment was NOT attempted and app was NOT restarted
        _deploymentProviderMock.Verify(d => d.DeployPackageAsync(It.IsAny<ReleaseMetadata>(), It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()), Times.Never);
        _lifecycleManagerMock.Verify(l => l.RestartApplication(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InstallUpdateAsync_WhenAllSavesSucceed_DeploysPackageAndRestarts()
    {
        // Arrange
        var service = CreateService();
        var release = new ReleaseMetadata
        {
            Version = "1.1.0",
            PackageIdentityName = PackageIdentity,
            PublisherId = PublisherId
        };

        _deploymentProviderMock
            .Setup(d => d.FetchReleaseFeedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(release);

        _deploymentProviderMock
            .Setup(d => d.ValidatePackageCompatibility(release, PackageIdentity, PublisherId, Architecture))
            .Returns(PackageCompatibilityResult.Success());

        _deploymentProviderMock
            .Setup(d => d.DeployPackageAsync(release, It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await service.CheckForUpdatesAsync(force: true);

        // Act
        var installResult = await service.InstallUpdateAsync(restartAfterInstall: true);

        // Assert
        Assert.True(installResult.Success);
        Assert.True(installResult.RestartPending);
        _lifecycleManagerMock.Verify(l => l.RestartApplication("--after-update"), Times.Once);
    }
}
