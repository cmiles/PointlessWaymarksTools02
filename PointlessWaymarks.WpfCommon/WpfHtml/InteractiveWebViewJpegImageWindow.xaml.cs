using System.Diagnostics;
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
            ImageSaved?.Invoke(this, new InteractiveWebViewJpegImageWindowSavedEventArgs(newFile, UserUrl ?? string.Empty));

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