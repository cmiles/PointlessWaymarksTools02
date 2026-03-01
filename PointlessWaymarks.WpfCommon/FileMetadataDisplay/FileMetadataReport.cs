using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Web;
using HtmlTableHelper;
using MetadataExtractor;
using MetadataExtractor.Formats.Xmp;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.WpfCommon.Status;
using XmpCore;

namespace PointlessWaymarks.WpfCommon.FileMetadataDisplay;

public static class FileMetadataReport
{
    public static async Task<string> AllFileMetadataToHtml(FileInfo selectedFile, string? ffProbeFullName)
    {
        if (selectedFile.Extension.Equals(".xmp", StringComparison.OrdinalIgnoreCase))
        {
            var xmpHtml = await XmpSidecarToHtml(selectedFile);
            return await xmpHtml.ToHtmlDocumentWithPureCss("Xmp Metadata", "body {margin: 12px;}");
        }

        var photoMetaTags = ImageMetadataReader.ReadMetadata(selectedFile.FullName);

        var tagHtml = photoMetaTags.SelectMany(x => x.Tags).OrderBy(x => x.DirectoryName).ThenBy(x => x.Name)
            .ToList().Select(x => new
            {
                DataType = x.Type.ToString(),
                x.DirectoryName,
                Tag = x.Name,
                TagValue = x.Description?.SafeObjectDump()
            }).ToHtmlTable(new { @class = "pure-table pure-table-striped" });

        var xmpDirectory = ImageMetadataReader.ReadMetadata(selectedFile.FullName).OfType<XmpDirectory>()
            .FirstOrDefault();

        var xmpMetadata = xmpDirectory?.GetXmpProperties().Select(x => new { XmpKey = x.Key, XmpValue = x.Value })
            .ToHtmlTable(new { @class = "pure-table pure-table-striped" });

        var htmlStringBuilder = new StringBuilder();

        if (photoMetaTags.SelectMany(x => x.Tags).Any())
        {
            htmlStringBuilder.AppendLine("<h3>Metadata - Part 1</h3><br>");
            htmlStringBuilder.AppendLine(tagHtml);
        }

        if (xmpDirectory != null &&
            xmpDirectory.GetXmpProperties().Select(x => new { XmpKey = x.Key, XmpValue = x.Value }).Any())
        {
            htmlStringBuilder.AppendLine("<br><br><h3>XMP - Part 2</h3><br>");
            htmlStringBuilder.AppendLine(xmpMetadata);
        }

        // Check for a sidecar .xmp file alongside the main file
        var sidecarFile = FindXmpSidecar(selectedFile);
        if (sidecarFile != null)
        {
            var sidecarHtml = await XmpSidecarToHtml(sidecarFile);
            htmlStringBuilder.AppendLine(
                $"<br><br><h3>Sidecar XMP Data — {HttpUtility.HtmlEncode(sidecarFile.Name)}</h3><br>");
            htmlStringBuilder.AppendLine(sidecarHtml);
        }

        // Check if this is a video file and ffprobe is available
        var videoExtensions = new[]
            { ".mp4", ".webm", ".ogg", ".avi", ".mov", ".mkv", ".flv", ".wmv", ".m4v", ".mpg", ".mpeg", ".3gp" };
        if (!string.IsNullOrWhiteSpace(ffProbeFullName) &&
            videoExtensions.Contains(selectedFile.Extension.ToLowerInvariant()))
        {
            var (success, ffprobeHtml) = await FfprobeMetadataToHtml(selectedFile, ffProbeFullName);

            if (success)
            {
                htmlStringBuilder.AppendLine("<br><br><h3>FFprobe Information - Part 3</h3><br>");
                htmlStringBuilder.AppendLine(ffprobeHtml);
            }
            else
            {
                htmlStringBuilder.AppendLine("<br><br><h3>FFprobe Information - Part 3</h3><br>");
                htmlStringBuilder.AppendLine(ffprobeHtml);
            }
        }

        var htmlString =
            await htmlStringBuilder.ToString().ToHtmlDocumentWithPureCss("Metadata", "body {margin: 12px;}");

        return htmlString;
    }

    public static async Task AllFileMetadataToHtmlDocumentAndOpen(FileInfo? selectedFile, string? ffProbeFullName,
        StatusControlContext statusContext)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (selectedFile == null)
        {
            await statusContext.ToastError("No photo...");
            return;
        }

        selectedFile.Refresh();

        if (!selectedFile.Exists)
        {
            await statusContext.ToastError($"File {selectedFile.FullName} doesn't exist?");
            return;
        }

        var metadataHtmlString = await AllFileMetadataToHtml(selectedFile, ffProbeFullName);

        if (string.IsNullOrWhiteSpace(metadataHtmlString))
        {
            await statusContext.ToastError($"No Metadata Found for {selectedFile.FullName}...");
            return;
        }

        var htmlString =
            await
                $"<h1>Metadata Report:</h1><h1>{HttpUtility.HtmlEncode(selectedFile.FullName)}</h1>{metadataHtmlString}"
                    .ToHtmlDocumentWithPureCss("Photo Metadata", "body {margin: 12px;}");

