using StickyNotes.Core.Models;

namespace StickyNotes.Core.Services.Persistence;

/// <summary>
/// Manages and persists window positions, dimensions, pin states, and always-on-top flags
/// across application restarts and updates.
/// </summary>
public interface IWindowStateManager
{
    void TrackWindow(WindowStateModel state);
    void UntrackWindow(string noteId);
    IReadOnlyList<WindowStateModel> GetTrackedWindows();
    Task<bool> FlushWindowStateAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WindowStateModel>> LoadSavedWindowStateAsync(CancellationToken cancellationToken = default);
}
