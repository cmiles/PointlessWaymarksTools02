using System.Diagnostics;
using PointlessWaymarks.CommonTools;

namespace PointlessWaymarks.SpatialTools;

/// <summary>
///     Drop-in client helper for external .NET C# desktop applications to launch PwTrackTrimmer.Photino
///     in modal dialog mode using the Process + File contract.
/// </summary>
public static class TrackTrimmerLauncher
{
    /// <summary>
    ///     Launches PwTrackTrimmer.Photino with the specified input track, waits for the user to confirm or cancel,
    ///     and returns the result with the path to the trimmed/edited output track file.
    /// </summary>
    /// <param name="inputFilePath">Path to the track file to edit (.fit, .tcx, or .gpx)</param>
    /// <param name="outputFilePath">Optional destination path. If null, a temp file with the same extension will be created.</param>
    public static async Task<DialogResult> EditTrackModalAsync(
        string inputFilePath,
        string? outputFilePath = null)
    {
        var executable = FindTrackTrimmerExecutable();
        if (executable == null || !executable.Exists)
        {
            var primaryPath = Path.Combine(AppContext.BaseDirectory, "PwTrackTrimmerWin", "PwTrackTrimmer.Photino.exe");
            var fallbackPath = Path.Combine(@"M:\PointlessWaymarksPublications\PwTrackTrimmerWin",
                "PwTrackTrimmer.Photino.exe");
            throw new FileNotFoundException(
                $"PwTrackTrimmer.Photino executable not found in deployment folder ({primaryPath}) or fallback ({fallbackPath}).");
        }

        if (!File.Exists(inputFilePath))
            throw new FileNotFoundException($"Input track file not found at: {inputFilePath}");

        if (string.IsNullOrEmpty(outputFilePath))
        {
            var ext = Path.GetExtension(inputFilePath);
            if (string.IsNullOrEmpty(ext)) ext = ".gpx";
            outputFilePath = Path.Combine(FileLocationTools.TempStorageDirectorySubdirectory().FullName,
                $"edited_track_{Guid.NewGuid():N}{ext}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executable.FullName,
            Arguments = $"--input \"{inputFilePath}\" --output \"{outputFilePath}\" --dialog",
            UseShellExecute = false,
            CreateNoWindow = false
        };

        using var process = Process.Start(startInfo);
        if (process == null) return new DialogResult { Confirmed = false, ExitCode = -1, OutputFilePath = null };

        await process.WaitForExitAsync();

        var confirmed = process.ExitCode == 0 && File.Exists(outputFilePath);
        return new DialogResult
        {
            Confirmed = confirmed,
            ExitCode = process.ExitCode,
            OutputFilePath = confirmed ? outputFilePath : null
        };
    }

    /// <summary>
    ///     Finds the PwTrackTrimmer.Photino executable, checking first in the deployment directory under
    ///     PwTrackTrimmerWin
    /// </summary>
    /// <returns>FileInfo for the executable if found; otherwise, null.</returns>
    public static FileInfo? FindTrackTrimmerExecutable()
    {
        var primaryPath = Path.Combine(AppContext.BaseDirectory, "PwTrackTrimmerWin", "PwTrackTrimmer.Photino.exe");
        if (File.Exists(primaryPath)) return new FileInfo(primaryPath);

        if (!string.IsNullOrWhiteSpace(AppDomain.CurrentDomain.BaseDirectory) &&
            !string.Equals(AppDomain.CurrentDomain.BaseDirectory, AppContext.BaseDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            var appDomainPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PwTrackTrimmerWin",
                "PwTrackTrimmer.Photino.exe");
            if (File.Exists(appDomainPath)) return new FileInfo(appDomainPath);
        }

        var fallbackPath = Path.Combine(@"M:\PointlessWaymarksPublications\PwTrackTrimmerWin",
            "PwTrackTrimmer.Photino.exe");
        if (File.Exists(fallbackPath)) return new FileInfo(fallbackPath);

        return null;
    }

    public class DialogResult
    {
        public bool Confirmed { get; set; }
        public int ExitCode { get; set; }
        public string? OutputFilePath { get; set; }
    }
}