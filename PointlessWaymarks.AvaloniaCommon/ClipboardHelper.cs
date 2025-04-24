using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using PointlessWaymarks.AvaloniaCommon.Status;

namespace PointlessWaymarks.AvaloniaCommon;

public static class ClipboardHelper
{
    public static IClipboard? Get()
    {
        //Desktop
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
            {
                MainWindow: { } window
            })
        {
            return window.Clipboard!;
        }
        //Android (and iOS?)
        else if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime { MainView: { } mainView })
        {
            var visualRoot = mainView.GetVisualRoot();
            if (visualRoot is TopLevel topLevel) return topLevel.Clipboard!;
        }

        return null!;
    }

    public static async Task<bool> TextToClipboardIfPossible(string text, StatusControlContext statusContext)
    {
        try
        {
            await ThreadSwitcher.ResumeForegroundAsync();

            var clipboard = Get();
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(text);
                await statusContext.ToastSuccess("Text copied to clipboard");
                return true;
            }
            else
            {
                await statusContext.ToastError("Clipboard not available...");
                return false;
            }
        }
        catch (Exception e)
        {
            await statusContext.ToastError($"Error setting clipboard text {e.Message}");
            return false;
        }
    }
}