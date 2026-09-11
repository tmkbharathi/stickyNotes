using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StickyNotes.Core.Models.Note;
using StickyNotes.Core.Services.Lifecycle;
using StickyNotes.Core.Services.Logging;
using StickyNotes.Core.Services.Persistence;
using StickyNotes.Core.Services.Update;
using StickyNotes.Services.Tray;
using StickyNotes.ViewModels;
using StickyNotes.Views;
using WinRT.Interop;

namespace StickyNotes;

public sealed partial class MainWindow : Window
{
    private readonly ISettingsService _settingsService;
    private readonly IUpdateService _updateService;
    private readonly INotePersistenceService _notePersistence;
    private readonly IWindowStateManager _windowStateManager;
    private readonly IAppLifecycleManager _lifecycleManager;
    private readonly IWindowGeometryService _geometryService;
    private readonly IUpdateLogger _logger;

    private AppWindow? _appWindow;
    private SystemTrayManager? _trayManager;
    private NoteWindow? _floatingWindow;
    private bool _isExplicitExit = false;

    public MainHubViewModel HubViewModel { get; }
    public SettingsViewModel SettingsVm { get; }
    public UpdateViewModel UpdateVm { get; }

    public bool IsMainWindowOpen => _appWindow?.IsVisible ?? false;
    public bool IsFloatingWindowActive => _floatingWindow != null && _floatingWindow.IsVisibleOnScreen;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    public MainWindow()
    {
        this.InitializeComponent();

        this.ExtendsContentIntoTitleBar = true;
        this.SetTitleBar(AppTitleBar);

        var storageDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StickyNotes");

        _logger = new UpdateLogger(storageDir);
        _settingsService = new SettingsService(storageDir, _logger);
        _notePersistence = new JsonNotePersistenceService(storageDir, _logger);
        _windowStateManager = new WindowStateManager(storageDir, _logger);
        _lifecycleManager = new AppLifecycleManager(_logger);
        _geometryService = new WindowGeometryService(storageDir, _logger);

        var httpClient = new HttpClient();
        var deploymentProvider = new MsixPackageManagerDeploymentProvider(httpClient, _logger);

        _updateService = new UpdateService(
            deploymentProvider,
            _notePersistence,
            _settingsService,
            _windowStateManager,
            _lifecycleManager,
            _logger);

        UpdateVm = new UpdateViewModel(_updateService);
        SettingsVm = new SettingsViewModel(_settingsService, _updateService);
        HubViewModel = new MainHubViewModel(_notePersistence, _updateService, _settingsService, _geometryService);

        UpdateBannerHost.Content = new Views.Controls.UpdateBannerControl { ViewModel = UpdateVm };

        // Restore and track MainWindow geometry before display
        var savedMainGeo = _geometryService.GetMainWindowGeometry();
        _appWindow = Helpers.WindowPlacementHelper.InitializeAndTrackWindow(
            this,
            savedMainGeo,
            (x, y, w, h, isMax) =>
            {
                _geometryService.SaveMainWindowGeometry(x, y, w, h, isMax, isVisible: IsMainWindowOpen);
            },
            defaultWidth: 1000,
            defaultHeight: 640,
            minWidth: 880,
            minHeight: 560);

        Helpers.WindowMinSizeHelper.SetMinSize(this, minWidthDip: 880, minHeightDip: 560);

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "appIcon.ico");
        if (File.Exists(iconPath) && _appWindow != null)
        {
            _appWindow.SetIcon(iconPath);
        }

        // Intercept close button to hide to tray
        if (_appWindow != null)
        {
            _appWindow.Closing += (s, e) =>
            {
                if (!_isExplicitExit)
                {
                    e.Cancel = true;
                    HideToTray();
                }
            };
        }

        // Initialize System Tray
        var hWnd = WindowNative.GetWindowHandle(this);
        _trayManager = new SystemTrayManager(hWnd)
        {
            IsMainWindowVisible = () => IsMainWindowOpen,
            IsFloatingWindowVisible = () => IsFloatingWindowActive,
            OnOpenMainWindowRequested = () => ShowAndFocus(),
            OnShowFloatingWindowRequested = () => ShowFloatingWindow(),
            OnHideFloatingWindowRequested = () => HideFloatingWindow(),
            OnExitRequested = () => ExitApplication()
        };

