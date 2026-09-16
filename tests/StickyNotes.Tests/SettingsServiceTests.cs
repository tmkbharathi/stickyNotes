using StickyNotes.Core.Models.Update;
using StickyNotes.Core.Services.Logging;
using StickyNotes.Core.Services.Persistence;
using Xunit;

namespace StickyNotes.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly IUpdateLogger _logger;

    public SettingsServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "StickyNotesTests_Settings_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _logger = new UpdateLogger(_testDir);
    }

    [Fact]
    public void FreshDirectory_ProvidesSensibleDefaults()
    {
        var service = new SettingsService(_testDir, _logger);
        var settings = service.GetUpdateSettings();

        Assert.NotNull(settings);
        Assert.True(settings.AutoCheckUpdates);
        Assert.Equal(UpdateChannel.Stable, settings.Channel);
        Assert.False(settings.HasInitializedStartup);
    }

    [Fact]
    public async Task CanPersistAndReload_CustomUpdateSettings()
    {
        var service1 = new SettingsService(_testDir, _logger);
        var settings = service1.GetUpdateSettings();

        settings.AutoCheckUpdates = false;
        settings.AutoDownloadUpdates = false;
        settings.HasInitializedStartup = true;
        settings.Channel = UpdateChannel.Beta;
        settings.LastCheckTimestamp = DateTimeOffset.UtcNow;

        await service1.SaveUpdateSettingsAsync(settings);

        // Reload from disk in a fresh instance
        var service2 = new SettingsService(_testDir, _logger);
        var reloaded = service2.GetUpdateSettings();

        Assert.False(reloaded.AutoCheckUpdates);
        Assert.False(reloaded.AutoDownloadUpdates);
        Assert.True(reloaded.HasInitializedStartup);
        Assert.Equal(UpdateChannel.Beta, reloaded.Channel);
        Assert.NotNull(reloaded.LastCheckTimestamp);
    }

    [Fact]
    public async Task SetUpdateChannelAsync_UpdatesAndPersistsChannel()
    {
        var service1 = new SettingsService(_testDir, _logger);
        await service1.SetUpdateChannelAsync(UpdateChannel.Development);

        var service2 = new SettingsService(_testDir, _logger);
        Assert.Equal(UpdateChannel.Development, service2.GetUpdateSettings().Channel);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch { }
    }
}
