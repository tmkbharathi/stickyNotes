using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace StickyNotes.Core.Models.Note;

/// <summary>
/// Represents a Fluent sticky note with rich content, copyable snippets, and metadata.
/// </summary>
public sealed class NoteModel : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _title = "Untitled Note";
    private string _content = string.Empty;
    private bool _isTitleMasked = false;
    private bool _isContentMasked = false;
    private bool _isContentFirst = false;
    private string _colorTheme = "yellow";
    private string _category = "Work";
    private bool _isPinned = true;
    private bool _isAlwaysOnTop = true;
    private bool _isDeleted = false;
    private bool _isOpenInWindow = false;
    private double? _windowX;
    private double? _windowY;
    private double _windowWidth = 360;
    private double _windowHeight = 540;
    private DateTimeOffset _createdAt = DateTimeOffset.UtcNow;
    private DateTimeOffset _modifiedAt = DateTimeOffset.UtcNow;
    private ObservableCollection<SnippetBoxModel> _snippets = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    [JsonPropertyName("id")]
    public string Id
    {
        get => _id;
        set { if (_id != value) { _id = value; OnPropertyChanged(); } }
    }

    [JsonPropertyName("title")]
    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }
    }

    [JsonPropertyName("isTitleMasked")]
    public bool IsTitleMasked
    {
        get => _isTitleMasked;
        set
        {
            if (_isTitleMasked != value)
            {
                _isTitleMasked = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayTitle));
                OnPropertyChanged(nameof(TitleMaskGlyph));
                OnPropertyChanged(nameof(TitleMaskToolTip));
            }
        }
    }

    [JsonIgnore]
    public string DisplayTitle
    {
        get
        {
            if (IsTitleMasked) return new string('•', string.IsNullOrWhiteSpace(_title) ? 8 : Math.Min(16, Math.Max(8, _title.Length)));
            return string.IsNullOrWhiteSpace(_title) ? "Untitled Note" : _title;
        }
    }

    [JsonIgnore]
    public string TitleMaskGlyph => IsTitleMasked ? "\uED1A" : "\uE7B3";

    [JsonIgnore]
    public string TitleMaskToolTip => IsTitleMasked ? "Unhide title" : "Hide title";

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

    [JsonPropertyName("isContentMasked")]
    public bool IsContentMasked
    {
        get => _isContentMasked;
        set
        {
            if (_isContentMasked != value)
            {
                _isContentMasked = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayContent));
                OnPropertyChanged(nameof(ContentMaskGlyph));
                OnPropertyChanged(nameof(ContentMaskToolTip));
            }
        }
    }

    [JsonPropertyName("isContentFirst")]
    public bool IsContentFirst
    {
        get => _isContentFirst;
        set
        {
            if (_isContentFirst != value)
            {
                _isContentFirst = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonIgnore]
    public string DisplayContent
    {
        get
        {
            if (IsContentMasked) return new string('•', string.IsNullOrWhiteSpace(_content) ? 8 : Math.Min(24, Math.Max(8, _content.Length)));
            return _content;
        }
    }

    [JsonIgnore]
    public string ContentMaskGlyph => IsContentMasked ? "\uED1A" : "\uE7B3";

    [JsonIgnore]
    public string ContentMaskToolTip => IsContentMasked ? "Unhide description" : "Hide description";

    [JsonPropertyName("colorTheme")]
    public string ColorTheme
    {
        get => _colorTheme;
        set { if (_colorTheme != value) { _colorTheme = value; OnPropertyChanged(); } }
    }

    [JsonPropertyName("category")]
    public string Category
    {
        get => _category;
        set { if (_category != value) { _category = value; OnPropertyChanged(); } }
    }

    [JsonPropertyName("isPinned")]
    public bool IsPinned
    {
        get => _isPinned;
        set { if (_isPinned != value) { _isPinned = value; OnPropertyChanged(); } }
    }

    [JsonPropertyName("isAlwaysOnTop")]
    public bool IsAlwaysOnTop
    {
        get => _isAlwaysOnTop;
        set { if (_isAlwaysOnTop != value) { _isAlwaysOnTop = value; OnPropertyChanged(); } }
    }

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted
    {
        get => _isDeleted;
        set { if (_isDeleted != value) { _isDeleted = value; OnPropertyChanged(); } }
    }

    [JsonPropertyName("isOpenInWindow")]
    public bool IsOpenInWindow
    {
        get => _isOpenInWindow;
        set { if (_isOpenInWindow != value) { _isOpenInWindow = value; OnPropertyChanged(); } }
    }

    [JsonPropertyName("windowX")]
    public double? WindowX
    {
        get => _windowX;
        set { if (_windowX != value) { _windowX = value; OnPropertyChanged(); } }
    }

    [JsonPropertyName("windowY")]
    public double? WindowY
    {
        get => _windowY;
        set { if (_windowY != value) { _windowY = value; OnPropertyChanged(); } }
    }

    [JsonPropertyName("windowWidth")]
    public double WindowWidth
    {
        get => _windowWidth;
        set { if (_windowWidth != value) { _windowWidth = value; OnPropertyChanged(); } }
    }

    [JsonPropertyName("windowHeight")]
    public double WindowHeight
    {
        get => _windowHeight;
        set { if (_windowHeight != value) { _windowHeight = value; OnPropertyChanged(); } }
    }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt
    {
        get => _createdAt;
        set { if (_createdAt != value) { _createdAt = value; OnPropertyChanged(); } }
    }

    [JsonPropertyName("modifiedAt")]
    public DateTimeOffset ModifiedAt
    {
        get => _modifiedAt;
        set { if (_modifiedAt != value) { _modifiedAt = value; OnPropertyChanged(); } }
    }

    [JsonPropertyName("snippets")]
    public ObservableCollection<SnippetBoxModel> Snippets
    {
        get => _snippets;
        set { if (_snippets != value) { _snippets = value; OnPropertyChanged(); OnPropertyChanged(nameof(SnippetsCountText)); } }
    }

    [JsonIgnore]
    public string SnippetsCountText => Snippets.Count switch
    {
        0 => string.Empty,
        1 => "1 copyable block",
        _ => $"{Snippets.Count} copyable blocks"
    };
}
