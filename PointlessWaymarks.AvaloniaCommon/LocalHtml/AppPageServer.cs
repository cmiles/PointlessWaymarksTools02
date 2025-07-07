using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using PointlessWaymarks.CommonTools;

namespace PointlessWaymarks.AvaloniaCommon.LocalHtml;

public class AppPageServer : IDisposable, IAsyncDisposable
{
    private WebApplication? _app;
    private CancellationTokenSource? _cancellationTokenSource;
    public int ServerPort { get; } = FreeTcpPort();
    public string BaseUrl => $"http://localhost:{ServerPort}";
    private bool _isStarted;

    public static int FreeTcpPort()
    {
        //https://stackoverflow.com/questions/138043/find-the-next-tcp-port-in-net
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (_isStarted)
        {
            await StopServer();
        }

        _cancellationTokenSource?.Dispose();

        if (_app != null)
        {
            await _app.DisposeAsync();
            _app = null;
        }
    }

    public async Task StartServer()
    {
        if (_isStarted) return;

        var appLocalHtmlDirectory = FileLocationTools.TempStorageAppLocalHtmlDirectory().FullName;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(x => x.ListenLocalhost(ServerPort));
        builder.Services.AddDirectoryBrowser();

        _app = builder.Build();

        _app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(appLocalHtmlDirectory),
            RequestPath = ""
        });

        _app.UseDirectoryBrowser(new DirectoryBrowserOptions
        {
            FileProvider = new PhysicalFileProvider(appLocalHtmlDirectory),
            RequestPath = ""
        });

        _cancellationTokenSource = new CancellationTokenSource();

        // Start the server in the background
        _ = Task.Run(async () =>
        {
            try
            {
                await _app.RunAsync();
            }
            catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
            {
                // Expected when cancellation is requested, no need to handle
            }
        }, _cancellationTokenSource.Token);

        _isStarted = true;

        // Small delay to ensure server is ready
        await Task.Delay(100);
    }

    public async Task StopServer()
    {
        if (!_isStarted) return;

        await _cancellationTokenSource?.CancelAsync()!;

        if (_app != null)
        {
            await _app.DisposeAsync();
            _app = null;
        }

        _isStarted = false;
    }

    private string GetPageDirectoryPath(Guid id)
    {
        var baseDir = FileLocationTools.TempStorageAppLocalHtmlDirectory().FullName;
        var directory = Path.Combine(baseDir, id.ToString());
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        return Path.Combine(baseDir, id.ToString());
    }

    private string GetPageDirectoryPath()
    {
        return GetPageDirectoryPath(Guid.NewGuid());
    }

    public async Task<string> AddPage(string htmlContent)
    {
        var pageDirectory = GetPageDirectoryPath();

        // Create directory if it doesn't exist
        Directory.CreateDirectory(pageDirectory);

        // Write the content to an index.html file in the directory
        var filePath = Path.Combine(pageDirectory, "index.html");
        await File.WriteAllTextAsync(filePath, htmlContent);

        return pageDirectory;
    }

    public async Task<string> AddPage(Guid id, string htmlContent)
    {
        var pageDirectory = GetPageDirectoryPath(id);

        // Create directory if it doesn't exist
        Directory.CreateDirectory(pageDirectory);

        // Write the content to an index.html file in the directory
        var filePath = Path.Combine(pageDirectory, "index.html");
        await File.WriteAllTextAsync(filePath, htmlContent);

        return pageDirectory;
    }

    public string GetPreviewUrl(Guid id, string pageName = "index")
    {
        return $"{BaseUrl}/{id}/{pageName}.html";
    }

    public async Task AddPureCss(Guid id)
    {
        var directory = GetPageDirectoryPath(id);
        var file = Path.Combine(directory, "pure.css");
        if (File.Exists(file)) return;

        await File.WriteAllTextAsync(file, await HtmlTools.PureCssAsString());
    }

    public async Task AddMinimalCss(Guid id)
    {
        var directory = GetPageDirectoryPath(id);
        var file = Path.Combine(directory, "minimal.css");
        if (File.Exists(file)) return;

        await File.WriteAllTextAsync(file, await HtmlTools.MinimalCssAsString());
    }

    public async Task<string> ErrorPage(Exception e, string title = "Error!")
    {
        return await ErrorPage(Guid.NewGuid(), e, title);
    }

    public async Task<string> ErrorPage(Guid pageId, Exception e, string title = "Error!")
    {
        var timestamp = DateTime.Now;

        // Create an error report with enhanced details
        var exceptionHtml = new System.Text.StringBuilder();
        RenderExceptionDetails(exceptionHtml, e, 0);

        var errorHtml = $@"
        <!DOCTYPE html>
        <html lang=""en"">
        <head>
            <meta charset=""UTF-8"">
            <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
            <title>{WebUtility.HtmlEncode(title)}</title>
            <link rel=""stylesheet"" href=""minimal.css"">
            <style>
                :root {{
                    --accent: #e63946;
                }}
                body {{
                    padding: 1rem;
                }}
                .exception-block {{
                    margin-bottom: 1.5rem;
                    border-left: 4px solid var(--accent);
                    padding-left: 1rem;
                }}
                .data-table {{
                    width: 100%;
                    border-collapse: collapse;
                    margin: 1rem 0;
                }}
                .data-table th, .data-table td {{
                    text-align: left;
                    padding: 0.5rem;
                    border: 1px solid #ddd;
                }}
                .data-table th {{
                    background-color: #f5f5f5;
                }}
                .stack-trace {{
                    background: #f5f5f5;
                    padding: 1rem;
                    border-radius: 4px;
                    overflow-x: auto;
                    white-space: pre-wrap;
                    font-family: monospace;
                    font-size: 0.9rem;
                }}
                .exception-type {{
                    font-weight: bold;
                    color: var(--accent);
                }}
                .copy-button {{
                    float: right;
                    padding: 0.25rem 0.5rem;
                    background: #f5f5f5;
                    border: 1px solid #ddd;
                    border-radius: 4px;
                    cursor: pointer;
                }}
                details {{
                    margin-bottom: 1rem;
                }}
                summary {{
                    cursor: pointer;
                    padding: 0.5rem;
                    background: #f5f5f5;
                    border-radius: 4px;
                }}
                .system-info {{
                    margin-top: 2rem;
                    font-size: 0.9rem;
                    color: #666;
                }}
                .nested {{
                    margin-left: 1.5rem;
                }}
            </style>
        </head>
        <body>
            <h1>{WebUtility.HtmlEncode(title)}</h1>
            <p>An error occurred at {timestamp:yyyy-MM-dd HH:mm:ss}</p>
            
            <button class=""copy-button"" onclick=""copyFullErrorReport()"">Copy Full Report</button>
            
            <div id=""error-report"">
                {exceptionHtml}
            </div>
            
            <div class=""system-info"">
                <details>
                    <summary>System Information</summary>
                    <p>OS: {Environment.OSVersion}</p>
                    <p>.NET Runtime: {Environment.Version}</p>
                    <p>64-bit OS: {Environment.Is64BitOperatingSystem}</p>
                    <p>Machine Name: {Environment.MachineName}</p>
                    <p>Processor Count: {Environment.ProcessorCount}</p>
                    <p>Working Set: {Environment.WorkingSet / 1024 / 1024} MB</p>
                </details>
            </div>
            
            <script>
                function copyToClipboard(text) {{
                    const textarea = document.createElement('textarea');
                    textarea.value = text;
                    document.body.appendChild(textarea);
                    textarea.select();
                    document.execCommand('copy');
                    document.body.removeChild(textarea);
                }}
                
                function copyFullErrorReport() {{
                    const fullReport = document.getElementById('error-report').innerText;
                    copyToClipboard(fullReport);
                    alert('Error report copied to clipboard');
                }}
            </script>
        </body>
        </html>";

        // First add the minimal CSS to the page directory
        await AddMinimalCss(pageId);

        // Then create the HTML page
        var pageDirectory = GetPageDirectoryPath(pageId);
        var filePath = Path.Combine(pageDirectory, "index.html");
        await File.WriteAllTextAsync(filePath, errorHtml);

        return GetPreviewUrl(pageId);
    }

    private void RenderExceptionDetails(System.Text.StringBuilder html, Exception e, int nestLevel)
    {
        // Add CSS class for nested exceptions
        string nestingClass = nestLevel > 0 ? " nested" : "";

        html.AppendLine($@"<div class=""exception-block{nestingClass}"">");

        // Exception type and message
        html.AppendLine($@"<div class=""exception-type"">{WebUtility.HtmlEncode(e.GetType().FullName)}</div>");
        html.AppendLine($@"<div class=""exception-message"">{WebUtility.HtmlEncode(e.Message)}</div>");

        // Data collection (key-value pairs)
        if (e.Data.Count > 0)
        {
            html.AppendLine("<details>");
            html.AppendLine("<summary>Exception Data Collection</summary>");
            html.AppendLine(@"<table class=""data-table"">");
            html.AppendLine(@"<tr><th>Key</th><th>Value</th></tr>");

            foreach (var key in e.Data.Keys)
            {
                var value = e.Data[key];
                html.AppendLine($@"<tr>
                <td>{WebUtility.HtmlEncode(key?.ToString() ?? "null")}</td>
                <td>{WebUtility.HtmlEncode(value?.ToString() ?? "null")}</td>
            </tr>");
            }

            html.AppendLine("</table>");
            html.AppendLine("</details>");
        }

        // Additional custom properties via reflection (optional enhancement)
        var customProperties = e.GetType().GetProperties()
            .Where(p => p.Name != nameof(Exception.Message) &&
                        p.Name != nameof(Exception.InnerException) &&
                        p.Name != nameof(Exception.StackTrace) &&
                        p.Name != nameof(Exception.Data) &&
                        p.Name != nameof(Exception.Source) &&
                        p.Name != nameof(Exception.HelpLink) &&
                        p.Name != nameof(Exception.TargetSite))
            .ToList();

        if (customProperties.Count > 0)
        {
            html.AppendLine("<details>");
            html.AppendLine("<summary>Custom Exception Properties</summary>");
            html.AppendLine(@"<table class=""data-table"">");
            html.AppendLine(@"<tr><th>Property</th><th>Value</th></tr>");

            foreach (var prop in customProperties)
            {
                try
                {
                    var value = prop.GetValue(e);
                    html.AppendLine($@"<tr>
                    <td>{WebUtility.HtmlEncode(prop.Name)}</td>
                    <td>{WebUtility.HtmlEncode(value?.ToString() ?? "null")}</td>
                </tr>");
                }
                catch
                {
                    html.AppendLine($@"<tr>
                    <td>{WebUtility.HtmlEncode(prop.Name)}</td>
                    <td><em>Unable to read property</em></td>
                </tr>");
                }
            }

            html.AppendLine("</table>");
            html.AppendLine("</details>");
        }

        // Stack trace
        if (!string.IsNullOrWhiteSpace(e.StackTrace))
        {
            html.AppendLine("<details open>");
            html.AppendLine("<summary>Stack Trace</summary>");
            html.AppendLine($@"<pre class=""stack-trace"">{WebUtility.HtmlEncode(e.StackTrace)}</pre>");
            html.AppendLine("</details>");
        }

        // Inner exception (recursive)
        if (e.InnerException != null)
        {
            html.AppendLine("<details open>");
            html.AppendLine("<summary>Inner Exception</summary>");
            RenderExceptionDetails(html, e.InnerException, nestLevel + 1);
            html.AppendLine("</details>");
        }

        // AggregateException handling
        if (e is AggregateException aggregateException && aggregateException.InnerExceptions.Count > 0)
        {
            html.AppendLine("<details open>");
            html.AppendLine($"<summary>Aggregate Exceptions ({aggregateException.InnerExceptions.Count})</summary>");

            int index = 0;
            foreach (var innerEx in aggregateException.InnerExceptions)
            {
                html.AppendLine($"<h4>Exception #{++index}</h4>");
                RenderExceptionDetails(html, innerEx, nestLevel + 1);
            }

            html.AppendLine("</details>");
        }

        html.AppendLine("</div>");
    }

    public void TryRemovePage(Guid id)
    {
        var baseDir = FileLocationTools.TempStorageAppLocalHtmlDirectory().FullName;
        var directory = Path.Combine(baseDir, id.ToString());

        if (!Directory.Exists(directory)) return;

        try
        {
            Directory.Delete(directory, true);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
    
    public async Task CleanupOldPreviews(TimeSpan maxAge)
    {
        var baseDir = FileLocationTools.TempStorageAppLocalHtmlDirectory().FullName;

        if (!Directory.Exists(baseDir))
            return;

        var cutoffDate = DateTime.Now - maxAge;

        foreach (var dir in Directory.GetDirectories(baseDir))
        {
            var dirInfo = new DirectoryInfo(dir);
            if (dirInfo.LastAccessTime < cutoffDate)
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch(Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
    }
}