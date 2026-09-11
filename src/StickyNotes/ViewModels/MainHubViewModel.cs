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
/// search, filter categories, and independent NoteWindow lifecycle.
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
    private bool _isLoadingNotes = false;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public MainHubViewModel(
        INotePersistenceService notePersistence,
        IUpdateService updateService,
        ISettingsService settingsService)
    {
        _notePersistence = notePersistence;
        _updateService = updateService;
        _settingsService = settingsService;

        UpdateVm = new UpdateViewModel(_updateService);

        NewNoteCommand = new AsyncRelayCommand(CreateNewNoteAsync);
        DeleteNoteCommand = new AsyncRelayCommand(async () => { });
    }

    public string NotesCountText => $"{DisplayedNotes.Count} {(DisplayedNotes.Count == 1 ? "note" : "notes")}";

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

    public ICommand NewNoteCommand { get; }
    public ICommand DeleteNoteCommand { get; }

    /// <summary>
    /// FAST STARTUP WORKFLOW:
    /// 1. Open application immediately
    /// 2. Load notes immediately
    /// 3. Start update check asynchronously in the background (non-blocking)
    /// </summary>
    public async Task InitializeAsync()
    {
        IsLoadingNotes = true;
        try
        {
            // 1 & 2. Immediate notes retrieval
            var notes = await _notePersistence.LoadAllNotesAsync();
            AllNotes.Clear();
            foreach (var note in notes.Where(n => !n.IsDeleted))
            {
                AllNotes.Add(note);
            }
            ApplyFilterAndSearch();
        }
        finally
        {
            IsLoadingNotes = false;
        }

        // 3. Background non-blocking update check
        var settings = _settingsService.GetUpdateSettings();
        if (settings.AutoCheckUpdates)
        {
            _ = Task.Run(async () =>
            {
                // Give the UI a moment to complete first render smoothly
                await Task.Delay(800);
                await _updateService.CheckForUpdatesAsync(force: false);
            });
        }
    }

    public async Task DeleteNoteAsync(NoteModel note)
    {
        note.IsDeleted = true;
        await _notePersistence.DeleteNoteAsync(note.Id);
        AllNotes.Remove(note);
        ApplyFilterAndSearch();
    }

    public async Task SaveNoteAsync(NoteModel note)
    {
        note.ModifiedAt = DateTimeOffset.UtcNow;
        await _notePersistence.SaveNoteAsync(note);
        ApplyFilterAndSearch();
    }

    public async Task TogglePinAsync(NoteModel note)
    {
        note.IsPinned = !note.IsPinned;
        await _notePersistence.SaveNoteAsync(note);
        ApplyFilterAndSearch();
    }

    public async Task CreateNewNoteAsync()
    {
        var newNote = new NoteModel
        {
            Title = "New Note",
            Content = "Type your note description or instructions here...",
            ColorTheme = "yellow",
            Category = "Work",
            Snippets = new List<SnippetBoxModel>
            {
                new() { Type = "CMD", Label = "Build Command", Content = "dotnet build" }
            }
        };

        await _notePersistence.SaveNoteAsync(newNote);
        AllNotes.Insert(0, newNote);
        ApplyFilterAndSearch();
    }

    public void FilterCategory(string category)
    {
        _currentFilter = category.ToLowerInvariant();
        ApplyFilterAndSearch();
    }

    private void ApplyFilterAndSearch()
    {
        DisplayedNotes.Clear();
        var query = _searchQuery.Trim().ToLowerInvariant();

        var filtered = AllNotes.Where(n =>
        {
            if (_currentFilter == "pinned" && !n.IsPinned) return false;
            if (_currentFilter == "work" && !n.Category.Equals("Work", StringComparison.OrdinalIgnoreCase)) return false;
            if (_currentFilter == "dev" && !n.Category.Equals("Dev", StringComparison.OrdinalIgnoreCase)) return false;
            if (_currentFilter == "personal" && !n.Category.Equals("Personal", StringComparison.OrdinalIgnoreCase)) return false;
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
        OnPropertyChanged(nameof(NotesCountText));
    }
}
