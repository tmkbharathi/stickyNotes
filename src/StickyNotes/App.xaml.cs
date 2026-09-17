using Microsoft.UI.Xaml;
using StickyNotes.Helpers;
using StickyNotes.Services;

namespace StickyNotes;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private MainWindow? _mainWindow;

    public static IServiceProvider Services { get; } = ServiceConfiguration.ConfigureServices();
    public static SynchronizationContext? CurrentAppSynchronizationContext { get; private set; }

    public App()
    {
        this.RequestedTheme = ApplicationTheme.Dark;
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
            var cmdArgs = Environment.GetCommandLineArgs();
            bool isBackground = cmdArgs.Any(a =>
                a.Equals("--startup", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("/startup", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("-startup", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("/minimized", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("--background", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("/background", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("-background", StringComparison.OrdinalIgnoreCase));

            if (!isBackground)
            {
                var msg = NativeMethods.WM_SHOW_MAIN_WINDOW;
                NativeMethods.PostMessage(NativeMethods.HWND_BROADCAST, msg, IntPtr.Zero, IntPtr.Zero);
            }
            Environment.Exit(0);
            return;
        }

        CurrentAppSynchronizationContext = SynchronizationContext.Current;
        base.OnLaunched(args);
        _mainWindow = new MainWindow();
    }
}
