using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using OneOf;
using OneOf.Types;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.WpfCommon.WpfHtml;

/// <summary>
///     Interaction logic for InteractiveWebViewJpegImageWindow.xaml
/// </summary>
[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class InteractiveWebViewJpegImageWindow
{
    public InteractiveWebViewJpegImageWindow(StatusControlContext statusContext, string? initialUrl,
        Func<string>? saveAsFilename)
    {
        InitializeComponent();

        StatusContext = statusContext;
        UserUrl = initialUrl;
        SaveAsFilename = saveAsFilename;
        BuildCommands();

        DataContext = this;
    }

    public bool CloseOnSave { get; set; }

    public Func<Task<OneOf<Success<byte[]>, Error<string>>>>? JpgScreenshotFunction { get; set; }
    public Func<string>? SaveAsFilename { get; set; }
    public StatusControlContext StatusContext { get; set; }
    public string? UserUrl { get; set; }

    public bool ManualCaptureActive { get; set; }
    public int ManualCapturedCount { get; set; }
    public List<byte[]> ManualCapturedFrames { get; set; } = [];

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!ManualCaptureActive) return;

        if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (UrlTextBox.IsFocused) return;

            e.Handled = true;
            StatusContext.RunBlockingTask(CaptureManualFrame);
        }
    }

    private void WebContentWebView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!ManualCaptureActive) return;

        if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            StatusContext.RunBlockingTask(CaptureManualFrame);
        }
    }

    [BlockingCommand]
    public async Task StartManualCapture()
    {
        ManualCapturedFrames.Clear();
        ManualCapturedCount = 0;
        ManualCaptureActive = true;
        await StatusContext.ToastSuccess(
            "Manual Screen Capture started. Scroll down and press Ctrl+Space (or click Capture) to capture frames.");
    }

    [BlockingCommand]
    public async Task CaptureManualFrame()
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        if (WebContentWebView.CoreWebView2 == null)
        {
            await StatusContext.ToastError("WebView2 is not ready yet.");
            return;
        }

        try
        {
            using var stream = new MemoryStream();
            await WebContentWebView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
            var imageBytes = stream.ToArray();

            if (imageBytes.Length == 0)
            {
                await StatusContext.ToastError("Failed to capture screen image.");
                return;
            }

            ManualCapturedFrames.Add(imageBytes);
            ManualCapturedCount = ManualCapturedFrames.Count;
            await StatusContext.ToastSuccess($"Captured frame {ManualCapturedCount}");
        }
        catch (Exception ex)
        {
            await StatusContext.ToastError($"Error capturing frame: {ex.Message}");
        }
    }

    [BlockingCommand]
    public async Task FinishManualCapture()
    {
        if (ManualCapturedFrames.Count == 0)
        {
            await StatusContext.ToastError("No images have been captured yet.");
            return;
        }

        await ThreadSwitcher.ResumeBackgroundAsync();

        StatusContext.Progress($"Stitching {ManualCapturedFrames.Count} captured frames...");

        byte[] finalJpgBytes;
        try
        {
            finalJpgBytes =
                await SingleColumnImageStitcher.StitchImageBytesVertical(ManualCapturedFrames, 100,
                    StatusContext.ProgressTracker());
        }
        catch (Exception ex)
        {
            await StatusContext.ToastError($"Stitching failed: {ex.Message}");
            return;
        }

        if (finalJpgBytes.Length == 0)
        {
            await StatusContext.ToastError("Stitching produced an empty image.");
            return;
        }

        var generatedFilename = SaveAsFilename is null ? string.Empty : SaveAsFilename();

        var newFile = string.IsNullOrWhiteSpace(generatedFilename)
            ? await WebViewToJpg.SaveByteArrayAsJpg(finalJpgBytes, string.Empty, StatusContext)
            : await WebViewToJpg.SaveByteArrayAsJpgToFilename(finalJpgBytes, generatedFilename, StatusContext);

        if (!string.IsNullOrWhiteSpace(newFile))
        {
            ImageSaved?.Invoke(this,
                new InteractiveWebViewJpegImageWindowSavedEventArgs(newFile, UserUrl ?? string.Empty));

            ManualCaptureActive = false;
            ManualCapturedFrames.Clear();
            ManualCapturedCount = 0;

            if (CloseOnSave)
            {
                await ThreadSwitcher.ResumeForegroundAsync();
                Close();
            }
        }
    }

    [BlockingCommand]
    public async Task CancelManualCapture()
    {
        ManualCaptureActive = false;
        ManualCapturedFrames.Clear();
        ManualCapturedCount = 0;
        await StatusContext.ToastWarning("Manual Screen Capture cancelled.");
    }

    private void ButtonNavigate_OnClick(object sender, RoutedEventArgs e)
    {
        var binding = UrlTextBox.GetBindingExpression(TextBox.TextProperty);
        binding?.UpdateSource();
    }

    public static async Task<InteractiveWebViewJpegImageWindow> CreateInstance(StatusControlContext statusContext,
        string? initialUrl = null, Func<string>? saveAsFileName = null)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        // Check if the clipboard contains a URL
        if (Clipboard.ContainsText() && string.IsNullOrWhiteSpace(initialUrl))
        {
            var clipboardText = Clipboard.GetText();
            if (Uri.TryCreate(clipboardText, UriKind.Absolute, out var uriResult) &&
                (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
                initialUrl = clipboardText;
        }

        var newInstance =
            new InteractiveWebViewJpegImageWindow(statusContext, initialUrl ?? string.Empty, saveAsFileName);
        return newInstance;
    }

    private void DevToolsProtocolEventHandler(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
    {
        Debug.WriteLine($"DevToolsProtocolEventReceived: {e.ParameterObjectAsJson}");
    }

    public event EventHandler<InteractiveWebViewJpegImageWindowSavedEventArgs>? ImageSaved;

    [BlockingCommand]
    public async Task SaveCurrentPageAsJpeg()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (JpgScreenshotFunction == null)
        {
            await StatusContext.ToastError("Screenshot function not available...");
            return;
        }

        var screenshotResult = await JpgScreenshotFunction();

        if (screenshotResult.IsT1)
        {
            await StatusContext.ToastError(screenshotResult.AsT1.Value);
            return;
        }

        var generatedFilename = SaveAsFilename is null ? string.Empty : SaveAsFilename();

        var newFile = string.IsNullOrWhiteSpace(generatedFilename)
            ? await WebViewToJpg.SaveByteArrayAsJpg(screenshotResult.AsT0.Value, string.Empty, StatusContext)
            : await WebViewToJpg.SaveByteArrayAsJpgToFilename(screenshotResult.AsT0.Value, generatedFilename,
                StatusContext);

        if (!string.IsNullOrWhiteSpace(newFile))
            ImageSaved?.Invoke(this,
                new InteractiveWebViewJpegImageWindowSavedEventArgs(newFile, UserUrl ?? string.Empty));

        if (CloseOnSave)
        {
            await ThreadSwitcher.ResumeForegroundAsync();
            Close();
        }
    }

    private void UrlTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var binding = UrlTextBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();
        }
    }
}