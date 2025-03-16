using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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
    public bool UpdateRunning { get; set; }
    public string UpdateVersion { get; set; } = string.Empty;

    public event EventHandler? UpdateDialogCompleted;

    [BlockingCommand]
    public async Task Dismiss()
    {
        await UiThreadSwitcher.ResumeBackgroundAsync();

        UpdateDialogCompleted?.Invoke(this, EventArgs.Empty);
        ShowMessage = false;
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
    public async Task LoadData(string? currentVersion, string? updateVersion, string? setupFile)
    {
        await UiThreadSwitcher.ResumeBackgroundAsync();

        CurrentVersion = currentVersion ?? string.Empty;
        UpdateVersion = updateVersion ?? string.Empty;
        SetupFile = setupFile ?? string.Empty;

        if (string.IsNullOrWhiteSpace(CurrentVersion) || string.IsNullOrWhiteSpace(UpdateVersion) ||
            string.IsNullOrWhiteSpace(SetupFile) ||
            string.Compare(CurrentVersion, UpdateVersion, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            UpdateDialogCompleted?.Invoke(this, EventArgs.Empty);
            ShowMessage = false;
            return;
        }

        UpdateMessage = SetupFile.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? $"Update Available! Download Update, Close Program and Update From {CurrentVersion} to {UpdateVersion} now? Make sure all work is saved first... {Environment.NewLine}{Environment.NewLine}Source: {SetupFile}"
            : $"Update Available! Close Program and Update From {CurrentVersion} to {UpdateVersion} now? Make sure all work is saved first...";

        ShowMessage = true;

        Log.ForContext(nameof(ProgramUpdateMessageContext), this.SafeObjectDump())
            .Information("Program Update Message Context Loaded - Show Update Message {showUpdate}", ShowMessage);
    }

    [BlockingCommand]
    public async Task Update()
    {
        if (UpdateRunning) return;

        UpdateRunning = true;

        try
        {
            await UiThreadSwitcher.ResumeBackgroundAsync();

            string localFile;

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
        catch (Exception e)
        {
            ShowMessage = false;
            UpdateRunning = false;
            throw new Exception($"Unexpected Problem with Program Update: {e.Message}");
        }
    }
}