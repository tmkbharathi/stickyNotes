using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace StickyNotes.Helpers;

/// <summary>
/// Subclasses a WinUI 3 Window to enforce native Win32 minimum window tracking size (WM_GETMINMAXINFO).
/// </summary>
public static class WindowMinSizeHelper
{
    // Keep reference to prevent garbage collection of delegate
    private static readonly List<NativeMethods.SUBCLASSPROC> _activeProcs = new();

    public static void SetMinSize(Window window, int minWidthDip, int minHeightDip)
    {
        var hWnd = WindowNative.GetWindowHandle(window);
        if (hWnd == IntPtr.Zero) return;

        NativeMethods.SUBCLASSPROC proc = (h, msg, wp, lp, id, data) =>
        {
            if (msg == NativeMethods.WM_GETMINMAXINFO)
            {
                var dpi = NativeMethods.GetDpiForWindow(h);
                float scaling = dpi > 0 ? (dpi / 96.0f) : 1.0f;

                var mmi = Marshal.PtrToStructure<NativeMethods.MINMAXINFO>(lp);
                mmi.ptMinTrackSize.x = (int)(minWidthDip * scaling);
                mmi.ptMinTrackSize.y = (int)(minHeightDip * scaling);
                Marshal.StructureToPtr(mmi, lp, true);
                return IntPtr.Zero;
            }
            return NativeMethods.DefSubclassProc(h, msg, wp, lp);
        };

        _activeProcs.Add(proc);
        NativeMethods.SetWindowSubclass(hWnd, proc, new UIntPtr((uint)_activeProcs.Count), IntPtr.Zero);
    }
}
