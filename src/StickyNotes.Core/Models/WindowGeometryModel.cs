using System.Text.Json.Serialization;

namespace StickyNotes.Core.Models;

/// <summary>
/// Persisted window bounds and visibility state for desktop windows (MainWindow, FloatingWindow).
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

    [JsonPropertyName("isVisible")]
    public bool IsVisible { get; set; } = false;

    [JsonPropertyName("activeNoteId")]
    public string? ActiveNoteId { get; set; }

    [JsonIgnore]
    public bool HasValue => Width > 0 && Height > 0 && X != int.MinValue && Y != int.MinValue;
}

/// <summary>
/// Root container holding independent window geometries and visibility states.
/// </summary>
public sealed class AppWindowGeometries
{
    [JsonPropertyName("isFirstRun")]
    public bool IsFirstRun { get; set; } = true;

    [JsonPropertyName("mainWindow")]
    public WindowGeometryModel MainWindow { get; set; } = new() { IsVisible = true };

    [JsonPropertyName("floatingWindow")]
    public WindowGeometryModel FloatingWindow { get; set; } = new() { IsVisible = false };
}
