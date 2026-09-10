using System.Collections.Concurrent;
using System.Text.Json;
using StickyNotes.Core.Models;
using StickyNotes.Core.Services.Logging;

namespace StickyNotes.Core.Services.Persistence;

/// <summary>
/// Thread-safe window state manager preserving multi-window coordinates during updates and restarts.
/// </summary>
public sealed class WindowStateManager : IWindowStateManager
{
    private readonly string _stateFilePath;
    private readonly IUpdateLogger _logger;
    private readonly ConcurrentDictionary<string, WindowStateModel> _windows = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public WindowStateManager(string storageDirectory, IUpdateLogger logger)
    {
        _logger = logger;
        Directory.CreateDirectory(storageDirectory);
        _stateFilePath = Path.Combine(storageDirectory, "windowstates.json");
    }

    public void TrackWindow(WindowStateModel state)
    {
        _windows[state.NoteId] = state;
    }

    public void UntrackWindow(string noteId)
    {
        _windows.TryRemove(noteId, out _);
    }

    public IReadOnlyList<WindowStateModel> GetTrackedWindows() => _windows.Values.ToList();

    public async Task<bool> FlushWindowStateAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var list = _windows.Values.ToList();
            var json = JsonSerializer.Serialize(list, JsonOptions);
            await File.WriteAllTextAsync(_stateFilePath, json, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to flush window state to disk before update.", ex);
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<WindowStateModel>> LoadSavedWindowStateAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_stateFilePath)) return Array.Empty<WindowStateModel>();

            var json = await File.ReadAllTextAsync(_stateFilePath, cancellationToken);
            var list = JsonSerializer.Deserialize<List<WindowStateModel>>(json) ?? new List<WindowStateModel>();

            _windows.Clear();
            foreach (var w in list)
            {
                _windows[w.NoteId] = w;
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to load saved window state.", ex);
            return Array.Empty<WindowStateModel>();
        }
        finally
        {
            _lock.Release();
        }
    }
}
