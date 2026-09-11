using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using StickyNotes.Core.Models.Note;
using StickyNotes.Core.Services.Persistence;
using WinRT.Interop;

namespace StickyNotes.Views;

public sealed partial class NoteWindow : Window
{
    private readonly INotePersistenceService? _persistence;
    private readonly Action<NoteModel>? _onNoteUpdated;
    private readonly Action<NoteModel>? _onNewNoteRequested;
    private OverlappedPresenter? _presenter;
    private AppWindow? _appWindow;

    public NoteModel Note { get; }

    public NoteWindow(
        NoteModel note,
        INotePersistenceService? persistence = null,
        Action<NoteModel>? onNoteUpdated = null,
        Action<NoteModel>? onNewNoteRequested = null)
    {
        Note = note;
        _persistence = persistence;
        _onNoteUpdated = onNoteUpdated;
        _onNewNoteRequested = onNewNoteRequested;

        this.InitializeComponent();

        EditorControl.Note = Note;
        EditorControl.NoteSaved += async (s, n) =>
        {
            if (_persistence != null)
            {
                await _persistence.SaveNoteAsync(n);
            }
            _onNoteUpdated?.Invoke(n);
        };
        EditorControl.NewNoteRequested += (s, e) =>
        {
            _onNewNoteRequested?.Invoke(Note);
        };

        ConfigureAppWindow();
    }

    private void ConfigureAppWindow()
    {
        var hWnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow != null)
        {
            _presenter = _appWindow.Presenter as OverlappedPresenter;
            if (_presenter != null)
            {
                _presenter.IsAlwaysOnTop = Note.IsAlwaysOnTop;
            }

            _appWindow.Resize(new Windows.Graphics.SizeInt32(360, 540));
        }

        this.ExtendsContentIntoTitleBar = true;
        this.SetTitleBar(EditorControl.TitleBarElement);
    }
}
