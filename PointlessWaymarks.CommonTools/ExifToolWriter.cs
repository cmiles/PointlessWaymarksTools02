using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace PointlessWaymarks.CommonTools;

/// <summary>
///     Metadata values to write via ExifTool. All properties are optional;
///     only non-null values are included in the generated arguments.
/// </summary>
public class ExifToolWriteRequest
{
    /// <summary>
    ///     Altitude in meters (will be written as GPSAltitude).
    /// </summary>
    public double? AltitudeInMeters { get; set; }
    public string? Copyright { get; set; }
    public string? Creator { get; set; }
    public string? Description { get; set; }
    public List<string>? Keywords { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Title { get; set; }
    public int? Rating { get; set; }
}

/// <summary>
///     Result of writing metadata to one or more files.
/// </summary>
public class ExifToolWriteResult
{
    public List<string> Errors { get; set; } = [];
    public int FilesProcessed { get; set; }
    public bool Success => Errors.Count == 0;
}

/// <summary>
///     A reusable wrapper around ExifTool for writing metadata to image/video files.
///     Uses a UTF-8 args file (-@) so non-ASCII characters are preserved.
/// </summary>
public static class ExifToolWriter
{
    /// <summary>
    ///     Builds the list of ExifTool arguments for the given request and file.
    ///     Each entry is a single argument line suitable for use with ExifTool's -@ argfile.
    /// </summary>
    public static List<string> BuildArguments(ExifToolWriteRequest request, FileInfo file)
    {
        var args = new List<string> { "-m", "-overwrite_original", "-charset", "iptc=utf8" };

        if (request.Title is not null)
        {
            args.Add($"-Title={request.Title}");
            args.Add($"-XMP:Title={request.Title}");
            args.Add($"-IPTC:ObjectName={request.Title}");
        }

        if (request.Description is not null)
        {
            args.Add($"-Description={request.Description}");
            args.Add($"-XMP-dc:Description={request.Description}");
            args.Add($"-IPTC:Caption-Abstract={request.Description}");
        }

        if (request.Creator is not null)
        {
            args.Add($"-Artist={request.Creator}");
            args.Add($"-XMP-dc:Creator={request.Creator}");
            args.Add($"-IPTC:By-line={request.Creator}");
        }

        if (request.Rating is >= 0 and <= 5)
        {
            args.Add($"-Rating={request.Rating}");
            args.Add($"-XMP:Rating={request.Rating}");
        }

        if (request.Copyright is not null)
        {
            args.Add($"-Copyright={request.Copyright}");
            args.Add($"-XMP-dc:Rights={request.Copyright}");
            args.Add($"-IPTC:CopyrightNotice={request.Copyright}");
        }

        if (request.Keywords is not null)
        {
            args.Add("-Keywords="); // clear existing IPTC Keywords
            args.Add("-Subject="); // clear existing XMP dc:Subject
            foreach (var keyword in request.Keywords)
            {
                args.Add($"-Keywords={keyword}");
                args.Add($"-Subject={keyword}");
            }
        }

        if (request.Latitude.HasValue)
            args.Add($"-GPSLatitude*={request.Latitude.Value.ToString(CultureInfo.InvariantCulture)}");

        if (request.Longitude.HasValue)
            args.Add($"-GPSLongitude*={request.Longitude.Value.ToString(CultureInfo.InvariantCulture)}");

        if (request.AltitudeInMeters.HasValue)
            args.Add($"-GPSAltitude*={request.AltitudeInMeters.Value.ToString(CultureInfo.InvariantCulture)}");

        args.Add(file.FullName);

        return args;
    }

