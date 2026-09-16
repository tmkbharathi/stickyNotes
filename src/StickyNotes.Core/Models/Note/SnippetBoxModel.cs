using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace StickyNotes.Core.Models.Note;

/// <summary>
/// Represents an isolated, copyable code, command, path, or configuration snippet inside a sticky note.
/// </summary>
public sealed class SnippetBoxModel : INotifyPropertyChanged
{
    public static readonly string[] AvailableTypes = new[] { "CMD", "GIT", "PATH", "C#", "JSON", "SQL", "URL", "FIGMA" };

    [JsonIgnore]
    public string[] TypeOptions => AvailableTypes;

    private string _id = Guid.NewGuid().ToString("N");
    private string _type = "CMD"; // CMD, GIT, PATH, JSON, C#, SQL, URL, FIGMA
    private string _label = "Command";
    private string _content = string.Empty;
    private bool _isMultiline = false;
    private bool _isMasked = false;
    private int _orderIndex = 0;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    [JsonPropertyName("id")]
    public string Id
    {
        get => _id;
        set { if (_id != value) { _id = value; OnPropertyChanged(); } }
    }

    [JsonPropertyName("type")]
    public string Type
    {
        get => _type;
        set
        {
            if (_type != value)
            {
                _type = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayLabel));
                OnPropertyChanged(nameof(IsSeparator));
                OnPropertyChanged(nameof(IsLabel));
                OnPropertyChanged(nameof(IsDescription));
                OnPropertyChanged(nameof(IsSnippet));
            }
        }
    }

    [JsonIgnore]
    public bool IsSeparator => string.Equals(_type, "SEPARATOR", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsLabel => string.Equals(_type, "LABEL", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsDescription => string.Equals(_type, "DESC", StringComparison.OrdinalIgnoreCase) || string.Equals(_type, "DESCRIPTION", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsSnippet => !IsSeparator && !IsLabel && !IsDescription;

    [JsonPropertyName("label")]
    public string Label
    {
        get => _label;
        set { if (_label != value) { _label = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayLabel)); } }
    }

    [JsonIgnore]
    public string DisplayLabel => string.IsNullOrWhiteSpace(_label) || _label.Equals(_type, StringComparison.OrdinalIgnoreCase) ? "Snippet" : _label;

    [JsonPropertyName("content")]
    public string Content
    {
        get => _content;
        set
        {
            if (_content != value)
            {
                _content = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayContent));
            }
        }
    }

    [JsonPropertyName("isMasked")]
    public bool IsMasked
    {
        get => _isMasked;
        set
        {
            if (_isMasked != value)
            {
                _isMasked = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayContent));
                OnPropertyChanged(nameof(MaskGlyph));
                OnPropertyChanged(nameof(MaskToolTip));
            }
        }
    }

    [JsonIgnore]
    public string DisplayContent => IsMasked ? new string('•', string.IsNullOrEmpty(_content) ? 8 : Math.Min(16, Math.Max(8, _content.Length))) : _content;

    [JsonIgnore]
    public string MaskGlyph => IsMasked ? "\uED1A" : "\uE7B3";

    [JsonIgnore]
    public string MaskToolTip => IsMasked ? "Unhide content" : "Hide content";

    [JsonPropertyName("isMultiline")]
    public bool IsMultiline
    {
        get => _isMultiline;
        set { if (_isMultiline != value) { _isMultiline = value; OnPropertyChanged(); } }
    }

    [JsonPropertyName("orderIndex")]
    public int OrderIndex
    {
        get => _orderIndex;
        set { if (_orderIndex != value) { _orderIndex = value; OnPropertyChanged(); } }
    }

    public SnippetBoxModel Clone()
    {
        return new SnippetBoxModel
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = this.Type,
            Label = this.Label,
            Content = this.Content,
            IsMultiline = this.IsMultiline,
            IsMasked = this.IsMasked,
            OrderIndex = this.OrderIndex
        };
    }
}

