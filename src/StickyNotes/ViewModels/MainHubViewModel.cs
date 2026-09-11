using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using StickyNotes.Core.Models.Note;
using StickyNotes.Core.Services.Persistence;
using StickyNotes.Core.Services.Update;

namespace StickyNotes.ViewModels;

/// <summary>
/// Main dashboard / hub ViewModel coordinating notes list, background update checks on startup,
/// search, filter categories, color filtering, dynamic snippet management, and floating NoteWindow lifecycle.
/// </summary>
public sealed class MainHubViewModel : INotifyPropertyChanged
{
    private readonly INotePersistenceService _notePersistence;
    private readonly IUpdateService _updateService;
    private readonly ISettingsService _settingsService;

    public ObservableCollection<NoteModel> AllNotes { get; } = new();
    public ObservableCollection<NoteModel> DisplayedNotes { get; } = new();

    public UpdateViewModel UpdateVm { get; }

    private string _searchQuery = string.Empty;
    private string _currentFilter = "all";
    private string _selectedColorFilter = string.Empty;
    private bool _isLoadingNotes = false;
    private bool _isGridView = true;
    private bool _isFloatingWindowVisible = false;
    private NoteModel? _activeNote;
    private string _saveStatusText = "Saved";
    private bool _isSaving = false;
    private System.Threading.Timer? _autoSaveTimer;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private readonly IWindowGeometryService? _geometryService;
    public IWindowGeometryService? GeometryService => _geometryService;

    public MainHubViewModel(
        INotePersistenceService notePersistence,
        IUpdateService updateService,
        ISettingsService settingsService,
        IWindowGeometryService? geometryService = null)
    {
        _notePersistence = notePersistence;
        _updateService = updateService;
        _settingsService = settingsService;
        _geometryService = geometryService;

        UpdateVm = new UpdateViewModel(_updateService);

        NewNoteCommand = new AsyncRelayCommand(CreateNewNoteAsync);
        AddSnippetBoxCommand = new RelayCommand(AddSnippetBoxToActiveNote);
    }

    public string NotesCountText => $"{DisplayedNotes.Count} {(DisplayedNotes.Count == 1 ? "note" : "notes")}";
    public int AllCount => AllNotes.Count(n => !n.IsDeleted);
    public int PinnedCount => AllNotes.Count(n => n.IsPinned && !n.IsDeleted);
    public int TrashCount => AllNotes.Count(n => n.IsDeleted);

    public string CurrentCategoryTitle
    {
        get
        {
            if (!string.IsNullOrEmpty(_selectedColorFilter))
                return $"{char.ToUpper(_selectedColorFilter[0])}{_selectedColorFilter[1..]} Notes";
            return _currentFilter switch
            {
                "pinned" => "Pinned Notes",
                "trash" => "Trash Notes",
                _ => "All Sticky Notes"
            };
        }
    }

