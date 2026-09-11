using StickyNotes.Core.Services.Persistence;
using Xunit;

namespace StickyNotes.Tests;

public class WindowGeometryServiceTests : IDisposable
{
    private readonly string _tempDirectory;

    public WindowGeometryServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "StickyNotesTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task SavesAndRestores_MainWindowAndFloatingWindow_Independently()
    {
        using var service1 = new WindowGeometryService(_tempDirectory);

        // Verify initial state
        var initialMain = service1.GetMainWindowGeometry();
        var initialFloat = service1.GetFloatingWindowGeometry();
        Assert.False(initialMain.HasValue);
        Assert.False(initialFloat.HasValue);
        Assert.True(initialMain.IsVisible);
        Assert.False(initialFloat.IsVisible);

        // Save MainWindow geometry
        service1.SaveMainWindowGeometry(150, 200, 1024, 768, false, true);
        // Save FloatingWindow geometry
        service1.SaveFloatingWindowGeometry(400, 300, 360, 520, false, true, "note-123");

        await service1.FlushAsync();

        // Create new service instance simulating app restart
        using var service2 = new WindowGeometryService(_tempDirectory);

        var restoredMain = service2.GetMainWindowGeometry();
        var restoredFloat = service2.GetFloatingWindowGeometry();

        Assert.True(restoredMain.HasValue);
        Assert.Equal(150, restoredMain.X);
        Assert.Equal(200, restoredMain.Y);
        Assert.Equal(1024, restoredMain.Width);
        Assert.Equal(768, restoredMain.Height);
        Assert.False(restoredMain.IsMaximized);
        Assert.True(restoredMain.IsVisible);

        Assert.True(restoredFloat.HasValue);
        Assert.Equal(400, restoredFloat.X);
        Assert.Equal(300, restoredFloat.Y);
        Assert.Equal(360, restoredFloat.Width);
        Assert.Equal(520, restoredFloat.Height);
        Assert.False(restoredFloat.IsMaximized);
        Assert.True(restoredFloat.IsVisible);
        Assert.Equal("note-123", restoredFloat.ActiveNoteId);
    }

    [Fact]
    public async Task Persists_MaximizedStateAndVisibility_Correctly()
    {
        using var service1 = new WindowGeometryService(_tempDirectory);
        service1.SaveMainWindowGeometry(100, 100, 1200, 800, true, false);
        service1.SetFloatingWindowVisibility(true, "note-456");
        await service1.FlushAsync();

        using var service2 = new WindowGeometryService(_tempDirectory);
        var restoredMain = service2.GetMainWindowGeometry();
        var restoredFloat = service2.GetFloatingWindowGeometry();

        Assert.True(restoredMain.IsMaximized);
        Assert.False(restoredMain.IsVisible);
        Assert.Equal(1200, restoredMain.Width);
        Assert.Equal(800, restoredMain.Height);

        Assert.True(restoredFloat.IsVisible);
        Assert.Equal("note-456", restoredFloat.ActiveNoteId);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }
}
