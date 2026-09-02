using System.Diagnostics;
using System.IO;
using PointlessWaymarks.CommonTools;
using Serilog;

namespace PointlessWaymarks.WpfCommon.PhotoPreview;

public class PhotoPreviewLauncher : IDisposable, IAsyncDisposable
{
    private Process? _previewProcess;
    private bool _disposed;

    public string ChannelId { get; }
    public PhotoPreviewIpcChannel? IpcChannel { get; private set; }

    public bool IsRunning => _previewProcess is { HasExited: false };

    public event EventHandler<PhotoItemRatingChangedIpcDto>? RatingChangedReceived;
    public event EventHandler<PhotoPreviewNavigateIpcDto>? NavigateReceived;
    public event EventHandler<PhotoPreviewFilterUnratedIpcDto>? FilterUnratedReceived;
    public event EventHandler? ProcessExited;

    public PhotoPreviewLauncher(string? channelId = null)
    {
        ChannelId = string.IsNullOrWhiteSpace(channelId) ? Guid.NewGuid().ToString("N") : channelId;
    }

    public static FileInfo? FindPreviewGuiExecutable()
    {
        var baseDir = AppContext.BaseDirectory;

        // 1. Same folder as host application (installed / published output)
        var exePath = Path.Combine(baseDir, "PointlessWaymarks.PhotoPreviewGui.exe");
        if (File.Exists(exePath)) return new FileInfo(exePath);

        // 2. Subfolder in published layout (e.g. self-contained sub-application)
        var subfolderPaths = new[]
        {
            Path.Combine(baseDir, "PointlessWaymarks.PhotoPreviewGui", "PointlessWaymarks.PhotoPreviewGui.exe"),
            Path.Combine(baseDir, "PhotoPreviewGui", "PointlessWaymarks.PhotoPreviewGui.exe"),
            Path.Combine(baseDir, "PhotoPreview_App", "PointlessWaymarks.PhotoPreviewGui.exe")
        };
        foreach (var subfolderPath in subfolderPaths)
        {
            if (File.Exists(subfolderPath)) return new FileInfo(Path.GetFullPath(subfolderPath));
        }

        // 3. Sibling folder in published layout
        var siblingPaths = new[]
        {
            Path.Combine(baseDir, "..", "PointlessWaymarks.PhotoPreviewGui", "PointlessWaymarks.PhotoPreviewGui.exe"),
            Path.Combine(baseDir, "..", "PointlessWaymarksTools", "PointlessWaymarks.PhotoPreviewGui", "PointlessWaymarks.PhotoPreviewGui.exe"),
            Path.Combine(baseDir, "..", "..", "PointlessWaymarksTools", "PointlessWaymarks.PhotoPreviewGui", "PointlessWaymarks.PhotoPreviewGui.exe")
        };
        foreach (var siblingPath in siblingPaths)
        {
            if (File.Exists(siblingPath)) return new FileInfo(Path.GetFullPath(siblingPath));
        }

        // 3. Search up parent directories (up to 8 levels) to find the solution/project directory structure
        var searchRoots = new[] { baseDir, Directory.GetCurrentDirectory(), AppDomain.CurrentDomain.BaseDirectory };

        foreach (var root in searchRoots.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct())
        {
            var searchDir = new DirectoryInfo(root);
            for (var i = 0; i < 8 && searchDir != null; i++)
            {
                var candidateTargetFolders = new[]
                {
                    Path.Combine(searchDir.FullName, "PointlessWaymarksTools", "PointlessWaymarks.PhotoPreviewGui"),
                    Path.Combine(searchDir.FullName, "PointlessWaymarks.PhotoPreviewGui")
                };

                foreach (var targetFolder in candidateTargetFolders)
                {
                    if (!Directory.Exists(targetFolder)) continue;

                    var candidatePaths = new[]
                    {
                        Path.Combine(targetFolder, "PointlessWaymarks.PhotoPreviewGui.exe"),
                        Path.Combine(targetFolder, "bin", "Debug", "net10.0-windows10.0.17763.0", "win-x64", "PointlessWaymarks.PhotoPreviewGui.exe"),
                        Path.Combine(targetFolder, "bin", "Release", "net10.0-windows10.0.17763.0", "win-x64", "PointlessWaymarks.PhotoPreviewGui.exe"),
                        Path.Combine(targetFolder, "bin", "Debug", "net10.0-windows", "win-x64", "PointlessWaymarks.PhotoPreviewGui.exe"),
                        Path.Combine(targetFolder, "bin", "Release", "net10.0-windows", "win-x64", "PointlessWaymarks.PhotoPreviewGui.exe"),
                        Path.Combine(targetFolder, "bin", "x64", "Debug", "net10.0-windows10.0.17763.0", "PointlessWaymarks.PhotoPreviewGui.exe"),
                        Path.Combine(targetFolder, "bin", "x64", "Release", "net10.0-windows10.0.17763.0", "PointlessWaymarks.PhotoPreviewGui.exe"),
                        Path.Combine(targetFolder, "bin", "x64", "Debug", "PointlessWaymarks.PhotoPreviewGui.exe"),
                        Path.Combine(targetFolder, "bin", "x64", "Release", "PointlessWaymarks.PhotoPreviewGui.exe")
                    };

                    foreach (var candidate in candidatePaths)
                    {
                        if (File.Exists(candidate)) return new FileInfo(Path.GetFullPath(candidate));
                    }

                    var binDir = Path.Combine(targetFolder, "bin");
                    if (Directory.Exists(binDir))
                    {
                        var foundFiles = Directory.GetFiles(binDir, "PointlessWaymarks.PhotoPreviewGui.exe", SearchOption.AllDirectories);
                        if (foundFiles.Length > 0)
                        {
                            return foundFiles.Select(x => new FileInfo(x))
                                .OrderByDescending(x => x.LastWriteTimeUtc)
                                .First();
                        }
                    }
                }

                searchDir = searchDir.Parent;
            }
        }

        return null;
    }

