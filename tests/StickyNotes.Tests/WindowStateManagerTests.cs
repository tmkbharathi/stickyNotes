using StickyNotes.Core.Models;
using StickyNotes.Core.Services.Logging;
using StickyNotes.Core.Services.Persistence;
using Xunit;

namespace StickyNotes.Tests;

public class WindowStateManagerTests : IDisposable
{
    private readonly string _testDir;
    private readonly IUpdateLogger _logger;

    public WindowStateManagerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "StickyNotesTests_WindowState_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _logger = new UpdateLogger(_testDir);
    }

    [Fact]
    public async Task TrackWindow_PersistsAndLoadsMultiWindowState()
    {
        var manager1 = new WindowStateManager(_testDir, _logger);

        manager1.TrackWindow(new WindowStateModel
        {
            NoteId = "note-win-1",
            X = 120,
            Y = 180,
            Width = 350,
            Height = 450,
            IsMinimized = false,
            IsAlwaysOnTop = true
        });

        manager1.TrackWindow(new WindowStateModel
        {
            NoteId = "note-win-2",
            X = 500,
            Y = 220,
            Width = 320,
            Height = 400,
            IsMinimized = true,
            IsAlwaysOnTop = false
        });

        Assert.Equal(2, manager1.GetTrackedWindows().Count);

        var flushed = await manager1.FlushWindowStateAsync();
        Assert.True(flushed);

        // Load in a fresh instance
        var manager2 = new WindowStateManager(_testDir, _logger);
        var loaded = await manager2.LoadSavedWindowStateAsync();

        Assert.Equal(2, loaded.Count);
        var win1 = loaded.FirstOrDefault(w => w.NoteId == "note-win-1");
        Assert.NotNull(win1);
        Assert.Equal(120, win1.X);
        Assert.Equal(180, win1.Y);
        Assert.False(win1.IsMinimized);
        Assert.True(win1.IsAlwaysOnTop);

        var win2 = loaded.FirstOrDefault(w => w.NoteId == "note-win-2");
        Assert.NotNull(win2);
        Assert.True(win2.IsMinimized);
        Assert.False(win2.IsAlwaysOnTop);
    }

    [Fact]
    public async Task UntrackWindow_RemovesWindowFromPersistence()
    {
        var manager = new WindowStateManager(_testDir, _logger);

        manager.TrackWindow(new WindowStateModel { NoteId = "note-del-1", Width = 300, Height = 400 });
        manager.TrackWindow(new WindowStateModel { NoteId = "note-del-2", Width = 300, Height = 400 });

        manager.UntrackWindow("note-del-1");
        await manager.FlushWindowStateAsync();

        var freshManager = new WindowStateManager(_testDir, _logger);
        var remaining = await freshManager.LoadSavedWindowStateAsync();

        Assert.Single(remaining);
        Assert.Equal("note-del-2", remaining[0].NoteId);
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
