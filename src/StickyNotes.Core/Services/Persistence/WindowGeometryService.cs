using System.Text.Json;
using StickyNotes.Core.Models;
using StickyNotes.Core.Services.Logging;

namespace StickyNotes.Core.Services.Persistence;

/// <summary>
/// Thread-safe file-backed implementation of IWindowGeometryService persisting window geometry
/// continuously and on shutdown.
/// </summary>
public sealed class WindowGeometryService : IWindowGeometryService, IDisposable
{
    private readonly string _geometryFilePath;
    private readonly IUpdateLogger? _logger;
    private AppWindowGeometries _geometries = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private System.Threading.Timer? _debounceTimer;

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
            }
            else
            {
                _geometries = new AppWindowGeometries();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to load saved window geometry. Using defaults.", ex);
            _geometries = new AppWindowGeometries();
        }
    }

    public WindowGeometryModel GetMainWindowGeometry() => _geometries.MainWindow;

    public WindowGeometryModel GetFloatingWindowGeometry() => _geometries.FloatingWindow;

    public void SaveMainWindowGeometry(int x, int y, int width, int height, bool isMaximized)
    {
        _geometries.MainWindow = new WindowGeometryModel
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
            IsMaximized = isMaximized
        };
        DebounceFlush();
    }

    public void SaveFloatingWindowGeometry(int x, int y, int width, int height, bool isMaximized)
    {
        _geometries.FloatingWindow = new WindowGeometryModel
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
            IsMaximized = isMaximized
        };
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
