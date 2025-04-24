using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Clowd.Clipboard;

namespace PointlessWaymarks.AvaloniaCommon.WindowScreenShot;

public static class AvaloniaCapture
{
    private static async Task<RenderTargetBitmap?> CaptureWindow(Window window)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        try
        {
            var pixelSize = new PixelSize((int)window.Width, (int)window.Height);
            var size = new Size(window.Width, window.Height);
            var dpiVector = new Vector(96, 96);

            var bitmap = new RenderTargetBitmap(pixelSize, dpiVector);
            window.Measure(size);
            window.Arrange(new Rect(size));
            bitmap.Render(window);

            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task<bool> TryCopyBitmapToClipboard(RenderTargetBitmap bitmap)
    {
        var tries = 3;
        while (tries-- > 0)
            try
            {
                await ThreadSwitcher.ResumeForegroundAsync();
                await ClipboardAvalonia.SetImageAsync(bitmap);
                return true;
            }
            catch
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }

        return false;
    }

    public static async Task<bool> TryWindowScreenShotToClipboardAsync(Window window)
    {
        try
        {
            var bitmap = await CaptureWindow(window);
            if (bitmap == null) return false;

            return await TryCopyBitmapToClipboard(bitmap);
        }
        catch (Exception)
        {
            return false;
        }
    }
}