using Windows.Graphics;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using StickyNotes.Core.Models;
using WinRT.Interop;

namespace StickyNotes.Helpers;

/// <summary>
/// Helper to restore and continuously track window geometry for WinUI 3 windows across restarts and multi-monitor setups.
/// </summary>
public static class WindowPlacementHelper
{
    public static AppWindow? InitializeAndTrackWindow(
        Window window,
        WindowGeometryModel savedGeometry,
        Action<int, int, int, int, bool> onGeometryChanged,
        int defaultWidth,
        int defaultHeight,
        int minWidth = 240,
        int minHeight = 200)
    {
        var hWnd = WindowNative.GetWindowHandle(window);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        if (appWindow == null) return null;

        var presenter = appWindow.Presenter as OverlappedPresenter;

        // Determine target placement before the window is shown
        int targetX, targetY, targetWidth, targetHeight;
        bool shouldMaximize = false;

        if (savedGeometry != null && savedGeometry.HasValue)
        {
            targetWidth = Math.Max(minWidth, savedGeometry.Width);
            targetHeight = Math.Max(minHeight, savedGeometry.Height);
            targetX = savedGeometry.X;
            targetY = savedGeometry.Y;
            shouldMaximize = savedGeometry.IsMaximized;

            // Validate against active monitor displays
            var testRect = new RectInt32(targetX, targetY, targetWidth, targetHeight);
            var displayArea = DisplayArea.GetFromRect(testRect, DisplayAreaFallback.Nearest) ?? DisplayArea.Primary;

            if (displayArea != null)
            {
                var workArea = displayArea.WorkArea;

                // Ensure window width/height doesn't exceed workArea
                targetWidth = Math.Min(targetWidth, workArea.Width);
                targetHeight = Math.Min(targetHeight, workArea.Height);

                // Ensure at least 50px of window is visible within work area
                if (targetX + targetWidth < workArea.X + 50)
                {
                    targetX = workArea.X;
                }
                else if (targetX > workArea.X + workArea.Width - 50)
                {
                    targetX = workArea.X + workArea.Width - targetWidth;
                }

                if (targetY < workArea.Y)
                {
                    targetY = workArea.Y;
                }
                else if (targetY > workArea.Y + workArea.Height - 50)
                {
                    targetY = workArea.Y + workArea.Height - targetHeight;
                }
            }
        }
        else
        {
            // First run / no saved geometry: center on primary display work area
            targetWidth = defaultWidth;
            targetHeight = defaultHeight;

            var primaryDisplay = DisplayArea.Primary;
            if (primaryDisplay != null)
            {
                var workArea = primaryDisplay.WorkArea;
                targetX = workArea.X + (workArea.Width - targetWidth) / 2;
                targetY = workArea.Y + (workArea.Height - targetHeight) / 2;
            }
            else
            {
                targetX = 100;
                targetY = 100;
            }
        }

        // Apply placement before show
        appWindow.MoveAndResize(new RectInt32(targetX, targetY, targetWidth, targetHeight));

        if (shouldMaximize && presenter != null)
        {
            presenter.Maximize();
        }

        // Track changes continuously
        int lastNormalX = targetX;
        int lastNormalY = targetY;
        int lastNormalWidth = targetWidth;
        int lastNormalHeight = targetHeight;

        appWindow.Changed += (s, e) =>
        {
            if (presenter != null && presenter.State == OverlappedPresenterState.Maximized)
            {
                onGeometryChanged(lastNormalX, lastNormalY, lastNormalWidth, lastNormalHeight, true);
            }
            else if (presenter != null && presenter.State == OverlappedPresenterState.Minimized)
            {
                // Don't overwrite normal dimensions when minimized
            }
            else
            {
                var pos = appWindow.Position;
                var size = appWindow.Size;

                if (size.Width >= minWidth && size.Height >= minHeight)
                {
                    lastNormalX = pos.X;
                    lastNormalY = pos.Y;
                    lastNormalWidth = size.Width;
                    lastNormalHeight = size.Height;

                    onGeometryChanged(pos.X, pos.Y, size.Width, size.Height, false);
                }
            }
        };

        // Hook closed event for immediate flush
        window.Closed += (s, e) =>
        {
            if (presenter != null && presenter.State == OverlappedPresenterState.Maximized)
            {
                onGeometryChanged(lastNormalX, lastNormalY, lastNormalWidth, lastNormalHeight, true);
            }
            else
            {
                var pos = appWindow.Position;
                var size = appWindow.Size;
                if (size.Width >= minWidth && size.Height >= minHeight)
                {
                    onGeometryChanged(pos.X, pos.Y, size.Width, size.Height, false);
                }
            }
        };

        return appWindow;
    }
}
