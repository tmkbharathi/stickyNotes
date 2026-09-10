using System.Text.Json.Serialization;

namespace StickyNotes.Core.Models;

/// <summary>
/// Persisted window bounds and layout for MainWindow and all active NoteWindows.
/// </summary>
public sealed class WindowStateModel
{
    [JsonPropertyName("noteId")]
    public string NoteId { get; set; } = string.Empty;

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("width")]
    public double Width { get; set; } = 360;

    [JsonPropertyName("height")]
    public double Height { get; set; } = 540;

    [JsonPropertyName("isAlwaysOnTop")]
    public bool IsAlwaysOnTop { get; set; } = true;

    [JsonPropertyName("isMinimized")]
    public bool IsMinimized { get; set; } = false;
}
