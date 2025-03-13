using System.Diagnostics;
using System.Net.Mime;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Flurl.Http;
using PointlessWaymarks.AvaloniaToolkit.StatusLayer;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using Serilog;

namespace PointlessWaymarks.AvaloniaToolkit.ProgramUpdateMessage;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class ProgramUpdateMessageContext
{
    public ProgramUpdateMessageContext(StatusLayerContext statusContext)
    {
        StatusContext = statusContext;
        BuildCommands();
    }

    public string CurrentVersion { get; set; } = string.Empty;
    public string Progress { get; set; } = string.Empty;
    public string SetupFile { get; set; } = string.Empty;
    public bool ShowMessage { get; set; }
    public StatusLayerContext StatusContext { get; set; }
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
            ? $"Update Available! Download Update, Close Program and Update From {CurrentVersion} to {UpdateVersion} now? Make sure all work is saved first... {Environment.NewLine}{Environment.NewLine}Source: {SetupFile}"
            : $"Update Available! Close Program and Update From {CurrentVersion} to {UpdateVersion} now? Make sure all work is saved first...";

        ShowMessage = true;

        Log.ForContext(nameof(ProgramUpdateMessageContext), this.SafeObjectDump())
            .Information("Program Update Message Context Loaded - Show Update Message {showUpdate}", ShowMessage);

        return Task.CompletedTask;
    }

    [BlockingCommand]
    public async Task Update()
    {
        var localFile = string.Empty;

        if (SetupFile.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            Progress = $"Setting Up Download of {SetupFile}";

            var fileName = GetFileNameFromUrl(SetupFile);
            localFile = Path.Combine(GetUserDownloadDirectory(), fileName);

            await DownloadTools.Download(SetupFile, localFile, (total, current, percent) =>
            {
                Progress =
                    $"Download Progress:{Environment.NewLine}File Size {FileAndFolderTools.GetBytesReadable(total ?? 0)}{Environment.NewLine}Downloaded {FileAndFolderTools.GetBytesReadable(current)}{Environment.NewLine}Percent Complete {percent:P0}";
                return false;
            });

            Progress = $"Update File {SetupFile} saved to {localFile}";

            Log.Information("Update File {0} saved to {1}", SetupFile, localFile);
        }
        else
        {
            localFile = SetupFile;
        }

        await UiThreadSwitcher.ResumeForegroundAsync();

        Progress = $"Starting Update Process - {localFile}";
        Process.Start(localFile);

        Progress = "Update Process Started - Closing Program";
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopApp)
            desktopApp.Shutdown();
    }
}