        await ThreadSwitcher.ResumeForegroundAsync();

        var file = new FileInfo(Path.Combine(FileLocationTools.TempStorageDirectory().FullName,
            $"PhotoMetadata-{Path.GetFileNameWithoutExtension(selectedFile.Name)}-{DateTime.Now:yyyy-MM-dd---HH-mm-ss}.htm"));

        await File.WriteAllTextAsync(file.FullName, htmlString);

        var ps = new ProcessStartInfo(file.FullName) { UseShellExecute = true, Verb = "open" };
        Process.Start(ps);
    }

    public static async Task<(bool success, string html)> FfprobeMetadataToHtml(FileInfo selectedFile,
        string? ffProbeFullName)
    {
        if (string.IsNullOrWhiteSpace(ffProbeFullName))
            return (false, "<p>FFprobe path not configured</p>");

        if (!File.Exists(ffProbeFullName))
            return (false, $"<p>FFprobe not found at: {HttpUtility.HtmlEncode(ffProbeFullName)}</p>");

        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = ffProbeFullName,
                Arguments = $"-v quiet -print_format json -show_format -show_streams \"{selectedFile.FullName}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process();
            process.StartInfo = processStartInfo;
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var errorOutput = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                return (false,
                    $"<p>FFprobe failed with exit code {process.ExitCode}</p><p>Error: {HttpUtility.HtmlEncode(errorOutput)}</p>");

            if (string.IsNullOrWhiteSpace(output))
                return (false, "<p>FFprobe returned no output</p>");

            var htmlStringBuilder = new StringBuilder();

            // Parse the JSON output and convert to table
            using var document = JsonDocument.Parse(output);
            var root = document.RootElement;

            var hasData = false;

            // Process format information
            if (root.TryGetProperty("format", out var format))
            {
                var formatData = new List<dynamic>();
                foreach (var property in format.EnumerateObject())
                {
                    var value = property.Value.ToString();
                    formatData.Add(new { Property = property.Name, Value = value });
                }

                if (formatData.Any())
                {
                    hasData = true;
                    var formatTable = formatData.ToHtmlTable(new { @class = "pure-table pure-table-striped" });
                    htmlStringBuilder.AppendLine("<h3>FFprobe Format Information</h3><br>");
                    htmlStringBuilder.AppendLine(formatTable);
                }
            }

            // Process streams information
            if (root.TryGetProperty("streams", out var streams))
            {
                var streamIndex = 0;
                foreach (var stream in streams.EnumerateArray())
                {
                    var streamData = new List<dynamic>();
                    foreach (var property in stream.EnumerateObject())
                    {
                        var value = property.Value.ToString();
                        streamData.Add(new { Property = property.Name, Value = value });
                    }

                    if (streamData.Any())
                    {
                        hasData = true;
                        var streamTable = streamData.ToHtmlTable(new { @class = "pure-table pure-table-striped" });
                        htmlStringBuilder.AppendLine($"<br><br><h3>FFprobe Stream {streamIndex}</h3><br>");
                        htmlStringBuilder.AppendLine(streamTable);
                    }

                    streamIndex++;
                }
            }

            if (!hasData)
                return (false, "<p>FFprobe returned valid JSON but no format or stream data was found</p>");

            return (true, htmlStringBuilder.ToString());
        }
        catch (JsonException ex)
        {
            return (false,
                $"<p>Failed to parse FFprobe JSON output</p><p>Error: {HttpUtility.HtmlEncode(ex.Message)}</p>");
        }
        catch (Exception ex)
        {
            return (false, $"<p>FFprobe execution failed</p><p>Error: {HttpUtility.HtmlEncode(ex.Message)}</p>");
        }
    }

    public static FileInfo? FindXmpSidecar(FileInfo file)
    {
        if (file.Directory is null) return null;

        // Common sidecar naming: photo.arw.xmp or photo.xmp (same base name)
        var withExtension = new FileInfo(Path.Combine(file.Directory.FullName, file.Name + ".xmp"));
        if (withExtension.Exists) return withExtension;

        var withoutExtension = new FileInfo(Path.Combine(file.Directory.FullName,
            Path.GetFileNameWithoutExtension(file.Name) + ".xmp"));
        if (withoutExtension.Exists) return withoutExtension;

        return null;
    }

    public static async Task<string> XmpSidecarToHtml(FileInfo xmpFile)
    {
        IXmpMeta xmp;
        await using (var stream = File.OpenRead(xmpFile.FullName))
        {
            xmp = XmpMetaFactory.Parse(stream);
        }

        var properties = xmp.Properties
            .OrderBy(x => x.Namespace).ThenBy(x => x.Path)
            .Select(x => new { x.Namespace, x.Path, x.Value })
            .ToList();

        if (properties.Count == 0) return "<p>No XMP properties found.</p>";

        return properties.ToHtmlTable(new { @class = "pure-table pure-table-striped" });
    }
}