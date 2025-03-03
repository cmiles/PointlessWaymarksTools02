using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using PointlessWaymarks.AvaloniaToolkit.StatusLayer;

namespace PointlessWaymarks.AvaloniaToolkit;

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

    public static async Task<bool> TextToClipboardIfPossible(string text, StatusLayerContext statusContext)
    {
        try
        {
            await UiThreadSwitcher.ResumeForegroundAsync();

            var clipboard = Get();
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(text);
                statusContext.ToastSuccess("Text copied to clipboard");
                return true;
            }
            else
            {
                statusContext.ToastError("Clipboard not available...");
                return false;
            }
        }
        catch (Exception e)
        {
            statusContext.ToastError($"Error setting clipboard text {e.Message}");
            return false;
        }
    }
}