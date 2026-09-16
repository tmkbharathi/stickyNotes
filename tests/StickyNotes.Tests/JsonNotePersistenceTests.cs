using System.Collections.ObjectModel;
using StickyNotes.Core.Models.Note;
using StickyNotes.Core.Services.Logging;
using StickyNotes.Core.Services.Persistence;
using Xunit;

namespace StickyNotes.Tests;

public class JsonNotePersistenceTests : IDisposable
{
    private readonly string _testDir;
    private readonly IUpdateLogger _logger;

    public JsonNotePersistenceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "StickyNotesTests_Persistence_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _logger = new UpdateLogger(_testDir);
    }

    [Fact]
    public async Task FreshInstall_GeneratesSeedNotes_AndPersistsToFile()
    {
        var service = new JsonNotePersistenceService(_testDir, _logger);
        var notes = await service.LoadAllNotesAsync();

        Assert.NotEmpty(notes);
        Assert.True(File.Exists(Path.Combine(_testDir, "stickynotes.json")));
        Assert.Equal(notes.Count, service.GetActiveNoteCount());
    }

    [Fact]
    public async Task SaveNote_CreatesAtomicBackup_AndRetainsEdits()
    {
        var service = new JsonNotePersistenceService(_testDir, _logger);
        await service.LoadAllNotesAsync();

        var newNote = new NoteModel
        {
            Id = "test-note-1",
            Title = "Architecture Test",
            Content = "Testing persistence atomic swap",
            ColorTheme = "pink",
            Category = "Testing",
            Snippets = new ObservableCollection<SnippetBoxModel>
            {
                new() { Id = "snip-1", Type = "CMD", Label = "Test", Content = "dotnet test" }
            }
        };

        var saved = await service.SaveNoteAsync(newNote);
        Assert.True(saved);

        // Verify backup was created
        var backupPath = Path.Combine(_testDir, "stickynotes.backup.json");
        Assert.True(File.Exists(backupPath));

        // Reload in a fresh instance
        var reloadedService = new JsonNotePersistenceService(_testDir, _logger);
        var loadedNotes = await reloadedService.LoadAllNotesAsync();
        var loadedNote = await reloadedService.GetNoteByIdAsync("test-note-1");

        Assert.NotNull(loadedNote);
        Assert.Equal("Architecture Test", loadedNote.Title);
        Assert.Equal("pink", loadedNote.ColorTheme);
        Assert.Single(loadedNote.Snippets);
    }

    [Fact]
    public async Task CorruptedPrimaryFile_RecoversAutomaticallyFromBackup()
    {
        var service = new JsonNotePersistenceService(_testDir, _logger);
        await service.LoadAllNotesAsync();

        var note = new NoteModel
        {
            Id = "recovery-note",
            Title = "Recovery Target Note",
            Content = "Important note data"
        };
        // Perform two saves so the backup file contains the recovery-note
        await service.SaveNoteAsync(note);
        note.Content = "Important note data (updated)";
        await service.SaveNoteAsync(note);

        var primaryFile = Path.Combine(_testDir, "stickynotes.json");
        var backupFile = Path.Combine(_testDir, "stickynotes.backup.json");
        Assert.True(File.Exists(backupFile));

        // Corrupt the primary file with invalid JSON syntax
        await File.WriteAllTextAsync(primaryFile, "{ invalid_json: [[[");

        // Create new service to simulate restart and recovery
        var recoveryService = new JsonNotePersistenceService(_testDir, _logger);
        var recoveredNotes = await recoveryService.LoadAllNotesAsync();

        Assert.NotEmpty(recoveredNotes);
        Assert.Contains(recoveredNotes, n => n.Id == "recovery-note" || n.Title == "Recovery Target Note");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch { }
    }
}
