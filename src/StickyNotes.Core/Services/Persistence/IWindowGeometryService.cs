using StickyNotes.Core.Models;

namespace StickyNotes.Core.Services.Persistence;

/// <summary>
/// Service contract for loading, updating, and saving window geometry across restarts.
/// </summary>
public interface IWindowGeometryService
{
    WindowGeometryModel GetMainWindowGeometry();
    WindowGeometryModel GetFloatingWindowGeometry();
    void SaveMainWindowGeometry(int x, int y, int width, int height, bool isMaximized);
    void SaveFloatingWindowGeometry(int x, int y, int width, int height, bool isMaximized);
    Task<bool> FlushAsync(CancellationToken cancellationToken = default);
}
