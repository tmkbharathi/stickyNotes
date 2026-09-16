using System.Diagnostics;
using System.Runtime.InteropServices;
using StickyNotes.Helpers;

namespace StickyNotes.Services.Tray;

/// <summary>
/// Native Win32 System Tray Icon manager providing dynamic context menus, background lifecycle,
/// and single-instance activation.
/// </summary>
public sealed class SystemTrayManager : IDisposable
{
    private const int CMD_OPEN_MAIN = 1001;
    private const int CMD_SHOW_FLOATING = 1002;
    private const int CMD_HIDE_FLOATING = 1003;
    private const int CMD_EXIT = 1004;

    private readonly IntPtr _hostHwnd;
    private NativeMethods.NOTIFYICONDATA _nid;
    private bool _isAdded = false;
    private NativeMethods.SUBCLASSPROC? _subclassProc;

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
            if (uMsg == NativeMethods.WM_TRAYICON)
            {
                var eventMsg = (uint)(lParam.ToInt64() & 0xFFFF);
                if (eventMsg == NativeMethods.WM_LBUTTONUP || eventMsg == NativeMethods.WM_LBUTTONDBLCLK)
                {
                    OnOpenMainWindowRequested?.Invoke();
                    return IntPtr.Zero;
                }
                else if (eventMsg == NativeMethods.WM_RBUTTONUP)
                {
                    ShowContextMenu();
                    return IntPtr.Zero;
                }
            }
            else if (uMsg == NativeMethods.WM_SHOW_MAIN_WINDOW)
            {
                OnOpenMainWindowRequested?.Invoke();
                return IntPtr.Zero;
            }

            return NativeMethods.DefSubclassProc(hWnd, uMsg, wParam, lParam);
        };

        NativeMethods.SetWindowSubclass(_hostHwnd, _subclassProc, new UIntPtr(999), IntPtr.Zero);
    }

    private void InitializeTrayIcon()
    {
        IntPtr hIcon = IntPtr.Zero;
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath))
            {
                hIcon = NativeMethods.ExtractIconW(IntPtr.Zero, exePath, 0);
            }
        }
        catch
        {
            // fallback
        }

        if (hIcon == IntPtr.Zero)
        {
            hIcon = NativeMethods.LoadIconW(IntPtr.Zero, (IntPtr)NativeMethods.IDI_APPLICATION);
        }

        _nid = new NativeMethods.NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
            hWnd = _hostHwnd,
            uID = 1,
            uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
            uCallbackMessage = NativeMethods.WM_TRAYICON,
            hIcon = hIcon,
            szTip = "Sticky Notes"
        };

        _isAdded = NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref _nid);
    }

    private void ShowContextMenu()
    {
        var hMenu = NativeMethods.CreatePopupMenu();
        if (hMenu == IntPtr.Zero) return;

        try
        {
            bool isMainVis = IsMainWindowVisible?.Invoke() ?? true;
            bool isFloatVis = IsFloatingWindowVisible?.Invoke() ?? false;

            var mainText = isMainVis ? "Focus Sticky Notes" : "Open Sticky Notes";
            NativeMethods.AppendMenuW(hMenu, NativeMethods.MF_STRING | NativeMethods.MF_ENABLED, CMD_OPEN_MAIN, mainText);

            NativeMethods.AppendMenuW(hMenu, NativeMethods.MF_SEPARATOR, 0, string.Empty);

            var showFloatFlags = isFloatVis ? (NativeMethods.MF_STRING | NativeMethods.MF_GRAYED) : (NativeMethods.MF_STRING | NativeMethods.MF_ENABLED);
            NativeMethods.AppendMenuW(hMenu, showFloatFlags, CMD_SHOW_FLOATING, "Show Floating Window");

            var hideFloatFlags = isFloatVis ? (NativeMethods.MF_STRING | NativeMethods.MF_ENABLED) : (NativeMethods.MF_STRING | NativeMethods.MF_GRAYED);
            NativeMethods.AppendMenuW(hMenu, hideFloatFlags, CMD_HIDE_FLOATING, "Hide Floating Window");

            NativeMethods.AppendMenuW(hMenu, NativeMethods.MF_SEPARATOR, 0, string.Empty);

            NativeMethods.AppendMenuW(hMenu, NativeMethods.MF_STRING | NativeMethods.MF_ENABLED, CMD_EXIT, "Exit");

            NativeMethods.GetCursorPos(out var pt);
            NativeMethods.SetForegroundWindow(_hostHwnd);

            var cmd = NativeMethods.TrackPopupMenuEx(hMenu, NativeMethods.TPM_RIGHTBUTTON | NativeMethods.TPM_BOTTOMALIGN | NativeMethods.TPM_RETURNCMD, pt.x, pt.y, _hostHwnd, IntPtr.Zero);
            NativeMethods.PostMessage(_hostHwnd, 0, IntPtr.Zero, IntPtr.Zero);

            NativeMethods.DestroyMenu(hMenu);
            hMenu = IntPtr.Zero;

            switch (cmd)
            {
                case CMD_OPEN_MAIN:
                    App.CurrentAppSynchronizationContext?.Post(_ => OnOpenMainWindowRequested?.Invoke(), null);
                    break;
                case CMD_SHOW_FLOATING:
                    App.CurrentAppSynchronizationContext?.Post(_ => OnShowFloatingWindowRequested?.Invoke(), null);
                    break;
                case CMD_HIDE_FLOATING:
                    App.CurrentAppSynchronizationContext?.Post(_ => OnHideFloatingWindowRequested?.Invoke(), null);
                    break;
                case CMD_EXIT:
                    RemoveTrayIcon();
                    App.CurrentAppSynchronizationContext?.Post(_ => OnExitRequested?.Invoke(), null);
                    break;
            }
        }
        finally
        {
            if (hMenu != IntPtr.Zero)
            {
                NativeMethods.DestroyMenu(hMenu);
            }
        }
    }

    public void RemoveTrayIcon()
    {
        if (_isAdded)
        {
            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref _nid);
            _isAdded = false;
        }
    }

    public void Dispose()
    {
        RemoveTrayIcon();
    }
}
