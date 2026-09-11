using Microsoft.UI.Xaml;

namespace StickyNotes;

public partial class App : Application
{
    private MainWindow? _mainWindow;

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);
        _mainWindow = new MainWindow();
        _mainWindow.Activate();
    }
}
