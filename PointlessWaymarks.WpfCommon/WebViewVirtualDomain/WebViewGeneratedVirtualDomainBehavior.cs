using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Core.DevToolsProtocolExtension;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Xaml.Behaviors;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.WpfCommon.Utility;
using Console = System.Console;
using Log = Serilog.Log;

namespace PointlessWaymarks.WpfCommon.WebViewVirtualDomain;

/// <summary>
///     This behavior targets dealing with string Html binding and sending Json data to a WebView2 - the challenges to this
///     really arise from the WebView2's initialization which can be distinctly delayed on first load, and the time to load
///     the Html during which the Javascript may not yet be active to process Json data. This is a particular pain point in
///     a View Model setup where the model doesn't have direct access to the WebView2.
/// </summary>
public class WebViewGeneratedVirtualDomainBehavior : Behavior<WebView2CompositionControl>
{
    /// <summary>
    ///     String HTML - when changed the WebView2 will be reloaded with the new string
    /// </summary>
    public static readonly DependencyProperty WebViewMessengerProperty = DependencyProperty.Register(
        nameof(WebViewMessenger),
        typeof(IWebViewMessenger), typeof(WebViewGeneratedVirtualDomainBehavior),
        new PropertyMetadata(null, OnWebViewManagerChanged));

    public static readonly DependencyProperty DeferNavigationToProperty = DependencyProperty.Register(
        nameof(DeferNavigationTo),
        typeof(Action<Uri, string>), typeof(WebViewGeneratedVirtualDomainBehavior),
        new PropertyMetadata(default(Action<Uri, string>)));

    public static readonly DependencyProperty RedirectExternalLinksToBrowserProperty = DependencyProperty.Register(
        nameof(RedirectExternalLinksToBrowser),
        typeof(bool), typeof(WebViewGeneratedVirtualDomainBehavior),
        new PropertyMetadata(false));


    private readonly SemaphoreSlim _loadGuard = new(1, 1);

    // Example Usage in Xaml
    // <b:Interaction.Behaviors>
    //      <wpfHtml:WebViewHtmlStringAndJsonMessagingBehavior WebViewJsonMessenger="{Binding .}" HtmlString="{Binding MapHtml}" />
    // </b:Interaction.Behaviors>

    private readonly DirectoryInfo _targetDirectory;


    private string _lastToWebNavigationUrl = "";
    private string _virtualDomain = "localweb.pointlesswaymarks.com";
    private bool _webViewHasLoaded;

    public WebViewGeneratedVirtualDomainBehavior()
    {
        var htmlTempDirectory = FileLocationTools.TempStorageWebViewVirtualDomainDirectory();
        if (!File.Exists(Path.Combine(htmlTempDirectory.FullName, "favicon.ico")))
            File.WriteAllText(Path.Combine(htmlTempDirectory.FullName, "favicon.ico"), HtmlTools.FavIconIco());
        _targetDirectory = UniqueFileTools
            .UniqueRandomLetterNameDirectory(htmlTempDirectory.FullName, 4);
    }

    public Action<Uri, string>? DeferNavigationTo
    {
        get => (Action<Uri, string>?)GetValue(DeferNavigationToProperty);
        set => SetValue(DeferNavigationToProperty, value);
    }

    public bool RedirectExternalLinksToBrowser
    {
        get => (bool)GetValue(RedirectExternalLinksToBrowserProperty);
        set => SetValue(RedirectExternalLinksToBrowserProperty, value);
    }

    public IWebViewMessenger? WebViewMessenger
    {
        get => (IWebViewMessenger)GetValue(WebViewMessengerProperty);
        set => SetValue(WebViewMessengerProperty, value);
    }

