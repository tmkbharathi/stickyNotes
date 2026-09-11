using System.Text.Json.Serialization;

namespace StickyNotes.Core.Models;

/// <summary>
/// Persisted window bounds and state for desktop windows (MainWindow, FloatingWindow).
/// </summary>
public sealed class WindowGeometryModel
{
    [JsonPropertyName("x")]
    public int X { get; set; } = int.MinValue;

    [JsonPropertyName("y")]
    public int Y { get; set; } = int.MinValue;

    [JsonPropertyName("width")]
    public int Width { get; set; } = 0;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 0;

    [JsonPropertyName("isMaximized")]
    public bool IsMaximized { get; set; } = false;

    [JsonIgnore]
    public bool HasValue => Width > 0 && Height > 0 && X != int.MinValue && Y != int.MinValue;
}

/// <summary>
/// Root container holding independent window geometries.
/// </summary>
public sealed class AppWindowGeometries
{
    [JsonPropertyName("mainWindow")]
    public WindowGeometryModel MainWindow { get; set; } = new();

    [JsonPropertyName("floatingWindow")]
    public WindowGeometryModel FloatingWindow { get; set; } = new();
}
