using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using StickyNotes.Core.Models.Note;
using StickyNotes.Core.Services.Persistence;
using StickyNotes.Helpers;
using WinRT.Interop;

namespace StickyNotes.Views;

/// <summary>
/// Dedicated read-only presentation and copy floating sticky note window.
/// </summary>
public sealed partial class NoteWindow : Window
{
    private readonly INotePersistenceService? _persistence;
    private readonly IWindowGeometryService? _geometryService;
    private readonly Action<NoteModel>? _onNoteUpdated;
    private readonly Action<NoteModel>? _onNewNoteRequested;
    private OverlappedPresenter? _presenter;
    private AppWindow? _appWindow;

    public NoteModel Note { get; }

    public NoteWindow(
        NoteModel note,
        INotePersistenceService? persistence = null,
        IWindowGeometryService? geometryService = null,
        Action<NoteModel>? onNoteUpdated = null,
        Action<NoteModel>? onNewNoteRequested = null)
    {
        Note = note;
        _persistence = persistence;
        _geometryService = geometryService;
        _onNoteUpdated = onNoteUpdated;
        _onNewNoteRequested = onNewNoteRequested;

        this.InitializeComponent();

        FloatingView.Note = Note;
        FloatingView.PinToggleRequested += (s, e) => OnTogglePin();
        FloatingView.MinimizeRequested += (s, e) => _presenter?.Minimize();
        FloatingView.CloseRequested += (s, e) => this.Close();
        FloatingView.HeaderDragRequested += (s, e) => OnHeaderDrag();

        ConfigureAppWindow();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int wMsg, int wParam, int lParam);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    private void ConfigureAppWindow()
    {
        var savedGeo = _geometryService?.GetFloatingWindowGeometry();

        _appWindow = WindowPlacementHelper.InitializeAndTrackWindow(
            this,
            savedGeo!,
            (x, y, w, h, isMax) =>
            {
                _geometryService?.SaveFloatingWindowGeometry(x, y, w, h, isMax);
            },
            defaultWidth: 340,
            defaultHeight: 480,
            minWidth: 240,
            minHeight: 180);

        if (_appWindow != null)
        {
            _presenter = _appWindow.Presenter as OverlappedPresenter;
            if (_presenter != null)
            {
                _presenter.IsAlwaysOnTop = Note.IsAlwaysOnTop || Note.IsPinned;
                _presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
                _presenter.IsResizable = true;
            }
        }
    }

    private void OnHeaderDrag()
    {
        var hWnd = WindowNative.GetWindowHandle(this);
        ReleaseCapture();
        SendMessage(hWnd, 0xA1, 0x2, 0); // WM_NCLBUTTONDOWN, HT_CAPTION
    }

    private void OnTogglePin()
    {
        Note.IsPinned = !Note.IsPinned;
        Note.IsAlwaysOnTop = Note.IsPinned;

        if (_presenter != null)
        {
            _presenter.IsAlwaysOnTop = Note.IsPinned;
        }

        _onNoteUpdated?.Invoke(Note);
    }
}
