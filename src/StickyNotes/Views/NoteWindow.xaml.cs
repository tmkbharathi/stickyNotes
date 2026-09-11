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

    public NoteModel Note { get; private set; }

    public bool IsVisibleOnScreen => _appWindow?.IsVisible ?? false;

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
        FloatingView.CloseRequested += (s, e) => HideToTray();
        FloatingView.HeaderDragRequested += (s, e) => OnHeaderDrag();

        ConfigureAppWindow();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int wMsg, int wParam, int lParam);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private void ConfigureAppWindow()
    {
        var savedGeo = _geometryService?.GetFloatingWindowGeometry();

        _appWindow = WindowPlacementHelper.InitializeAndTrackWindow(
            this,
            savedGeo!,
            (x, y, w, h, isMax) =>
            {
                _geometryService?.SaveFloatingWindowGeometry(x, y, w, h, isMax, isVisible: IsVisibleOnScreen, activeNoteId: Note.Id);
            },
            defaultWidth: 340,
            defaultHeight: 480,
            minWidth: 260,
            minHeight: 220);

        WindowMinSizeHelper.SetMinSize(this, minWidthDip: 260, minHeightDip: 220);

        if (_appWindow != null)
        {
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "appIcon.ico");
            if (System.IO.File.Exists(iconPath))
            {
                _appWindow.SetIcon(iconPath);
            }

            _presenter = _appWindow.Presenter as OverlappedPresenter;
            if (_presenter != null)
            {
                _presenter.IsAlwaysOnTop = Note.IsAlwaysOnTop || Note.IsPinned;
                _presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
                _presenter.IsResizable = true;
            }

            // Intercept close button on appWindow
            _appWindow.Closing += (s, e) =>
            {
                e.Cancel = true;
                HideToTray();
            };
        }
    }

    public void ShowAndFocus()
    {
        if (_appWindow != null)
        {
            _appWindow.Show();
            _appWindow.MoveInZOrderAtTop();
        }

        var hWnd = WindowNative.GetWindowHandle(this);
        ShowWindow(hWnd, 9 /* SW_RESTORE */);
        SetForegroundWindow(hWnd);

        _geometryService?.SetFloatingWindowVisibility(true, Note.Id);
    }

    public void HideToTray()
    {
        _appWindow?.Hide();
        _geometryService?.SetFloatingWindowVisibility(false, Note.Id);
    }

    public void UpdateNote(NoteModel note)
    {
        Note = note;
        FloatingView.Note = note;
        if (IsVisibleOnScreen)
        {
            _geometryService?.SetFloatingWindowVisibility(true, note.Id);
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
