using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using WpfScreenHelper;
using Point = System.Drawing.Point;

namespace PointlessWaymarks.WpfCommon.Utility;

/// <summary>
///     Methods for 'safely' positioning a window to avoid pitfalls like the window being
///     off screen. Heavily based on reading code from https://github.com/RickStrahl/MarkdownMonster/,
///     https://github.com/anakic/Jot, https://github.com/microsoft/WPF-Samples/tree/master/Windows/SaveWindowState
///     and https://github.com/micdenny/WpfScreenHelper
/// </summary>
public static class WindowInitialPositionHelpers
{
    /// <summary>
    ///     !! Only call this on the UI Thread !! Ensures that a window is visible on a screen (ie the window is not trapped
    ///     off screen).
    ///     In general for anything other than the initial main window try to use PositionWindowAndShowOnUiThread
    ///     as it will both ensure calls are on the correct thread and will by default
    ///     position windows relative to the first active window in the application.
    /// </summary>
    /// <param name="window"></param>
    /// <param name="parentWindow"></param>
    public static void EnsureWindowIsVisible(Window window, Window? parentWindow = null)
    {
        //This borrows heavily from https://github.com/RickStrahl/MarkdownMonster/

        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;

        Rect screenBounds = Rect.Empty;

        if (parentWindow == null && Application.Current != null && Application.Current.Dispatcher.CheckAccess())
        {
            try
            {
                parentWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive) ??
                               Application.Current.Windows.OfType<Window>().FirstOrDefault();
            }
            catch
            {
                // Ignore collection access errors across threads
            }
        }

        if (parentWindow != null && parentWindow.Dispatcher.CheckAccess())
        {
            try
            {
                var parentHandle = new WindowInteropHelper(parentWindow).Handle;
                if (parentHandle != IntPtr.Zero)
                {
                    screenBounds = Screen.FromHandle(parentHandle)?.WpfWorkingArea ?? Rect.Empty;
                }
            }
            catch
            {
                // Ignore
            }
        }

        if (screenBounds.IsEmpty && window.Dispatcher.CheckAccess())
        {
            try
            {
                var windowHandle = new WindowInteropHelper(window).Handle;
                if (windowHandle != IntPtr.Zero)
                {
                    screenBounds = Screen.FromHandle(windowHandle)?.WpfWorkingArea ?? Rect.Empty;
                }
            }
            catch
            {
                // Ignore
            }
        }

        if (screenBounds.IsEmpty)
        {
            try
            {
                var point = new System.Windows.Point(
                    double.IsNaN(window.Left) ? 0 : window.Left,
                    double.IsNaN(window.Top) ? 0 : window.Top);
                screenBounds = Screen.FromPoint(point)?.WpfWorkingArea ?? Rect.Empty;
            }
            catch
            {
                // Ignore
            }
        }

        if (screenBounds.IsEmpty)
        {
            screenBounds = Screen.PrimaryScreen?.WpfWorkingArea ?? SystemParameters.WorkArea;
        }

        if (double.IsNaN(window.Width) || window.Width <= 0) window.Width = 600;
        if (double.IsNaN(window.Height) || window.Height <= 0) window.Height = 400;
        if (double.IsNaN(window.Left)) window.Left = screenBounds.Left + 10;
        if (double.IsNaN(window.Top)) window.Top = screenBounds.Top + 24;

        if (window.Left + window.Width > screenBounds.Right) window.Left = screenBounds.Right - window.Width;

        if (window.Left < screenBounds.Left)
        {
            window.Left = 10 + screenBounds.Left;
            if (window.Width + 10 > screenBounds.Width)
                window.Width = screenBounds.Width - 20;
        }

        if (window.Top + window.Height > screenBounds.Bottom) window.Top = screenBounds.Bottom - window.Height;

        if (window.Top < screenBounds.Top)
        {
            window.Top = 10 + screenBounds.Top;
            if (window.Height + 10 > screenBounds.Height)
                window.Height = screenBounds.Height - 20;
        }
    }

    private static uint GetDpi(IntPtr hwnd, DpiType dpiType)
    {
        var screen = Screen.FromHandle(hwnd);
        var screenPoint = new Point((int)screen.Bounds.Left, (int)screen.Bounds.Top);
        var monitor = MonitorFromPoint(screenPoint, 2 /*MONITOR_DEFAULTTONEAREST*/);

        uint dpiX = 96;

        try
        {
            GetDpiForMonitor(monitor, dpiType, out dpiX, out _);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }

        return dpiX;
    }

    //https://msdn.microsoft.com/en-us/library/windows/desktop/dn280510(v=vs.85).aspx
    [DllImport("Shcore.dll")]
    private static extern IntPtr GetDpiForMonitor([In] IntPtr hmonitor, [In] DpiType dpiType, [Out] out uint dpiX,
        [Out] out uint dpiY);

    //https://msdn.microsoft.com/en-us/library/windows/desktop/dd145062(v=vs.85).aspx
    [DllImport("User32.dll")]
    private static extern IntPtr MonitorFromPoint([In] Point pt, [In] uint dwFlags);


    /// <summary>
    ///     !! Only call this on the UI Thread !! Positions a window attempting to avoid common pitfalls like being offscreen
    ///     - this avoids the need to Dispatch this to or
    ///     switch to the UI thread before interacting with the window.
    /// </summary>
    /// <param name="toPosition">If null the position is based on the first active window in the Application</param>
    /// <returns></returns>
    public static void PositionWindowAndShow(this Window toPosition)
    {
        var positionReference = Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive) ??
                                Application.Current.Windows.OfType<Window>().FirstOrDefault();

        if (positionReference != null)
        {
            toPosition.Left = positionReference.Left + 10;
            toPosition.Top = positionReference.Top + 24;
        }

        EnsureWindowIsVisible(toPosition, positionReference);

        toPosition.Show();
    }

    public static bool? PositionWindowAndShowDialog(this Window toPosition)
    {
        var positionReference = Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive) ??
                                Application.Current.Windows.OfType<Window>().FirstOrDefault();

        if (positionReference != null)
            toPosition.Owner = positionReference;
        else
            EnsureWindowIsVisible(toPosition, positionReference);

        return toPosition.ShowDialog();
    }

    public static async Task<bool?> PositionWindowAndShowDialogOnUiThread(this Window toPosition)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        return toPosition.PositionWindowAndShowDialog();
    }

    /// <summary>
    ///     Positions a window on the UI Thread attempting to avoid common pitfalls like being offscreen
    ///     - this avoids the need to Dispatch this to or
    ///     switch to the UI thread before interacting with the window.
    /// </summary>
    /// <param name="toPosition">If null the position is based on the first active window in the Application</param>
    /// <returns></returns>
    public static async Task PositionWindowAndShowOnUiThread(this Window toPosition)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        toPosition.PositionWindowAndShow();
    }

    private enum DpiType
    {
        Effective = 0,
        Angular = 1,
        Raw = 2
    }
}