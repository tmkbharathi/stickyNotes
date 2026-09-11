using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StickyNotes.Services.Tray;

/// <summary>
/// Native Win32 System Tray Icon manager providing dynamic context menus, background lifecycle,
/// and single-instance activation.
/// </summary>
public sealed class SystemTrayManager : IDisposable
{
    private const uint WM_USER = 0x0400;
    public const uint WM_TRAYICON = WM_USER + 200;
    public const uint WM_SHOW_MAIN_WINDOW = WM_USER + 201;

    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;
    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;

    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_LBUTTONDBLCLK = 0x0203;
    private const uint WM_RBUTTONUP = 0x0205;

    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_RETURNCMD = 0x0100;

    private const uint MF_STRING = 0x00000000;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint MF_ENABLED = 0x00000000;
    private const uint MF_GRAYED = 0x00000001;

    private const int CMD_OPEN_MAIN = 1001;
    private const int CMD_SHOW_FLOATING = 1002;
    private const int CMD_HIDE_FLOATING = 1003;
    private const int CMD_EXIT = 1004;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractIconW(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIconW(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(IntPtr hMenu, uint fuFlags, int x, int y, IntPtr hWnd, IntPtr lptpm);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, UIntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    private readonly IntPtr _hostHwnd;
    private NOTIFYICONDATA _nid;
    private bool _isAdded = false;
    private SUBCLASSPROC? _subclassProc;

    public Func<bool>? IsMainWindowVisible { get; set; }
    public Func<bool>? IsFloatingWindowVisible { get; set; }
    public Action? OnOpenMainWindowRequested { get; set; }
    public Action? OnShowFloatingWindowRequested { get; set; }
    public Action? OnHideFloatingWindowRequested { get; set; }
    public Action? OnExitRequested { get; set; }

    public SystemTrayManager(IntPtr hostHwnd)
    {
        _hostHwnd = hostHwnd;
        HookWindowMessages();
        InitializeTrayIcon();
    }

    private void HookWindowMessages()
    {
        if (_hostHwnd == IntPtr.Zero) return;

        _subclassProc = (hWnd, uMsg, wParam, lParam, uIdSubclass, dwRefData) =>
        {
            if (uMsg == WM_TRAYICON)
            {
                var eventMsg = (uint)(lParam.ToInt64() & 0xFFFF);
                if (eventMsg == WM_LBUTTONUP || eventMsg == WM_LBUTTONDBLCLK)
                {
                    OnOpenMainWindowRequested?.Invoke();
                    return IntPtr.Zero;
                }
                else if (eventMsg == WM_RBUTTONUP)
                {
                    ShowContextMenu();
                    return IntPtr.Zero;
                }
            }
            else if (uMsg == WM_SHOW_MAIN_WINDOW)
            {
                OnOpenMainWindowRequested?.Invoke();
                return IntPtr.Zero;
            }

            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        };

        SetWindowSubclass(_hostHwnd, _subclassProc, new UIntPtr(999), IntPtr.Zero);
    }

    private void InitializeTrayIcon()
    {
        IntPtr hIcon = IntPtr.Zero;
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath))
            {
                hIcon = ExtractIconW(IntPtr.Zero, exePath, 0);
            }
        }
        catch
        {
            // fallback
        }

        if (hIcon == IntPtr.Zero)
        {
            hIcon = LoadIconW(IntPtr.Zero, (IntPtr)32512); // IDI_APPLICATION
        }

        _nid = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hostHwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = hIcon,
            szTip = "Sticky Notes"
        };

        _isAdded = Shell_NotifyIcon(NIM_ADD, ref _nid);
    }

    private void ShowContextMenu()
    {
        var hMenu = CreatePopupMenu();
        if (hMenu == IntPtr.Zero) return;

        try
        {
            bool isMainVis = IsMainWindowVisible?.Invoke() ?? true;
            bool isFloatVis = IsFloatingWindowVisible?.Invoke() ?? false;

            var mainText = isMainVis ? "Focus Sticky Notes" : "Open Sticky Notes";
            AppendMenuW(hMenu, MF_STRING | MF_ENABLED, CMD_OPEN_MAIN, mainText);

            AppendMenuW(hMenu, MF_SEPARATOR, 0, string.Empty);

            var showFloatFlags = isFloatVis ? (MF_STRING | MF_GRAYED) : (MF_STRING | MF_ENABLED);
            AppendMenuW(hMenu, showFloatFlags, CMD_SHOW_FLOATING, "Show Floating Window");

            var hideFloatFlags = isFloatVis ? (MF_STRING | MF_ENABLED) : (MF_STRING | MF_GRAYED);
            AppendMenuW(hMenu, hideFloatFlags, CMD_HIDE_FLOATING, "Hide Floating Window");

            AppendMenuW(hMenu, MF_SEPARATOR, 0, string.Empty);

            AppendMenuW(hMenu, MF_STRING | MF_ENABLED, CMD_EXIT, "Exit");

            GetCursorPos(out var pt);
            SetForegroundWindow(_hostHwnd);

            var cmd = TrackPopupMenuEx(hMenu, TPM_RIGHTBUTTON | TPM_RETURNCMD, pt.x, pt.y, _hostHwnd, IntPtr.Zero);
            PostMessage(_hostHwnd, 0, IntPtr.Zero, IntPtr.Zero);

            switch (cmd)
            {
                case (int)CMD_OPEN_MAIN:
                    OnOpenMainWindowRequested?.Invoke();
                    break;
                case (int)CMD_SHOW_FLOATING:
                    OnShowFloatingWindowRequested?.Invoke();
                    break;
                case (int)CMD_HIDE_FLOATING:
                    OnHideFloatingWindowRequested?.Invoke();
                    break;
                case (int)CMD_EXIT:
                    OnExitRequested?.Invoke();
                    break;
            }
        }
        finally
        {
            DestroyMenu(hMenu);
        }
    }

    public void RemoveTrayIcon()
    {
        if (_isAdded)
        {
            Shell_NotifyIcon(NIM_DELETE, ref _nid);
            _isAdded = false;
        }
    }

    public void Dispose()
    {
        RemoveTrayIcon();
    }
}
