using System.Collections.Concurrent;
using System.Text.Json;
using StickyNotes.Core.Common;
using StickyNotes.Core.Models.Note;
using StickyNotes.Core.Services.Logging;

namespace StickyNotes.Core.Services.Persistence;

/// <summary>
/// Production JSON-based thread-safe persistence implementation with atomic writes and backup guarantees.
/// </summary>
public sealed class JsonNotePersistenceService : INotePersistenceService
{
    private readonly string _storageDirectory;
    private readonly string _notesFilePath;
    private readonly string _backupFilePath;
    private readonly IUpdateLogger _logger;
    private readonly ConcurrentDictionary<string, NoteModel> _activeNotes = new();
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = JsonDefaults.Options;

    public JsonNotePersistenceService(string storageDirectory, IUpdateLogger logger)
    {
        _storageDirectory = storageDirectory;
        _logger = logger;
        Directory.CreateDirectory(_storageDirectory);
        _notesFilePath = Path.Combine(_storageDirectory, "stickynotes.json");
        _backupFilePath = Path.Combine(_storageDirectory, "stickynotes.backup.json");
    }

    public async Task<IReadOnlyList<NoteModel>> LoadAllNotesAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_notesFilePath))
            {
                // Generate seed notes if clean install
                var seedNotes = CreateDefaultSeedNotes();
                await SaveInternalAsync(seedNotes, cancellationToken);
                foreach (var note in seedNotes)
                {
                    _activeNotes[note.Id] = note;
                }
                return seedNotes;
            }

            var json = await File.ReadAllTextAsync(_notesFilePath, cancellationToken);
            var notes = JsonSerializer.Deserialize<List<NoteModel>>(json) ?? new List<NoteModel>();

            _activeNotes.Clear();
            foreach (var note in notes)
            {
                _activeNotes[note.Id] = note;
            }

            return notes;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to load notes from main storage. Attempting backup recovery.", ex);
            return await TryRecoverFromBackupAsync(cancellationToken);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task<IReadOnlyList<NoteModel>> TryRecoverFromBackupAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(_backupFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_backupFilePath, cancellationToken);
                var backupNotes = JsonSerializer.Deserialize<List<NoteModel>>(json) ?? new List<NoteModel>();
                _logger.LogInfo($"Successfully restored {backupNotes.Count} notes from backup file.");
                return backupNotes;
            }
            catch (Exception ex)
            {
                _logger.LogError("Backup recovery failed as well.", ex);
            }
        }
        return CreateDefaultSeedNotes();
    }

    public Task<NoteModel?> GetNoteByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _activeNotes.TryGetValue(id, out var note);
        return Task.FromResult(note);
    }

    public async Task<bool> SaveNoteAsync(NoteModel note, CancellationToken cancellationToken = default)
    {
        note.ModifiedAt = DateTimeOffset.UtcNow;
        _activeNotes[note.Id] = note;
        return await FlushAllPendingAsync(cancellationToken);
    }

    public async Task<bool> FlushAllPendingAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var notesList = _activeNotes.Values.ToList();
            await SaveInternalAsync(notesList, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to flush pending notes to disk.", ex);
            return false;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<bool> DeleteNoteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (_activeNotes.TryGetValue(id, out var note))
        {
            note.IsDeleted = true;
            note.ModifiedAt = DateTimeOffset.UtcNow;
            return await FlushAllPendingAsync(cancellationToken);
        }
        return false;
    }

    public int GetActiveNoteCount() => _activeNotes.Values.Count(n => !n.IsDeleted);

    public void RegisterActiveNote(NoteModel note) => _activeNotes[note.Id] = note;
    public void UnregisterActiveNote(string id) => _activeNotes.TryRemove(id, out _);

    private async Task SaveInternalAsync(List<NoteModel> notes, CancellationToken cancellationToken)
    {
        var tempFile = _notesFilePath + ".tmp";
        var json = JsonSerializer.Serialize(notes, JsonOptions);

        await File.WriteAllTextAsync(tempFile, json, cancellationToken);

        // Maintain atomic swap
        if (File.Exists(_notesFilePath))
        {
            File.Copy(_notesFilePath, _backupFilePath, overwrite: true);
        }

        File.Move(tempFile, _notesFilePath, overwrite: true);
    }

    private static List<NoteModel> CreateDefaultSeedNotes()
    {
        return new List<NoteModel>
        {
            new()
            {
                Id = "seed-1",
                Title = "Sprint 42 Goals & Release Tasks",
                Content = "Production deployment steps and build verification commands:",
                ColorTheme = "yellow",
                Category = "Work",
                IsPinned = true,
                IsOpenInWindow = true,
                Snippets = new System.Collections.ObjectModel.ObservableCollection<SnippetBoxModel>
                {
                    new() { Id = "s-1", Type = "CMD", Label = "Build Command", Content = "dotnet publish -c Release -r win-x64 --self-contained", OrderIndex = 0 },
                    new() { Id = "s-2", Type = "PATH", Label = "Package Path", Content = @"C:\Build\Packages\Release\WinUI3.msix", OrderIndex = 1 }
                }
            },
            new()
            {
                Id = "seed-2",
                Title = "WinUI 3 AppWindow Init",
                Content = "Essential window lifecycle snippet for standalone Mica popouts:",
                ColorTheme = "blue",
                Category = "Dev",
                IsPinned = false,
                Snippets = new System.Collections.ObjectModel.ObservableCollection<SnippetBoxModel>
                {
                    new() { Id = "s-3", Type = "C#", Label = "Window Presenter", Content = "var presenter = OverlappedPresenter.Create();\npresenter.IsAlwaysOnTop = true;\npresenter.SetBorderAndTitleBar(true, true);", IsMultiline = true, OrderIndex = 0 }
                }
            },
            new()
            {
                Id = "seed-3",
                Title = "Weekend Shopping & Supplies",
                Content = "☑ Whole grain sourdough\n☑ Oat milk & espresso beans\n☐ Dual-monitor HDMI cable\n☐ Microfiber cleaning cloths",
                ColorTheme = "green",
                Category = "Personal",
                IsPinned = false
            }
        };
    }
}
