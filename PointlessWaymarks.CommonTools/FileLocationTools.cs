using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

namespace PointlessWaymarks.CommonTools;

public static class FileLocationTools
{
    public static readonly string FfmpegExeName = "ffmpeg.exe";
    public static readonly string FfprobeExeName = "ffprobe.exe";

    /// <summary>
    ///     This returns the default Pointless Waymarks storage directory - currently in the users
    ///     My Documents in a Pointless Waymarks Cms Folder - this will return the same value regardless
    ///     of settings, site locations, etc...
    /// </summary>
    /// <returns></returns>
    public static DirectoryInfo DefaultAssetsStorageDirectory()
    {
        var directory =
            new DirectoryInfo(Path.Combine(DefaultStorageDirectory().FullName,
                "Assets"));

        if (!directory.Exists) directory.Create();

        directory.Refresh();

        return directory;
    }

    public static DirectoryInfo DefaultErrorReportsDirectory()
    {
        var baseDirectory = DefaultStorageDirectory();

        var directory =
            new DirectoryInfo(Path.Combine(baseDirectory.FullName, "Error-Reports"));

        if (!directory.Exists) directory.Create();

        directory.Refresh();

        return directory;
    }

    public static DirectoryInfo DefaultExifToolStorageDirectory()
    {
        var directory =
            new DirectoryInfo(Path.Combine(DefaultStorageDirectory().FullName,
                "ExifTool"));

        if (!directory.Exists) directory.Create();

        directory.Refresh();

        return directory;
    }

    public static FileInfo DefaultFeatureIntersectSettingsFile()
    {
        var file =
            new FileInfo(Path.Combine(DefaultStorageDirectory().FullName,
                "PwGtgFeatureIntersectTaggerSettings.json"));

        return file;
    }

    public static DirectoryInfo DefaultFfmpegStorageDirectory()
    {
        var directory =
            new DirectoryInfo(Path.Combine(DefaultStorageDirectory().FullName,
                "FFmpeg"));

        if (!directory.Exists) directory.Create();

        directory.Refresh();

        return directory;
    }

    public static (bool exists, FileInfo? executable) FfmpegExecutableExists(DirectoryInfo? directory)
    {
        if (directory is not { Exists: true }) return (false, null);
        var file = new FileInfo(Path.Combine(directory.FullName, FfmpegExeName));
        return file.Exists ? (true, file) : (false, null);
    }

    public static (bool exists, FileInfo? executable) FfprobeExecutableExists(DirectoryInfo? directory)
    {
        if (directory is not { Exists: true }) return (false, null);
        var file = new FileInfo(Path.Combine(directory.FullName, FfprobeExeName));
        return file.Exists ? (true, file) : (false, null);
    }

    /// <summary>
    ///     This returns the default Pointless Waymarks storage directory - currently in the users
    ///     My Documents in a Pointless Waymarks Cms Folder - this will return the same value regardless
    ///     of settings, site locations, etc...
    /// </summary>
    /// <returns></returns>
    public static DirectoryInfo DefaultLogStorageDirectory()
    {
        var baseDirectory = DefaultStorageDirectory();

        var directory = new DirectoryInfo(Path.Combine(baseDirectory.FullName, "PwLogs"));

        if (!directory.Exists) directory.Create();

        directory.Refresh();

        return directory;
    }

    /// <summary>
    ///     This returns the default Pointless Waymarks Project storage directory - currently in the users
    ///     My Documents in a Pointless Waymarks Project Folder - this will return the same value regardless
    ///     of settings, site locations, etc...
    /// </summary>
    /// <returns></returns>
    public static DirectoryInfo DefaultStorageDirectory()
    {
        var directory =
            new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Pointless Waymarks Project"));

        if (!directory.Exists) directory.Create();

        directory.Refresh();

        return directory;
    }

