using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StickyNotes.Core.Services.Lifecycle;
using StickyNotes.Core.Services.Logging;
using StickyNotes.Core.Services.Persistence;
using StickyNotes.Core.Services.Update;
using StickyNotes.ViewModels;
using StickyNotes.Views;

namespace StickyNotes;

public sealed partial class MainWindow : Window
{
    private readonly ISettingsService _settingsService;
    private readonly IUpdateService _updateService;
    private readonly INotePersistenceService _notePersistence;
    private readonly IWindowStateManager _windowStateManager;
    private readonly IAppLifecycleManager _lifecycleManager;
    private readonly IUpdateLogger _logger;

    public MainHubViewModel HubViewModel { get; }
    public SettingsViewModel SettingsVm { get; }
    public UpdateViewModel UpdateVm { get; }

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
        var geometryService = new WindowGeometryService(storageDir, _logger);

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
        HubViewModel = new MainHubViewModel(_notePersistence, _updateService, _settingsService, geometryService);

        UpdateBannerHost.Content = new Views.Controls.UpdateBannerControl { ViewModel = UpdateVm };

        // Restore and track MainWindow geometry before display
        var savedMainGeo = geometryService.GetMainWindowGeometry();
        Helpers.WindowPlacementHelper.InitializeAndTrackWindow(
            this,
            savedMainGeo,
            (x, y, w, h, isMax) =>
            {
                geometryService.SaveMainWindowGeometry(x, y, w, h, isMax);
            },
            defaultWidth: 1040,
            defaultHeight: 700,
            minWidth: 600,
            minHeight: 400);

        this.Closed += async (s, e) =>
        {
            await geometryService.FlushAsync();
        };

        NavigateToNotes();
        _ = HubViewModel.InitializeAsync();
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
        if (HubViewModel.ActiveNote != null)
        {
            var noteWin = new NoteWindow(
                HubViewModel.ActiveNote,
                _notePersistence,
                HubViewModel.GeometryService,
                onNoteUpdated: async _ => await HubViewModel.SaveNoteAsync(HubViewModel.ActiveNote),
                onNewNoteRequested: async _ => await HubViewModel.CreateNewNoteAsync());
            noteWin.Activate();
        }
        else
        {
            _ = HubViewModel.CreateNewNoteAsync();
        }
    }
}
