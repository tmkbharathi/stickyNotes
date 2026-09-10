using StickyNotes.Core.Models.Note;

namespace StickyNotes.Core.Services.Persistence;

/// <summary>
/// Handles storage, autosave flushing, and retrieval of sticky notes and code snippets.
/// </summary>
public interface INotePersistenceService
{
    Task<IReadOnlyList<NoteModel>> LoadAllNotesAsync(CancellationToken cancellationToken = default);
    Task<NoteModel?> GetNoteByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<bool> SaveNoteAsync(NoteModel note, CancellationToken cancellationToken = default);
    Task<bool> FlushAllPendingAsync(CancellationToken cancellationToken = default);
    Task<bool> DeleteNoteAsync(string id, CancellationToken cancellationToken = default);
    int GetActiveNoteCount();
    void RegisterActiveNote(NoteModel note);
    void UnregisterActiveNote(string id);
}