    public bool HasActiveFilter => _currentFilter != "all" || !string.IsNullOrEmpty(_selectedColorFilter);

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery != value)
            {
                _searchQuery = value;
                OnPropertyChanged();
                ApplyFilterAndSearch();
            }
        }
    }

    public bool IsLoadingNotes
    {
        get => _isLoadingNotes;
        private set { _isLoadingNotes = value; OnPropertyChanged(); }
    }

    public bool IsGridView
    {
        get => _isGridView;
        set { if (_isGridView != value) { _isGridView = value; OnPropertyChanged(); } }
    }

    public bool IsFloatingWindowVisible
    {
        get => _isFloatingWindowVisible;
        set { if (_isFloatingWindowVisible != value) { _isFloatingWindowVisible = value; OnPropertyChanged(); } }
    }

    public NoteModel? ActiveNote
    {
        get => _activeNote;
        set
        {
            if (_activeNote != value)
            {
                if (_activeNote != null)
                {
                    _activeNote.PropertyChanged -= OnActiveNotePropertyChanged;
                }
                _activeNote = value;
                if (_activeNote != null)
                {
                    _activeNote.PropertyChanged += OnActiveNotePropertyChanged;
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActiveNoteSnippetCountText));
                OnPropertyChanged(nameof(ActiveNoteCharCountText));
            }
        }
    }

    public string SaveStatusText
    {
        get => _saveStatusText;
        private set { _saveStatusText = value; OnPropertyChanged(); }
    }

    public bool IsSaving
    {
        get => _isSaving;
        private set { _isSaving = value; OnPropertyChanged(); }
    }

    public string ActiveNoteSnippetCountText =>
        $"{ActiveNote?.Snippets?.Count ?? 0} {((ActiveNote?.Snippets?.Count ?? 0) == 1 ? "snippet box" : "snippet boxes")}";

    public string ActiveNoteCharCountText =>
        $"{((ActiveNote?.Title?.Length ?? 0) + (ActiveNote?.Content?.Length ?? 0))} characters";

    public ICommand NewNoteCommand { get; }
    public ICommand AddSnippetBoxCommand { get; }

    public async Task InitializeAsync()
    {
        IsLoadingNotes = true;
        try
        {
            var notes = await _notePersistence.LoadAllNotesAsync();
            AllNotes.Clear();
            foreach (var note in notes.Where(n => !n.IsDeleted))
            {
                AllNotes.Add(note);
            }

            if (AllNotes.Count > 0)
            {
                ActiveNote = AllNotes[0];
            }

            ApplyFilterAndSearch();
        }
        finally
        {
            IsLoadingNotes = false;
        }

        var settings = _settingsService.GetUpdateSettings();
        if (settings.AutoCheckUpdates)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(800);
                await _updateService.CheckForUpdatesAsync(force: false);
            });
        }
    }

    public void OpenNoteInEditor(NoteModel note)
    {
        ActiveNote = note;
        IsFloatingWindowVisible = true;
    }

    public void AddSnippetBoxToActiveNote()
    {
        if (ActiveNote == null) return;
        var newBox = new SnippetBoxModel
        {
            Type = "CMD",
            Label = "Command",
            Content = "// Enter command or snippet here...",
            OrderIndex = ActiveNote.Snippets.Count
        };
        newBox.PropertyChanged += (s, e) => TriggerAutoSave();
        ActiveNote.Snippets.Add(newBox);
        OnPropertyChanged(nameof(ActiveNoteSnippetCountText));
        TriggerAutoSave();
    }

    public void RemoveSnippetBoxFromActiveNote(SnippetBoxModel snippet)
    {
        if (ActiveNote == null) return;
        ActiveNote.Snippets.Remove(snippet);
        OnPropertyChanged(nameof(ActiveNoteSnippetCountText));
        TriggerAutoSave();
    }

    public void SetActiveNoteTheme(string colorTheme)
    {
        if (ActiveNote == null) return;
        ActiveNote.ColorTheme = colorTheme;
        TriggerAutoSave();
    }

    public void ToggleActiveNotePin()
    {
        if (ActiveNote == null) return;
        ActiveNote.IsPinned = !ActiveNote.IsPinned;
        TriggerAutoSave();
        UpdateBadgeCounts();
    }

    public void ToggleActiveNoteAlwaysOnTop()
    {
        if (ActiveNote == null) return;
        ActiveNote.IsAlwaysOnTop = !ActiveNote.IsAlwaysOnTop;
        TriggerAutoSave();
    }

    private void OnActiveNotePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ActiveNoteCharCountText));
        TriggerAutoSave();
    }

    private void TriggerAutoSave()
    {
        if (ActiveNote == null) return;

        IsSaving = true;
        SaveStatusText = "Saving...";

        _autoSaveTimer?.Dispose();
        _autoSaveTimer = new System.Threading.Timer(async _ =>
        {
            if (ActiveNote != null)
            {
                await _notePersistence.SaveNoteAsync(ActiveNote);
            }
            App.CurrentAppSynchronizationContext?.Post(__ =>
            {
                IsSaving = false;
                SaveStatusText = "Saved";
                UpdateBadgeCounts();
            }, null);
        }, null, 500, Timeout.Infinite);
    }

    public async Task DeleteNoteAsync(NoteModel note)
    {
        note.IsDeleted = true;
        await _notePersistence.DeleteNoteAsync(note.Id);
        AllNotes.Remove(note);
        if (ActiveNote == note)
        {
            ActiveNote = AllNotes.FirstOrDefault();
            if (ActiveNote == null) IsFloatingWindowVisible = false;
        }
        ApplyFilterAndSearch();
        UpdateBadgeCounts();
    }

    public async Task SaveNoteAsync(NoteModel note)
    {
        note.ModifiedAt = DateTimeOffset.UtcNow;
        await _notePersistence.SaveNoteAsync(note);
        ApplyFilterAndSearch();
        UpdateBadgeCounts();
    }

    public async Task TogglePinAsync(NoteModel note)
    {
        note.IsPinned = !note.IsPinned;
        note.IsAlwaysOnTop = note.IsPinned;
        await _notePersistence.SaveNoteAsync(note);
        ApplyFilterAndSearch();
        UpdateBadgeCounts();
    }

    public async Task CreateNewNoteAsync()
    {
        var newNote = new NoteModel
        {
            Title = "Untitled Note",
            Content = "Type note text or instructions...",
            ColorTheme = "yellow",
            Category = "Work",
            Snippets = new ObservableCollection<SnippetBoxModel>
            {
                new() { Type = "CMD", Label = "Snippet", Content = "dotnet run" }
            }
        };

        await _notePersistence.SaveNoteAsync(newNote);
        AllNotes.Insert(0, newNote);
        ActiveNote = newNote;
        IsFloatingWindowVisible = true;
        ApplyFilterAndSearch();
        UpdateBadgeCounts();
    }

    public void FilterCategory(string category)
    {
        _currentFilter = category.ToLowerInvariant();
        _selectedColorFilter = string.Empty;
        ApplyFilterAndSearch();
    }

    public void FilterByColor(string color)
    {
        if (_selectedColorFilter.Equals(color, StringComparison.OrdinalIgnoreCase))
        {
            _selectedColorFilter = string.Empty;
        }
        else
        {
            _selectedColorFilter = color.ToLowerInvariant();
        }
        ApplyFilterAndSearch();
    }

    private void UpdateBadgeCounts()
    {
        OnPropertyChanged(nameof(AllCount));
        OnPropertyChanged(nameof(PinnedCount));
        OnPropertyChanged(nameof(TrashCount));
        OnPropertyChanged(nameof(NotesCountText));
    }

    private void ApplyFilterAndSearch()
    {
        DisplayedNotes.Clear();
        var query = _searchQuery.Trim().ToLowerInvariant();

        var filtered = AllNotes.Where(n =>
        {
            if (!string.IsNullOrEmpty(_selectedColorFilter) && !n.ColorTheme.Equals(_selectedColorFilter, StringComparison.OrdinalIgnoreCase))
                return false;

            if (_currentFilter == "pinned" && !n.IsPinned) return false;
            if (_currentFilter == "trash" && !n.IsDeleted) return false;

            if (string.IsNullOrEmpty(query)) return true;

            var inTitle = n.Title.ToLowerInvariant().Contains(query);
            var inContent = n.Content.ToLowerInvariant().Contains(query);
            var inSnippets = n.Snippets.Any(s => s.Content.ToLowerInvariant().Contains(query) || s.Label.ToLowerInvariant().Contains(query));

            return inTitle || inContent || inSnippets;
        });

        foreach (var item in filtered)
        {
            DisplayedNotes.Add(item);
        }

        OnPropertyChanged(nameof(CurrentCategoryTitle));
        OnPropertyChanged(nameof(HasActiveFilter));
        UpdateBadgeCounts();
    }
}