        NavigateToNotes();

        // Initialize and determine initial window visibility
        _ = InitializeStartupVisibilityAsync();
    }

    private async Task InitializeStartupVisibilityAsync()
    {
        await HubViewModel.InitializeAsync();
        var savedFloatGeo = _geometryService.GetFloatingWindowGeometry();
        var cmdArgs = Environment.GetCommandLineArgs();
        bool isStartupLaunch = cmdArgs.Any(a =>
            a.Equals("--startup", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/startup", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("-startup", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/minimized", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("--background", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/background", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("-background", StringComparison.OrdinalIgnoreCase));

        // When opened normally via shortcut/icon, always show Main Window. Only minimize to tray on startup/background flag.
        bool shouldShowMain = !isStartupLaunch;
        bool shouldShowFloat = savedFloatGeo.IsVisible;

        // Auto-show Floating Window if was previously visible
        if (shouldShowFloat)
        {
            NoteModel? targetNote = null;
            if (!string.IsNullOrEmpty(savedFloatGeo.ActiveNoteId))
            {
                targetNote = HubViewModel.AllNotes.FirstOrDefault(n => n.Id == savedFloatGeo.ActiveNoteId);
            }
            targetNote ??= HubViewModel.ActiveNote ?? HubViewModel.AllNotes.FirstOrDefault();

            if (targetNote != null)
            {
                ShowFloatingWindow(targetNote);
            }
        }

        // Show or keep Main Window hidden in tray
        if (shouldShowMain)
        {
            ShowAndFocus();
        }
        else
        {
            HideToTray();
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

        _geometryService.SetMainWindowVisibility(true);
    }

    public void HideToTray()
    {
        _appWindow?.Hide();
        _geometryService.SetMainWindowVisibility(false);
    }

    public void ShowFloatingWindow(NoteModel? note = null)
    {
        note ??= HubViewModel.ActiveNote ?? HubViewModel.AllNotes.FirstOrDefault();
        if (note == null) return;

        if (_floatingWindow == null)
        {
            _floatingWindow = new NoteWindow(
                note,
                _notePersistence,
                _geometryService,
                onNoteUpdated: async n => await HubViewModel.SaveNoteAsync(n),
                onNewNoteRequested: async _ => await HubViewModel.CreateNewNoteAsync());
        }
        else
        {
            _floatingWindow.UpdateNote(note);
        }

        _floatingWindow.ShowAndFocus();
        _geometryService.SetFloatingWindowVisibility(true, note.Id);
    }

    public void HideFloatingWindow()
    {
        _floatingWindow?.HideToTray();
        _geometryService.SetFloatingWindowVisibility(false);
    }

    public async void ExitApplication()
    {
        _isExplicitExit = true;

        var mainPos = _appWindow?.Position;
        var mainSize = _appWindow?.Size;
        if (mainPos.HasValue && mainSize.HasValue)
        {
            _geometryService.SaveMainWindowGeometry(mainPos.Value.X, mainPos.Value.Y, mainSize.Value.Width, mainSize.Value.Height, isMaximized: false, isVisible: IsMainWindowOpen);
        }

        if (_floatingWindow != null)
        {
            _geometryService.SetFloatingWindowVisibility(_floatingWindow.IsVisibleOnScreen, _floatingWindow.Note?.Id);
        }

        _trayManager?.Dispose();
        await _geometryService.FlushAsync();
        await _notePersistence.FlushAllPendingAsync();

        _floatingWindow?.Close();
        this.Close();

        Application.Current.Exit();
        Environment.Exit(0);
    }

    private void NavigateToNotes()
    {
        ContentFrame.Navigate(typeof(NotesHubPage), HubViewModel);
    }

    private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(SettingsPage), SettingsVm);
    }

    private void OnOpenPopoutWindowClick(object sender, RoutedEventArgs e)
    {
        ShowFloatingWindow(HubViewModel.ActiveNote);
    }
}
