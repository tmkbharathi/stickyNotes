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

        var storageDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StickyNotes");

        _logger = new UpdateLogger(storageDir);
        _settingsService = new SettingsService(storageDir, _logger);
        _notePersistence = new JsonNotePersistenceService(storageDir, _logger);
        _windowStateManager = new WindowStateManager(storageDir, _logger);
        _lifecycleManager = new AppLifecycleManager(_logger);

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
        HubViewModel = new MainHubViewModel(_notePersistence, _updateService, _settingsService);

        UpdateBannerHost.Content = new Views.Controls.UpdateBannerControl { ViewModel = UpdateVm };

        NavigateToNotes();
        _ = HubViewModel.InitializeAsync();
    }

    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            if (ContentFrame.Content is SettingsPage settingsPage)
            {
                settingsPage.ViewModel = SettingsVm;
            }
        }
        else
        {
            NavigateToNotes();
        }
    }

    private void NavigateToNotes()
    {
        // Default notes hub placeholder or content view
        var notesHub = new StackPanel { Spacing = 12 };
        notesHub.Children.Add(new TextBlock
        {
            Text = "Sticky Notes Hub",
            Style = (Style)Application.Current.Resources["TitleTextBlockStyle"]
        });
        notesHub.Children.Add(new TextBlock
        {
            Text = "Create, search, and manage your sticky notes with multi-snippet copy engine.",
            Style = (Style)Application.Current.Resources["BodyTextBlockStyle"]
        });

        ContentFrame.Content = notesHub;
    }
}
