using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace StickyNotes.Helpers;

/// <summary>
/// Subclasses a WinUI 3 Window to enforce native Win32 minimum window tracking size (WM_GETMINMAXINFO).
/// </summary>
public static class WindowMinSizeHelper
{
    private const int WM_GETMINMAXINFO = 0x0024;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, UIntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    // Keep reference to prevent garbage collection of delegate
    private static readonly List<SUBCLASSPROC> _activeProcs = new();

    public static void SetMinSize(Window window, int minWidthDip, int minHeightDip)
    {
        var hWnd = WindowNative.GetWindowHandle(window);
        if (hWnd == IntPtr.Zero) return;

        SUBCLASSPROC proc = (h, msg, wp, lp, id, data) =>
        {
            if (msg == WM_GETMINMAXINFO)
            {
                var dpi = GetDpiForWindow(h);
                float scaling = dpi > 0 ? (dpi / 96.0f) : 1.0f;

                var mmi = Marshal.PtrToStructure<MINMAXINFO>(lp);
                mmi.ptMinTrackSize.x = (int)(minWidthDip * scaling);
                mmi.ptMinTrackSize.y = (int)(minHeightDip * scaling);
                Marshal.StructureToPtr(mmi, lp, true);
                return IntPtr.Zero;
            }
            return DefSubclassProc(h, msg, wp, lp);
        };

        _activeProcs.Add(proc);
        SetWindowSubclass(hWnd, proc, new UIntPtr((uint)_activeProcs.Count), IntPtr.Zero);
    }
}
