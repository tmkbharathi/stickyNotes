using System.Text.Json;
using StickyNotes.Core.Models;
using StickyNotes.Core.Services.Logging;

namespace StickyNotes.Core.Services.Persistence;

/// <summary>
/// Thread-safe file-backed implementation of IWindowGeometryService persisting window geometry
/// and visibility states continuously and on shutdown.
/// </summary>
public sealed class WindowGeometryService : IWindowGeometryService, IDisposable
{
    private readonly string _geometryFilePath;
    private readonly IUpdateLogger? _logger;
    private AppWindowGeometries _geometries = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private System.Threading.Timer? _debounceTimer;

    public bool IsFirstRun => _geometries.IsFirstRun;

    public WindowGeometryService(string storageDirectory, IUpdateLogger? logger = null)
    {
        _logger = logger;
        Directory.CreateDirectory(storageDirectory);
        _geometryFilePath = Path.Combine(storageDirectory, "window_geometry.json");
        LoadInitialGeometries();
    }

    private void LoadInitialGeometries()
    {
        try
        {
            if (File.Exists(_geometryFilePath))
            {
                var json = File.ReadAllText(_geometryFilePath);
                _geometries = JsonSerializer.Deserialize<AppWindowGeometries>(json) ?? new AppWindowGeometries();
                _geometries.IsFirstRun = false;
            }
            else
            {
                _geometries = new AppWindowGeometries
                {
                    IsFirstRun = true,
                    MainWindow = new WindowGeometryModel { IsVisible = true },
                    FloatingWindow = new WindowGeometryModel { IsVisible = false }
                };
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to load saved window geometry. Using defaults.", ex);
            _geometries = new AppWindowGeometries
            {
                IsFirstRun = true,
                MainWindow = new WindowGeometryModel { IsVisible = true },
                FloatingWindow = new WindowGeometryModel { IsVisible = false }
            };
        }
    }

    public WindowGeometryModel GetMainWindowGeometry() => _geometries.MainWindow;

    public WindowGeometryModel GetFloatingWindowGeometry() => _geometries.FloatingWindow;

    public void SaveMainWindowGeometry(int x, int y, int width, int height, bool isMaximized, bool isVisible)
    {
        _geometries.IsFirstRun = false;
        _geometries.MainWindow.X = x;
        _geometries.MainWindow.Y = y;
        _geometries.MainWindow.Width = width;
        _geometries.MainWindow.Height = height;
        _geometries.MainWindow.IsMaximized = isMaximized;
        _geometries.MainWindow.IsVisible = isVisible;
        DebounceFlush();
    }

    public void SaveFloatingWindowGeometry(int x, int y, int width, int height, bool isMaximized, bool isVisible, string? activeNoteId = null)
    {
        _geometries.IsFirstRun = false;
        _geometries.FloatingWindow.X = x;
        _geometries.FloatingWindow.Y = y;
        _geometries.FloatingWindow.Width = width;
        _geometries.FloatingWindow.Height = height;
        _geometries.FloatingWindow.IsMaximized = isMaximized;
        _geometries.FloatingWindow.IsVisible = isVisible;
        if (!string.IsNullOrEmpty(activeNoteId))
        {
            _geometries.FloatingWindow.ActiveNoteId = activeNoteId;
        }
        DebounceFlush();
    }

    public void SetMainWindowVisibility(bool isVisible)
    {
        _geometries.IsFirstRun = false;
        _geometries.MainWindow.IsVisible = isVisible;
        DebounceFlush();
    }

    public void SetFloatingWindowVisibility(bool isVisible, string? activeNoteId = null)
    {
        _geometries.IsFirstRun = false;
        _geometries.FloatingWindow.IsVisible = isVisible;
        if (!string.IsNullOrEmpty(activeNoteId))
        {
            _geometries.FloatingWindow.ActiveNoteId = activeNoteId;
        }
        DebounceFlush();
    }

    private void DebounceFlush()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = new System.Threading.Timer(_ =>
        {
            _ = FlushAsync();
        }, null, 300, Timeout.Infinite);
    }

    public async Task<bool> FlushAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var tempFile = _geometryFilePath + ".tmp";
            var json = JsonSerializer.Serialize(_geometries, JsonOptions);
            await File.WriteAllTextAsync(tempFile, json, cancellationToken);
            File.Move(tempFile, _geometryFilePath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to flush window geometry to disk.", ex);
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        _debounceTimer?.Dispose();
        _lock.Dispose();
    }
}