    public async Task<bool> EnsureRunningAsync(string? initialFilePath = null, string? initialTitle = null, int initialRating = 0)
    {
        if (IsRunning) return true;

        var exe = FindPreviewGuiExecutable();
        if (exe == null || !exe.Exists)
        {
            Log.Error("Could not locate PointlessWaymarks.PhotoPreviewGui.exe");
            return false;
        }

        if (IpcChannel == null)
        {
            IpcChannel = new PhotoPreviewIpcChannel(ChannelId);
            IpcChannel.RatingChangedReceived += (s, e) => RatingChangedReceived?.Invoke(this, e);
            IpcChannel.NavigateReceived += (s, e) => NavigateReceived?.Invoke(this, e);
            IpcChannel.FilterUnratedReceived += (s, e) => FilterUnratedReceived?.Invoke(this, e);
        }

        var arguments = $"--channel-id \"{ChannelId}\"";
        if (!string.IsNullOrWhiteSpace(initialFilePath) && File.Exists(initialFilePath))
        {
            arguments += $" --file \"{initialFilePath}\"";
            if (!string.IsNullOrWhiteSpace(initialTitle)) arguments += $" --title \"{initialTitle}\"";
            arguments += $" --rating {initialRating}";
        }

        var psi = new ProcessStartInfo
        {
            FileName = exe.FullName,
            Arguments = arguments,
            UseShellExecute = true
        };

        try
        {
            _previewProcess = Process.Start(psi);
            if (_previewProcess != null)
            {
                _previewProcess.EnableRaisingEvents = true;
                _previewProcess.Exited += (s, e) => ProcessExited?.Invoke(this, EventArgs.Empty);
            }

            // Brief pause to allow message bus subscription
            await Task.Delay(200);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start PhotoPreviewGui process");
            return false;
        }
    }

    public void SendPreviewRequest(string filePath, string title, int rating, List<string>? upcomingFilePaths = null)
    {
        IpcChannel?.PublishPreviewRequest(filePath, title, rating, upcomingFilePaths);
    }

    public void SendClearPreview()
    {
        IpcChannel?.PublishClearPreview();
    }

    public void SendFilterUnrated(bool filterUnratedOnly)
    {
        IpcChannel?.PublishFilterUnrated(filterUnratedOnly);
    }

    public void Close()
    {
        try
        {
            IpcChannel?.PublishClose();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error sending close command to PhotoPreviewGui");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Close();
        IpcChannel?.Dispose();
        _previewProcess?.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        Close();
        if (IpcChannel != null) await IpcChannel.DisposeAsync();
        _previewProcess?.Dispose();
        GC.SuppressFinalize(this);
    }
}