    public static async Task<(bool Success, string Message, FileInfo? ExifToolExe)> FindDownloadUpdateExifTool(
        string? directory = null,
        IProgress<string>? progress = null)
    {
        progress?.Report("Checking ExifTool configuration...");

        if (string.IsNullOrWhiteSpace(directory)) directory = DefaultExifToolStorageDirectory().FullName;

        var targetDir = new DirectoryInfo(directory);
        if (!targetDir.Exists)
        {
            targetDir.Create();
            targetDir.Refresh();
        }

        const string versionUrl =
            "https://oliverbetz.de/cms/files/Artikel/ExifTool-for-Windows/exiftool_latest_version.txt";

        string remoteVersion;
        using (var http = new HttpClient())
        {
            remoteVersion = (await http.GetStringAsync(versionUrl).ConfigureAwait(false)).Trim();
        }

        var (existingFound, existingExe) = ExifToolExecutableExists(targetDir);
        var localVersion = await GetExifToolVersion(existingExe);

        if (existingFound && !string.IsNullOrWhiteSpace(localVersion) &&
            string.Equals(localVersion, remoteVersion, StringComparison.OrdinalIgnoreCase))
        {
            progress?.Report($"ExifTool {localVersion} already available at {existingExe!.FullName}.");
            return (true, "ExifTool already up to date.", existingExe);
        }

        string? tempZipPath = null;

        try
        {
            progress?.Report(
                existingFound
                    ? $"Updating ExifTool from {localVersion ?? "unknown"} to {remoteVersion}..."
                    : $"Downloading ExifTool {remoteVersion}...");

            var exifToolUrl =
                $"https://oliverbetz.de/cms/files/Artikel/ExifTool-for-Windows/exiftool-{remoteVersion}_64.zip";
            tempZipPath = Path.Combine(targetDir.FullName, $"exiftool-{Guid.NewGuid():N}_64.zip");

            using (var http = new HttpClient())
            {
                await DownloadFileAsync(http, exifToolUrl, tempZipPath, "ExifTool", progress);
            }

            progress?.Report("Extracting ExifTool...");
            await Task.Run(() => ZipFile.ExtractToDirectory(tempZipPath, targetDir.FullName, true));

            var (extractedFound, extractedExe) = ExifToolExecutableExists(targetDir);
            if (!extractedFound)
                return (false, "Download completed but exiftool executable was not found after extraction.", null);

            if (!extractedExe!.Name.Equals("exiftool.exe", StringComparison.OrdinalIgnoreCase))
            {
                var renamedPath = Path.Combine(extractedExe.Directory!.FullName, "exiftool.exe");
                if (File.Exists(renamedPath)) File.Delete(renamedPath);
                extractedExe.MoveTo(renamedPath, true);
                extractedExe = new FileInfo(renamedPath);
            }

            progress?.Report($"ExifTool ready at {extractedExe.FullName} (version {remoteVersion}).");
            return (true, "Successfully downloaded and extracted ExifTool.", extractedExe);
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Failed to download ExifTool: {ex.Message}", null);
        }
        catch (Exception ex)
        {
            return (false, $"An error occurred while setting up ExifTool: {ex.Message}", null);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempZipPath)) TryDeleteFile(tempZipPath);
        }
    }

    

    public static async Task<(bool Success, string Message, FileInfo? FfmpegExe, FileInfo? FfprobeExe)>
        FindDownloadUpdateFfmpegAndFfprobe(
            string? directory = null,
            IProgress<string>? progress = null)
    {
        progress?.Report("Checking ffmpeg configuration...");

        if (string.IsNullOrWhiteSpace(directory)) directory = DefaultFfmpegStorageDirectory().FullName;

        var targetDirectory = new DirectoryInfo(directory);
        if (!targetDirectory.Exists)
        {
            targetDirectory.Create();
            targetDirectory.Refresh();
        }

        string? tempFfmpegZipPath = null;
        string? tempFfprobeZipPath = null;

        try
        {
            progress?.Report("Fetching latest ffmpeg version information...");

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "PointlessWaymarks-Utilities");

            var apiResponse = await httpClient.GetStringAsync("https://ffbinaries.com/api/v1/version/latest");
            using var apiDocument = JsonDocument.Parse(apiResponse);
            var apiData = apiDocument.RootElement;

            if (!apiData.TryGetProperty("bin", out var binElement) ||
                !binElement.TryGetProperty("windows-64", out var windows64Element))
                return (false, "Could not parse ffmpeg download information from API.", null, null);

            var ffmpegUrl = windows64Element.TryGetProperty("ffmpeg", out var ffmpegElement)
                ? ffmpegElement.GetString()
                : null;
            var ffprobeUrl = windows64Element.TryGetProperty("ffprobe", out var ffprobeElement)
                ? ffprobeElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(ffmpegUrl) || string.IsNullOrWhiteSpace(ffprobeUrl))
                return (false, "Could not retrieve ffmpeg download URLs from API response.", null, null);

            var remoteVersion = apiData.TryGetProperty("version", out var versionElement)
                ? versionElement.GetString() ?? "unknown"
                : "unknown";

            progress?.Report($"Latest ffmpeg version: {remoteVersion}");

            var (existingFfmpegFound, existingFfmpegExe) = FfmpegExecutableExists(targetDirectory);
            var (existingFfprobeFound, existingFfprobeExe) = FfprobeExecutableExists(targetDirectory);

            if (existingFfmpegFound && existingFfprobeFound)
            {
                var localVersion = await GetFfmpegVersion(existingFfmpegExe);

                if (!string.IsNullOrWhiteSpace(localVersion) &&
                    string.Equals(localVersion, remoteVersion, StringComparison.OrdinalIgnoreCase))
                {
                    progress?.Report(
                        $"ffmpeg {localVersion} already available at {existingFfmpegExe!.FullName}.");
                    return (true, "ffmpeg and ffprobe already up to date.", existingFfmpegExe,
                        existingFfprobeExe);
                }

                progress?.Report(
                    $"Updating ffmpeg from {localVersion ?? "unknown"} to {remoteVersion}...");
            }
            else
            {
                progress?.Report($"Downloading ffmpeg {remoteVersion}...");
            }

            tempFfmpegZipPath = Path.Combine(targetDirectory.FullName, $"ffmpeg-{Guid.NewGuid():N}.zip");
            tempFfprobeZipPath = Path.Combine(targetDirectory.FullName, $"ffprobe-{Guid.NewGuid():N}.zip");

            await DownloadFileAsync(httpClient, ffmpegUrl, tempFfmpegZipPath, "ffmpeg", progress);
            await DownloadFileAsync(httpClient, ffprobeUrl, tempFfprobeZipPath, "ffprobe", progress);

            progress?.Report("Extracting ffmpeg archive...");
            await Task.Run(() => ZipFile.ExtractToDirectory(tempFfmpegZipPath, targetDirectory.FullName, true));

            progress?.Report("Extracting ffprobe archive...");
            await Task.Run(() => ZipFile.ExtractToDirectory(tempFfprobeZipPath, targetDirectory.FullName, true));

            var (ffmpegFound, ffmpegExe) = FfmpegExecutableExists(targetDirectory);
            var (ffprobeFound, ffprobeExe) = FfprobeExecutableExists(targetDirectory);

            if (!ffmpegFound || !ffprobeFound)
                return (false,
                    "Download completed but ffmpeg.exe or ffprobe.exe was not found after extraction.",
                    null, null);

            progress?.Report(
                $"ffmpeg ready at {ffmpegExe!.FullName} (version {remoteVersion}).");
            return (true, $"Successfully downloaded and installed ffmpeg {remoteVersion}.", ffmpegExe,
                ffprobeExe);
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Failed to download ffmpeg: {ex.Message}", null, null);
        }
        catch (Exception ex)
        {
            return (false, $"An error occurred while setting up ffmpeg: {ex.Message}", null, null);
        }
        finally
        {
            TryDeleteFile(tempFfmpegZipPath);
            TryDeleteFile(tempFfprobeZipPath);
        }
    }

    private static async Task DownloadFileAsync(HttpClient httpClient, string url, string destinationPath,
        string label, IProgress<string>? progress)
    {
        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        await using var downloadStream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = File.Create(destinationPath);

        var buffer = new byte[8192];
        long totalRead = 0;
        int bytesRead;
        var lastReportedTenth = -1;

        while ((bytesRead = await downloadStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead);
            totalRead += bytesRead;

            if (totalBytes <= 0) continue;

            var currentTenth = (int)(totalRead * 10 / totalBytes);
            if (currentTenth > lastReportedTenth)
            {
                lastReportedTenth = currentTenth;
                progress?.Report($"Downloading {label}... {currentTenth * 10}%");
            }
        }

        progress?.Report($"Downloading {label}... Done");
    }

    private static (bool exists, FileInfo? executable) ExifToolExecutableExists(DirectoryInfo targetDir)
    {
        var file = targetDir.GetFiles("exiftool*.exe", SearchOption.AllDirectories)
            .OrderByDescending(f => f.Name.Equals("exiftool.exe", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
        return file is { Exists: true } ? (true, file) : (false, null);
    }

    private static async Task<string?> GetExifToolVersion(FileInfo? executable)
    {
        if (executable is not { Exists: true }) return null;

        try
        {
            var psi = new ProcessStartInfo(executable.FullName, "-ver")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);

            var version = output.Trim();
            return string.IsNullOrWhiteSpace(version) ? null : version;
        }
        catch
        {
            return null;
        }
    }

    public static async Task<string?> GetFfmpegVersion(FileInfo? executable)
    {
        if (executable is not { Exists: true }) return null;

        try
        {
            var psi = new ProcessStartInfo(executable.FullName, "-version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);

            // First line is typically "ffmpeg version N.N.N ..." — extract the version token.
            var firstLine = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            if (string.IsNullOrWhiteSpace(firstLine)) return null;

            var parts = firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // Expected: "ffmpeg version 6.1 Copyright ..."
            var versionIndex = Array.IndexOf(parts, "version");
            var version = versionIndex >= 0 && versionIndex + 1 < parts.Length
                ? parts[versionIndex + 1]
                : null;

            return string.IsNullOrWhiteSpace(version) ? null : version;
        }
        catch
        {
            return null;
        }
    }

    public static DirectoryInfo TempStorageAppLocalHtmlDirectory()
    {
        var directory = new DirectoryInfo(Path.Combine(DefaultStorageDirectory().FullName, "AppLocalHtml"));

        if (!directory.Exists) directory.Create();

        directory.Refresh();

        return directory;
    }

    public static DirectoryInfo TempStorageDirectory()
    {
        var directory = new DirectoryInfo(Path.Combine(
            DefaultStorageDirectory().FullName,
            "TemporaryFiles"));

        if (!directory.Exists) directory.Create();

        directory.Refresh();

        return directory;
    }

    public static DirectoryInfo TempStorageWebViewVirtualDomainDirectory()
    {
        var directory = new DirectoryInfo(Path.Combine(DefaultStorageDirectory().FullName, "WebViewVirtualHtml"));

        if (!directory.Exists) directory.Create();

        directory.Refresh();

        return directory;
    }

    public static void TryDeleteFile(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Ignore cleanup errors.
        }
    }
}