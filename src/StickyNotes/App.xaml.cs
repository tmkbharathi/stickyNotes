using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace StickyNotes;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private MainWindow? _mainWindow;
    public static SynchronizationContext? CurrentAppSynchronizationContext { get; private set; }

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public App()
    {
        this.InitializeComponent();

        this.UnhandledException += (s, e) =>
        {
            try
            {
                var storageDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StickyNotes");
                Directory.CreateDirectory(storageDir);
                File.AppendAllText(Path.Combine(storageDir, "crash.log"), $"{DateTime.UtcNow:O} [WinUI UnhandledException] {e.Message}\n{e.Exception}\n\n");
            }
            catch { }
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try
            {
                var storageDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StickyNotes");
                Directory.CreateDirectory(storageDir);
                File.AppendAllText(Path.Combine(storageDir, "crash.log"), $"{DateTime.UtcNow:O} [AppDomain UnhandledException] {e.ExceptionObject}\n\n");
            }
            catch { }
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            try
            {
                var storageDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StickyNotes");
                Directory.CreateDirectory(storageDir);
                File.AppendAllText(Path.Combine(storageDir, "crash.log"), $"{DateTime.UtcNow:O} [UnobservedTaskException] {e.Exception}\n\n");
            }
            catch { }
            e.SetObserved();
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        const string mutexName = "Global\\StickyNotes_SingleInstance_Mutex_bcfafafa";
        _singleInstanceMutex = new Mutex(true, mutexName, out bool isNewInstance);

        if (!isNewInstance)
        {
            var msg = Services.Tray.SystemTrayManager.WM_SHOW_MAIN_WINDOW;
            PostMessage((IntPtr)0xFFFF /* HWND_BROADCAST */, msg, IntPtr.Zero, IntPtr.Zero);
            Environment.Exit(0);
            return;
        }

        CurrentAppSynchronizationContext = SynchronizationContext.Current;
        base.OnLaunched(args);
        _mainWindow = new MainWindow();
    }
}
