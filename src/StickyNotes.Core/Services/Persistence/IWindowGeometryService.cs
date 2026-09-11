using StickyNotes.Core.Models;

namespace StickyNotes.Core.Services.Persistence;

/// <summary>
/// Service contract for loading, updating, and saving window geometry and visibility across restarts.
/// </summary>
public interface IWindowGeometryService
{
    bool IsFirstRun { get; }
    WindowGeometryModel GetMainWindowGeometry();
    WindowGeometryModel GetFloatingWindowGeometry();
    void SaveMainWindowGeometry(int x, int y, int width, int height, bool isMaximized, bool isVisible);
    void SaveFloatingWindowGeometry(int x, int y, int width, int height, bool isMaximized, bool isVisible, string? activeNoteId = null);
    void SetMainWindowVisibility(bool isVisible);
    void SetFloatingWindowVisibility(bool isVisible, string? activeNoteId = null);
    Task<bool> FlushAsync(CancellationToken cancellationToken = default);
}