    /// <summary>
    ///     Returns a human-readable command-line string showing what ExifTool would
    ///     be invoked with for the given request and file. Useful for logging/diagnostics.
    /// </summary>
    public static string GetCommandLinePreview(FileInfo exifToolExe, ExifToolWriteRequest request, FileInfo file)
    {
        var args = BuildArguments(request, file);
        return $"{exifToolExe.FullName} {string.Join(" ", args)}";
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunExifToolAsync(
        FileInfo exifToolExe, string argsFilePath)
    {
        var psi = new ProcessStartInfo(exifToolExe.FullName)
        {
            Arguments = $"-@ \"{argsFilePath}\"",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi)
                         ?? throw new InvalidOperationException("Failed to start ExifTool process.");

        var stdOut = await proc.StandardOutput.ReadToEndAsync();
        var stdErr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        return (proc.ExitCode, stdOut, stdErr);
    }

    /// <summary>
    ///     Writes the supplied metadata to every file in <paramref name="files" />.
    /// </summary>
    public static async Task<ExifToolWriteResult> WriteMetadataAsync(
        FileInfo exifToolExe,
        ExifToolWriteRequest request,
        IReadOnlyList<FileInfo> files,
        IProgress<string>? progress = null)
    {
        var result = new ExifToolWriteResult();

        if (files.Count == 0)
        {
            result.Errors.Add("No files provided.");
            return result;
        }

        foreach (var file in files)
        {
            const int maxAttempts = 3;
            var success = false;
            var lastError = string.Empty;
            var exifToolTmpPath = $"{file.FullName}_exiftool_tmp";

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                string? argsFilePath = null;
                try
                {
                    // Clean up any stale/leftover _exiftool_tmp file from previous runs or failed attempts
                    if (File.Exists(exifToolTmpPath))
                    {
                        try
                        {
                            var tmpAttr = File.GetAttributes(exifToolTmpPath);
                            if (tmpAttr.HasFlag(FileAttributes.ReadOnly))
                                File.SetAttributes(exifToolTmpPath, FileAttributes.Normal);
                            File.Delete(exifToolTmpPath);
                        }
                        catch
                        {
                            // If locked, we will see if waiting helps
                        }
                    }

                    // Ensure target file is not marked read-only
                    if (file.Exists)
                    {
                        file.Refresh();
                        if (file.IsReadOnly)
                        {
                            try
                            {
                                file.IsReadOnly = false;
                            }
                            catch
                            {
                                // Attempt to continue anyway
                            }
                        }
                    }

                    progress?.Report(attempt == 1
                        ? $"Writing metadata to {file.Name}..."
                        : $"Writing metadata to {file.Name} (attempt {attempt}/{maxAttempts})...");

                    var args = BuildArguments(request, file);

                    argsFilePath = Path.Combine(Path.GetTempPath(), $"exiftool-args-{Guid.NewGuid():N}.txt");
                    await File.WriteAllLinesAsync(argsFilePath, args, new UTF8Encoding(false));

                    progress?.Report(GetCommandLinePreview(exifToolExe, request, file));

                    var (exitCode, stdOut, stdErr) = await RunExifToolAsync(exifToolExe, argsFilePath);

                    if (exitCode != 0)
                    {
                        var errorMsg = $"ExifTool error ({exitCode}): {stdErr}\n{stdOut}".Trim();
                        throw new InvalidOperationException(errorMsg);
                    }

                    result.FilesProcessed++;
                    success = true;
                    break;
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    if (attempt < maxAttempts)
                    {
                        progress?.Report(
                            $"ExifTool write failed for {file.Name} (attempt {attempt}/{maxAttempts}): {ex.Message}. Retrying in {250 * attempt}ms...");
                        await Task.Delay(250 * attempt);
                    }
                }
                finally
                {
                    FileLocationTools.TryDeleteFile(argsFilePath);
                }
            }

            if (!success)
            {
                result.Errors.Add($"{file.Name}: {lastError}");
                // Cleanup temp file if left behind
                if (File.Exists(exifToolTmpPath))
                {
                    try
                    {
                        var tmpAttr = File.GetAttributes(exifToolTmpPath);
                        if (tmpAttr.HasFlag(FileAttributes.ReadOnly))
                            File.SetAttributes(exifToolTmpPath, FileAttributes.Normal);
                        File.Delete(exifToolTmpPath);
                    }
                    catch
                    {
                        // Ignore
                    }
                }
            }
        }

        progress?.Report(result.Success
            ? $"Metadata written successfully to {result.FilesProcessed} file(s)."
            : $"Metadata written to {result.FilesProcessed} file(s) with {result.Errors.Count} error(s).");

        return result;
    }
}