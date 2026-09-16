using System.Runtime.InteropServices;
using StickyNotes.Core.Services.Lifecycle;
using Xunit;

namespace StickyNotes.Tests;

public class StartupServiceTests
{
    [Fact]
    public void StartupService_ExecutablePath_ResolvesCorrectly()
    {
        var customPath = @"C:\Program Files\Sticky Notes\StickyNotes.exe";
        var service = new WindowsStartupService(customExecutablePath: customPath);

        Assert.Equal(customPath, service.GetStartupExecutablePath());
    }

    [Fact]
    public void StartupService_CanSetAndQueryStartupState()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var customPath = @"C:\StickyNotes\StickyNotes.exe";
        var service = new WindowsStartupService(customExecutablePath: customPath);

        // Test enabling startup
        bool enabled = service.SetStartupEnabled(true);
        Assert.True(enabled);
        Assert.True(service.IsStartupEnabled());

        // Test disabling startup
        bool disabled = service.SetStartupEnabled(false);
        Assert.True(disabled);
        Assert.False(service.IsStartupEnabled());
    }
}
