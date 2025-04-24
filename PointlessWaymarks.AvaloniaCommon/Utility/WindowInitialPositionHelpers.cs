using Avalonia.Controls;

namespace PointlessWaymarks.AvaloniaCommon.Utility
{
    public static class WindowInitialPositionHelpers
    {
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

            toPosition.Show();
        }

    }
}
