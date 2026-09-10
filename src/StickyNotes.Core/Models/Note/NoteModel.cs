using System.Text.Json.Serialization;

namespace StickyNotes.Core.Models.Note;

/// <summary>
/// Represents a Fluent sticky note with rich content, copyable snippets, and metadata.
/// </summary>
public sealed class NoteModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("title")]
    public string Title { get; set; } = "Untitled Note";

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("colorTheme")]
    public string ColorTheme { get; set; } = "yellow"; // yellow, blue, green, pink, purple, gray

    [JsonPropertyName("category")]
    public string Category { get; set; } = "Work"; // Work, Dev, Personal, Design, Study, Sync

    [JsonPropertyName("isPinned")]
    public bool IsPinned { get; set; } = false;

    [JsonPropertyName("isAlwaysOnTop")]
    public bool IsAlwaysOnTop { get; set; } = true;

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; } = false;

    [JsonPropertyName("isOpenInWindow")]
    public bool IsOpenInWindow { get; set; } = false;

    [JsonPropertyName("windowX")]
    public double? WindowX { get; set; }

    [JsonPropertyName("windowY")]
    public double? WindowY { get; set; }

    [JsonPropertyName("windowWidth")]
    public double WindowWidth { get; set; } = 360;

    [JsonPropertyName("windowHeight")]
    public double WindowHeight { get; set; } = 540;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("modifiedAt")]
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("snippets")]
    public List<SnippetBoxModel> Snippets { get; set; } = new();
}
