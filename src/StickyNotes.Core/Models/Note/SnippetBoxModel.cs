using System.Text.Json.Serialization;

namespace StickyNotes.Core.Models.Note;

/// <summary>
/// Represents an isolated, copyable code, command, path, or configuration snippet inside a sticky note.
/// </summary>
public sealed class SnippetBoxModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("type")]
    public string Type { get; set; } = "CMD"; // CMD, GIT, PATH, JSON, C#, SQL, URL, FIGMA

    [JsonPropertyName("label")]
    public string Label { get; set; } = "Command";

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("isMultiline")]
    public bool IsMultiline { get; set; } = false;

    [JsonPropertyName("orderIndex")]
    public int OrderIndex { get; set; } = 0;
}
