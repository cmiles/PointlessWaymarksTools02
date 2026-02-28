using System.Diagnostics;
using System.IO;
using System.Net.Mime;
using System.Windows;
using Flurl.Http;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon.Status;
using Serilog;

namespace PointlessWaymarks.WpfCommon.ProgramUpdateMessage;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class ProgramUpdateMessageContext
{
    public ProgramUpdateMessageContext(StatusControlContext statusContext)
    {
        StatusContext = statusContext;
        BuildCommands();
    }

    public string CurrentVersion { get; set; } = string.Empty;
    public long DownloadedBytes { get; set; }
    public bool DownloadIsIndeterminate { get; set; }
    public string DownloadProgressMessage { get; set; } = string.Empty;
    public double DownloadProgressPercent { get; set; }
    public bool IsDownloading { get; set; }
    public string SetupFile { get; set; } = string.Empty;
    public bool ShowMessage { get; set; }
    public StatusControlContext StatusContext { get; set; }
    public long TotalBytes { get; set; }
    public string UpdateMessage { get; set; } = string.Empty;
    public string UpdateVersion { get; set; } = string.Empty;

    [BlockingCommand]
    public Task Dismiss()
    {
        ShowMessage = false;
        return Task.CompletedTask;
    }

    private static string? GetFileNameFromResponse(IFlurlResponse response)
    {
        var contentDisposition = response.Headers.FirstOrDefault(h =>
            h.Name.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(contentDisposition.Value))
        {
            var fileName = new ContentDisposition(contentDisposition.Value).FileName;
            return fileName?.Trim('"');
        }

        return null;
    }

    private static string GetFileNameFromUrl(string url)
    {
        return Path.GetFileName(new Uri(url).LocalPath);
    }

    public static string GetUserDownloadDirectory()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    //Async expected on this method by convention
    public Task LoadData(string? currentVersion, string? updateVersion, string? setupFile)
    {
        CurrentVersion = currentVersion ?? string.Empty;
        UpdateVersion = updateVersion ?? string.Empty;
        SetupFile = setupFile ?? string.Empty;

        if (string.IsNullOrWhiteSpace(CurrentVersion) || string.IsNullOrWhiteSpace(UpdateVersion) ||
            string.IsNullOrWhiteSpace(SetupFile) ||
            string.Compare(CurrentVersion, UpdateVersion, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            ShowMessage = false;
            return Task.CompletedTask;
        }

        UpdateMessage = SetupFile.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? $"Update Available! Download Update ({SetupFile}), Close Program and Update From {CurrentVersion} to {UpdateVersion} now? Make sure all work is saved first..."
            : $"Update Available! Close Program and Update From {CurrentVersion} to {UpdateVersion} now? Make sure all work is saved first...";


        ShowMessage = true;

        Log.ForContext(nameof(ProgramUpdateMessageContext), this.SafeObjectDump())
            .Information("Program Update Message Context Loaded - Show Update Message {showUpdate}", ShowMessage);

        return Task.CompletedTask;
    }

    [BlockingCommand]
    public async Task Update()
    {
        string localFile;

        if (SetupFile.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var response = await SetupFile.GetAsync();
            var contentLength = response.ResponseMessage.Content.Headers.ContentLength;
            var fileName = GetFileNameFromResponse(response) ?? GetFileNameFromUrl(SetupFile);
            var filePath = Path.Combine(GetUserDownloadDirectory(), fileName);

            TotalBytes = contentLength ?? -1;
            DownloadedBytes = 0;
            DownloadProgressPercent = 0;
            DownloadIsIndeterminate = TotalBytes <= 0;
            DownloadProgressMessage = TotalBytes > 0
                ? $"Starting download of {fileName} ({FileAndFolderTools.GetBytesReadable(TotalBytes)})..."
                : $"Starting download of {fileName}...";
            IsDownloading = true;

            try
            {
                using var responseStream = await response.ResponseMessage.Content.ReadAsStreamAsync();
                await using var fileStream = new FileStream(filePath, FileMode.Create,
                    FileAccess.Write, FileShare.None, 81_920, true);

                var buffer = new byte[81_920];
                int bytesRead;
                while ((bytesRead = await responseStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    DownloadedBytes += bytesRead;

                    if (TotalBytes > 0)
                    {
                        DownloadProgressPercent = (double)DownloadedBytes / TotalBytes * 100;
                        DownloadProgressMessage =
                            $"Downloading {FileAndFolderTools.GetBytesReadable(DownloadedBytes)} of {FileAndFolderTools.GetBytesReadable(TotalBytes)} ({DownloadProgressPercent:N0}%)";
                    }
                    else
                    {
                        DownloadProgressMessage =
                            $"Downloading {FileAndFolderTools.GetBytesReadable(DownloadedBytes)}...";
                    }
                }
            }
            finally
            {
                IsDownloading = false;
            }

            DownloadProgressMessage = $"Download complete: {FileAndFolderTools.GetBytesReadable(DownloadedBytes)}";
            localFile = filePath;

            Log.Information("Update File {0} saved to {1}", SetupFile, filePath);
        }
        else
        {
            localFile = SetupFile;
        }

        await ThreadSwitcher.ResumeForegroundAsync();

        Process.Start(localFile);

        Application.Current.Shutdown();
    }
}