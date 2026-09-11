using Microsoft.UI.Xaml;

namespace StickyNotes;

public partial class App : Application

{
    private MainWindow? _mainWindow;
    public static SynchronizationContext? CurrentAppSynchronizationContext { get; private set; }

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        CurrentAppSynchronizationContext = SynchronizationContext.Current;
        base.OnLaunched(args);
        _mainWindow = new MainWindow();
        _mainWindow.Activate();
    }
}

