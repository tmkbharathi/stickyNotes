using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StickyNotes.Core.Models.Note;
using StickyNotes.Core.Models.Update;
using StickyNotes.Core.Services.Lifecycle;
using StickyNotes.Core.Services.Persistence;
using StickyNotes.Core.Services.Update;
using StickyNotes.Helpers;
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
    private readonly IWindowGeometryService _geometryService;
    private readonly IStartupService _startupService;

    private AppWindow? _appWindow;
    private SystemTrayManager? _trayManager;
    private NoteWindow? _floatingWindow;
    private bool _isExplicitExit = false;

    public MainHubViewModel HubViewModel { get; }
    public SettingsViewModel SettingsVm { get; }
    public UpdateViewModel UpdateVm { get; }

    public bool IsMainWindowOpen => _appWindow?.IsVisible ?? false;
    public bool IsFloatingWindowActive => _floatingWindow != null && _floatingWindow.IsVisibleOnScreen;

    public MainWindow()
    {
        this.InitializeComponent();

        this.ExtendsContentIntoTitleBar = true;
        this.SetTitleBar(AppTitleBar);

        // Resolve dependencies via Composition Root (App.Services)
        var sp = App.Services;
        _settingsService = sp.GetRequiredService<ISettingsService>();
        _notePersistence = sp.GetRequiredService<INotePersistenceService>();
        _geometryService = sp.GetRequiredService<IWindowGeometryService>();
        _startupService = sp.GetRequiredService<IStartupService>();
        _updateService = sp.GetRequiredService<IUpdateService>();

        HubViewModel = sp.GetRequiredService<MainHubViewModel>();
        SettingsVm = sp.GetRequiredService<SettingsViewModel>();
        UpdateVm = sp.GetRequiredService<UpdateViewModel>();

        // Default auto startup with Windows to ON on initial run
        var currentUpdateSettings = _settingsService.GetUpdateSettings();
        if (!currentUpdateSettings.HasInitializedStartup)
        {
            _startupService.SetStartupEnabled(true);
            currentUpdateSettings.HasInitializedStartup = true;
            _ = _settingsService.SaveUpdateSettingsAsync(currentUpdateSettings);
        }

        _updateService.StateChanged += (s, e) =>
        {
            if (e.NewState == UpdateState.UpdateAvailable)
            {
                if (App.CurrentAppSynchronizationContext != null)
                {
                    App.CurrentAppSynchronizationContext.Post(_ => _ = ShowUpdateDialogAsync(), null);
                }
                else
                {
                    _ = ShowUpdateDialogAsync();
                }
            }
        };

        // Restore and track MainWindow geometry before display
        var savedMainGeo = _geometryService.GetMainWindowGeometry();
        _appWindow = WindowPlacementHelper.InitializeAndTrackWindow(
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

        WindowMinSizeHelper.SetMinSize(this, minWidthDip: 880, minHeightDip: 560);

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

        ContentFrame.Navigated += (s, e) =>
        {
            TopSettingsButton.Visibility = (e.SourcePageType == typeof(SettingsPage))
                ? Visibility.Collapsed
                : Visibility.Visible;
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
        this.Activate();
        if (_appWindow != null)
        {
            _appWindow.Show();
            _appWindow.MoveInZOrderAtTop();
        }

        var hWnd = WindowNative.GetWindowHandle(this);
        NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
        NativeMethods.SetForegroundWindow(hWnd);

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
        _trayManager?.RemoveTrayIcon();
        _trayManager?.Dispose();
        _trayManager = null;

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

        try
        {
            await _geometryService.FlushAsync();
            await _notePersistence.FlushAllPendingAsync();
        }
        catch
        {
            // Ignore error during termination
        }

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

    private Views.Dialogs.UpdateAvailableDialog? _activeUpdateDialog;

    private async Task ShowUpdateDialogAsync()
    {
        if (_activeUpdateDialog != null || this.Content?.XamlRoot == null) return;

        try
        {
            _activeUpdateDialog = new Views.Dialogs.UpdateAvailableDialog(UpdateVm)
            {
                XamlRoot = this.Content.XamlRoot
            };
            await _activeUpdateDialog.ShowAsync();
        }
        catch
        {
            // Avoid collision if already open
        }
        finally
        {
            _activeUpdateDialog = null;
        }
    }
}
