using System.Text.Json;

namespace StickyNotes.Core.Common;

/// <summary>
/// Pre-configured standard JSON serializer options used across all persistence and networking layers.
/// </summary>
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}