    private void CoreWebView2OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        Debug.WriteLine($"Resource Requested: {e.Request.Uri}");
    }

    private void DevToolsProtocolEventHandler(object? sender, CoreWebView2DevToolsProtocolEventReceivedEventArgs e)
    {
        Debug.WriteLine($"DevToolsProtocolEventReceived: {e.ParameterObjectAsJson}");
    }

    protected override void OnAttached()
    {
        _virtualDomain = $"localweb.pointlesswaymarks.com/{_targetDirectory.Name}";

        AssociatedObject.Loaded += OnLoaded;
        AssociatedObject.CoreWebView2InitializationCompleted += OnCoreWebView2InitializationCompleted;
    }

    private async void OnCoreWebView2InitializationCompleted(object? sender,
        CoreWebView2InitializationCompletedEventArgs e)
    {
        try
        {
            // Exit early if initialization failed
            if (!e.IsSuccess)
            {
                Log.Error("CoreWebView2 initialization failed: {Error}",
                    e.InitializationException?.Message ?? "Unknown error");
                return;
            }

            await ThreadSwitcher.ResumeForegroundAsync();

            AssociatedObject.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "localweb.pointlesswaymarks.com",
                _targetDirectory.Parent!.FullName
                , CoreWebView2HostResourceAccessKind.Allow);

            AssociatedObject.CoreWebView2.GetDevToolsProtocolEventReceiver("Log.entryAdded")
                .DevToolsProtocolEventReceived += DevToolsProtocolEventHandler;
            await AssociatedObject.CoreWebView2.CallDevToolsProtocolMethodAsync("Log.enable", "{}");

            AssociatedObject.NavigationStarting += WebView_OnNavigationStarting;
            AssociatedObject.CoreWebView2.WebMessageReceived += OnCoreWebView2OnWebMessageReceived;
            AssociatedObject.CoreWebView2.WebResourceRequested += CoreWebView2OnWebResourceRequested;

            WebViewMessenger?.ToWebView.Suspend(false);
        }
        catch (Exception ex)
        {
            // Log the error but don't rethrow - this prevents the application from crashing
            Log.Error(ex, "Error in CoreWebView2 initialization: {Message}", ex.Message);
            Debug.WriteLine($"CoreWebView2 initialization error: {ex}");

            // Try to inform the user if possible
            try
            {
                await ThreadSwitcher.ResumeForegroundAsync();
                MessageBox.Show(
                    $"Error initializing WebView2: {ex.Message}\n\nPlease check your WebView2 installation.",
                    "WebView2 Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // Last resort - just log that we couldn't even show the error
                Log.Error("Could not show WebView2 initialization error message to user");
            }
        }
    }

    /// <summary>
    ///     Transforms and redirects the WebView2 Message Received Event to this object's OnJsonFromWebView event
    /// </summary>
    /// <param name="o"></param>
    /// <param name="args"></param>
    private async void OnCoreWebView2OnWebMessageReceived(object? o, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            await ThreadSwitcher.ResumeForegroundAsync();

            if (args.WebMessageAsJson.Contains("scriptFinished"))
            {
                Debug.WriteLine("scriptFinished Received");
                WebViewMessenger?.ToWebView.Suspend(false);
                return;
            }

            WebViewMessenger?.FromWebView.Enqueue(new FromWebViewMessage(args.WebMessageAsJson));
        }
        catch (Exception ex)
        {
            // Log the error but don't rethrow - this prevents the application from crashing
            Log.Error(ex, "Error processing WebView message: {Message}", ex.Message);
            Debug.WriteLine($"WebView message processing error: {ex}");
        }
    }

    /// <summary>
    ///     Set up the web environment.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _loadGuard.WaitAsync();

            if (!_webViewHasLoaded)
                try
                {
                    await ThreadSwitcher.ResumeForegroundAsync();

                    await AssociatedObject.EnsureCoreWebView2Async();

                    _webViewHasLoaded = true;
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    Log.Error(exception, "Error in the OnLoaded method with the WebView2.");
                }
                finally
                {
                    _loadGuard.Release();
                }
        }
        catch (Exception ex)
        {
            // This catch block handles exceptions that might occur with the semaphore or before entering the inner try block
            Log.Error(ex, "Unhandled error in WebView OnLoaded method: {Message}", ex.Message);
            Debug.WriteLine($"WebView OnLoaded error: {ex}");

            // Ensure semaphore is released even if an exception occurs before the inner try block
            if (_loadGuard.CurrentCount == 0)
                try
                {
                    _loadGuard.Release();
                }
                catch (SemaphoreFullException)
                {
                    // This would happen if the semaphore was somehow already released
                    Log.Warning("Attempted to release semaphore that was already at max count");
                }
        }
    }

    private static async void OnWebViewManagerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        try
        {
            if (d is WebViewGeneratedVirtualDomainBehavior bindingBehavior &&
                e.NewValue is IWebViewMessenger newMessenger)
            {
                await ThreadSwitcher.ResumeForegroundAsync();

                bindingBehavior.WebViewMessenger = newMessenger;

                bindingBehavior.WebViewMessenger.ToWebView.Suspend(!bindingBehavior._webViewHasLoaded);

                newMessenger.ToWebView.Processor = bindingBehavior.ToWebViewMessageProcessor;
            }
        }
        catch (Exception ex)
        {
            // Log the error but don't rethrow - this prevents the application from crashing
            Log.Error(ex, "Error in WebView manager change handler: {Message}", ex.Message);
            Debug.WriteLine($"WebView manager change error: {ex}");
        }
    }

    private async Task ProcessToWebJavaScriptExecute(ExecuteJavaScript javaScriptRequest)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        if (WebViewMessenger is null)
        {
            Debug.WriteLine(
                $"{nameof(ProcessToWebJavaScriptExecute)}  - WebViewMessenger IS NULL, Nothing will be processed!! - Tag {javaScriptRequest.RequestTag} - javascript Starts: {javaScriptRequest.JavaScriptToExecute[..Math.Min(javaScriptRequest.JavaScriptToExecute.Length, 512)]}");
            return;
        }

        Debug.WriteLine(
            $"{nameof(ProcessToWebJavaScriptExecute)}  - Suspended: {WebViewMessenger.ToWebView.Suspended} - Tag {javaScriptRequest.RequestTag} - javascript Starts: {javaScriptRequest.JavaScriptToExecute[..Math.Min(javaScriptRequest.JavaScriptToExecute.Length, 512)]}");

        if (!string.IsNullOrWhiteSpace(javaScriptRequest.JavaScriptToExecute))
        {
            if (javaScriptRequest.WaitForScriptFinished) WebViewMessenger.ToWebView.Suspend(true);

            //As far as I can tell the WebView2 ExecuteScriptWithResultAsync and ExecuteScriptAsync don't help you with
            //resolving promises?
            //https://github.com/MicrosoftEdge/WebView2Feedback/issues/416
            //https://github.com/MicrosoftEdge/WebView2Feedback/issues/2295
            var helper = AssociatedObject.CoreWebView2.GetDevToolsProtocolHelper();
            //TODO: returnByValue here?
            var javascriptResult = await helper.Runtime.EvaluateAsync(javaScriptRequest.JavaScriptToExecute,
                awaitPromise: true, returnByValue: true);

            //TODO:Better error and result handling
            if (javascriptResult?.ExceptionDetails != null)
                throw new Exception(javascriptResult.ExceptionDetails.ToString());
        }
    }

    private async Task ProcessToWebViewFileBuilder(FileBuilder fileBuilder)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        if (WebViewMessenger is null)
        {
            Debug.WriteLine(
                $"{nameof(ProcessToWebViewFileBuilder)}  - WebViewMessenger IS NULL, Nothing will be processed!! -- Tag {fileBuilder.RequestTag} - Create {fileBuilder.Create.Count}, Copy {fileBuilder.Copy.Count}");
            return;
        }

        Debug.WriteLine(
            $"{nameof(ProcessToWebViewFileBuilder)} - Tag {fileBuilder.RequestTag} - Create {fileBuilder.Create.Count}, Copy {fileBuilder.Copy.Count} - Suspended: {WebViewMessenger.ToWebView.Suspended}");

        await ThreadSwitcher.ResumeBackgroundAsync();

        foreach (var loopCreate in fileBuilder.Create)
        {
            var targetFile = new FileInfo(Path.Combine(_targetDirectory.FullName, loopCreate.FileName));

            if (!targetFile.Exists)
            {
                if (!targetFile.Directory?.Exists ?? false) targetFile.Directory?.Create();

                try
                {
                    if (loopCreate.Content.IsT0)
                        await File.WriteAllTextAsync(targetFile.FullName,
                            loopCreate.Content.AsT0.Replace("[[VirtualDomain]]", _virtualDomain,
                                StringComparison.OrdinalIgnoreCase));
                    if (loopCreate.Content.IsT1)
                        await File.WriteAllBytesAsync(targetFile.FullName, loopCreate.Content.AsT1);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                continue;
            }

            //We know the file exists at this point
            if (!loopCreate.TryToOverwrite) continue;

            try
            {
                targetFile.Delete();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Log.Verbose(
                    "Silent Error in {method} - Create Branch - trying to delete file {file}, some errors are expected...",
                    nameof(ProcessToWebViewFileBuilder), targetFile);
            }

            if (loopCreate.Content.IsT0)
                await File.WriteAllTextAsync(targetFile.FullName,
                    loopCreate.Content.AsT0.Replace("[[VirtualDomain]]", _virtualDomain,
                        StringComparison.OrdinalIgnoreCase));
            if (loopCreate.Content.IsT1)
                await File.WriteAllBytesAsync(targetFile.FullName, loopCreate.Content.AsT1);
        }

        foreach (var loopCopy in fileBuilder.Copy)
        {
            var targetFile = Path.Combine(_targetDirectory.FullName, Path.GetFileName(loopCopy.FileToCopy));

            if (!File.Exists(targetFile))
            {
                File.Copy(loopCopy.FileToCopy, targetFile);
                continue;
            }

            //We know the file exists at this point
            if (!loopCopy.TryToOverwrite) continue;

            try
            {
                File.Delete(targetFile);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Log.Verbose(
                    "Silent Error in {method} - Copy Branch - trying to delete file {file}, some errors are expected...",
                    nameof(ProcessToWebViewFileBuilder), targetFile);
            }

            File.Copy(loopCopy.FileToCopy, targetFile);
        }
    }

    private async Task ProcessToWebViewJson(JsonData jsonData)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        if (WebViewMessenger is null)
        {
            Debug.WriteLine(
                $"{nameof(ProcessToWebViewJson)}  - WebViewMessenger IS NULL, Nothing will be processed!! - Tag {jsonData.RequestTag} - Json Starts: {jsonData.Json[..Math.Min(jsonData.Json.Length, 100)]}");
            return;
        }

        Debug.WriteLine(
            $"{nameof(ProcessToWebViewJson)} - Suspended: {WebViewMessenger.ToWebView.Suspended} - Tag {jsonData.RequestTag} - Json Starts: {jsonData.Json[..Math.Min(jsonData.Json.Length, 100)]}");

        if (!string.IsNullOrWhiteSpace(jsonData.Json))
        {
            var jsonModified =
                jsonData.Json.Replace("[[VirtualDomain]]", _virtualDomain, StringComparison.OrdinalIgnoreCase);
            AssociatedObject.CoreWebView2.PostWebMessageAsJson(jsonModified);
        }
    }

    private async Task ProcessToWebViewNavigation(NavigateTo navigateTo)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        if (WebViewMessenger is null)
        {
            Debug.WriteLine(
                $"{nameof(ProcessToWebViewNavigation)}  - WebViewMessenger IS NULL, Nothing will be processed!! - Tag {navigateTo.RequestTag} - To: {navigateTo.Url} - WaitForScriptFinished: {navigateTo.WaitForScriptFinished}");
            return;
        }

        Debug.WriteLine(
            $"{nameof(ProcessToWebViewNavigation)} - Suspended: {WebViewMessenger.ToWebView.Suspended} - Tag {navigateTo.RequestTag} - To: {navigateTo.Url} - WaitForScriptFinished: {navigateTo.WaitForScriptFinished}");

        if (!string.IsNullOrWhiteSpace(navigateTo.Url))
        {
            if (navigateTo.WaitForScriptFinished) WebViewMessenger.ToWebView.Suspend(true);

            _lastToWebNavigationUrl = $"https://{_virtualDomain}/{navigateTo.Url}";

            AssociatedObject.CoreWebView2.Navigate(
                $"https://{_virtualDomain}/{navigateTo.Url}");
        }
    }

    private async Task ToWebViewMessageProcessor(ToWebViewRequest arg)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        await arg.Match(
            ProcessToWebViewFileBuilder,
            ProcessToWebViewNavigation,
            ProcessToWebViewJson,
            ProcessToWebJavaScriptExecute
        );
    }

    private async void WebView_OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        try
        {
            await ThreadSwitcher.ResumeForegroundAsync();

            if (e.NavigationKind is CoreWebView2NavigationKind.BackOrForward)
            {
                e.Cancel = true;
                return;
            }

            if (_lastToWebNavigationUrl == e.Uri) return;

            var navigationUri = new Uri(e.Uri);

            if (DeferNavigationTo != null && e.IsUserInitiated)
            {
                e.Cancel = true;
                DeferNavigationTo(navigationUri, _virtualDomain);
                return;
            }

            if (RedirectExternalLinksToBrowser && !navigationUri.Host.StartsWith(_virtualDomain))
            {
                e.Cancel = true;
                ProcessHelpers.OpenUrlInExternalBrowser(e.Uri);
            }
        }
        catch (Exception ex)
        {
            // Log the error but don't rethrow - this prevents the application from crashing
            Log.Error(ex, "Error handling WebView navigation: {Message}", ex.Message);
            Debug.WriteLine($"WebView navigation error: {ex}");

            // Try to prevent navigation if we encountered an error (safer default)
            try
            {
                e.Cancel = true;
            }
            catch
            {
                // Last resort if we can't even cancel
                Log.Error("Could not cancel navigation after error");
            }
        }
    }
}