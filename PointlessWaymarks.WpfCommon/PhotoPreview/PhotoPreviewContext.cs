using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Messaging;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Xmp;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.SpatialTools;
using PointlessWaymarks.WpfCommon.AppMessages;
using PointlessWaymarks.WpfCommon.StarRating;
using PointlessWaymarks.WpfCommon.Status;
using XmpCore;

namespace PointlessWaymarks.WpfCommon.PhotoPreview;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class PhotoPreviewContext
{
    private const int MaxCacheSize = 15;
    private readonly Lock _ctsLock = new();

    private readonly ConcurrentDictionary<string, PreviewCacheEntry> _previewCache =
        new(StringComparer.OrdinalIgnoreCase);

    private string? _lastTempFile;
    private CancellationTokenSource? _prefetchCts;
    private CancellationTokenSource? _previewCts;
    private bool _settingRatingInternally;
    public string CurrentFilePath { get; set; } = string.Empty;
    public string DisplayTitle { get; set; } = "Photo Preview";
    public BitmapSource? EdgeOverlayImage { get; set; }
    public bool FilterUnratedOnly { get; set; }
    public BitmapSource? HistogramImage { get; set; }
    public bool IsLoading { get; set; }
    public bool LockZoom { get; set; }
    public string MetadataOverlayText { get; set; } = string.Empty;
    public BitmapSource? PreviewImage { get; set; }
    public StarRatingContext RatingEntry { get; set; } = StarRatingContext.CreateInstance();
    public bool ShowEdgeOverlay { get; set; }
    public bool ShowHistogram { get; set; } = true;
    public bool ShowMetadataOverlay { get; set; }
    public required StatusControlContext StatusContext { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public bool WriteRatingToFile { get; set; } = true;
    public double ZoomLevel { get; set; } = 1.0;

    /// <summary>
    ///     Cleans up an aperture string to a consistent ƒ/X.X format.
    ///     Ported from PhotoGenerator.ApertureCleanup.
    /// </summary>
    private static string ApertureCleanup(string? aperture)
    {
        if (string.IsNullOrWhiteSpace(aperture))
            return string.Empty;

        var apertureForCleaning = aperture.Trim();
        if (apertureForCleaning.StartsWith("f/", StringComparison.OrdinalIgnoreCase) ||
            apertureForCleaning.StartsWith("ƒ/", StringComparison.OrdinalIgnoreCase))
            apertureForCleaning = apertureForCleaning.Substring(2);
        else if (apertureForCleaning.StartsWith("f", StringComparison.OrdinalIgnoreCase) ||
                 apertureForCleaning.StartsWith("ƒ", StringComparison.OrdinalIgnoreCase))
            apertureForCleaning = apertureForCleaning.Substring(1);

        if (decimal.TryParse(apertureForCleaning, out var apertureValue))
        {
            var cultureSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            var apertureStringDecimal = apertureValue.ToString(CultureInfo.CurrentCulture);
            apertureStringDecimal = apertureStringDecimal.Contains(cultureSeparator)
                ? apertureStringDecimal.TrimEnd('0').TrimEnd(cultureSeparator.ToCharArray())
                : apertureStringDecimal;

            return $"ƒ/{apertureStringDecimal}";
        }

        return aperture.Trim();
    }

    private static BitmapSource ApplyRotation(BitmapSource source, Rotation rotation)
    {
        if (rotation == Rotation.Rotate0) return source;

        var angle = rotation switch
        {
            Rotation.Rotate90 => 90.0,
            Rotation.Rotate180 => 180.0,
            Rotation.Rotate270 => 270.0,
            _ => 0.0
        };

        var transformed = new TransformedBitmap(source, new RotateTransform(angle));
        transformed.Freeze();
        return transformed;
    }

    private static string BuildCameraString(string? make, string? model)
    {
        if (string.IsNullOrWhiteSpace(make) && string.IsNullOrWhiteSpace(model))
            return string.Empty;

        if (string.IsNullOrWhiteSpace(make))
            return model!;

        if (string.IsNullOrWhiteSpace(model))
            return make;

        // If model already contains the make, just use model
        if (model.StartsWith(make, StringComparison.OrdinalIgnoreCase))
            return model;

        return $"{make} {model}";
    }

    public void Cleanup()
    {
        lock (_ctsLock)
        {
            _previewCts?.Cancel();
            _previewCts?.Dispose();
            _previewCts = null;

            _prefetchCts?.Cancel();
            _prefetchCts?.Dispose();
            _prefetchCts = null;
        }

        _previewCache.Clear();

        WeakReferenceMessenger.Default.Unregister<PhotoPreviewRequestMessage>(this);
        WeakReferenceMessenger.Default.Unregister<PhotoItemRatingChangedMessage>(this);
        WeakReferenceMessenger.Default.Unregister<PhotoPreviewClearMessage>(this);
        CleanupTempFile();
    }

    private void CleanupTempFile()
    {
        if (string.IsNullOrWhiteSpace(_lastTempFile)) return;

        var path = _lastTempFile;
        _lastTempFile = null;

        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // File may be locked; ignore cleanup errors
        }
    }

    public static async Task<PhotoPreviewContext> CreateInstance(StatusControlContext? statusContext,
        bool writeRatingToFile = true)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var context = new PhotoPreviewContext
        {
            StatusContext = statusContext ?? await StatusControlContext.CreateInstance(),
            WriteRatingToFile = writeRatingToFile
        };

        context.BuildCommands();

        WeakReferenceMessenger.Default.Register<PhotoPreviewRequestMessage>(context,
            (r, m) => ((PhotoPreviewContext)r).OnPreviewRequested(m.Value));

        WeakReferenceMessenger.Default.Register<PhotoItemRatingChangedMessage>(context,
            (r, m) => ((PhotoPreviewContext)r).OnRatingChanged(m.Value));

        WeakReferenceMessenger.Default.Register<PhotoPreviewClearMessage>(context,
            (r, _) => ((PhotoPreviewContext)r).OnPreviewCleared());

        context.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FilterUnratedOnly))
                WeakReferenceMessenger.Default.Send(
                    new PhotoPreviewFilterUnratedMessage(context.FilterUnratedOnly));
        };

        context.RatingEntry.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StarRatingContext.UserValue) && !context._settingRatingInternally)
                context.StatusContext.RunFireAndForgetNonBlockingTask(() =>
                    context.SetRatingInternal(context.RatingEntry.UserValue));
        };

        return context;
    }

    /// <summary>
    ///     Copies pixel data from a BitmapSource into a new standalone BitmapSource
    ///     that has no reference to a BitmapDecoder. This prevents thread affinity
    ///     issues when the bitmap is later wrapped by FormatConvertedBitmap or
    ///     TransformedBitmap on a different thread and Freeze() is called, which
    ///     would otherwise walk the dependency chain to the decoder and fail with
    ///     "The calling thread cannot access this object".
    /// </summary>
    private static BitmapSource DetachFromDecoder(BitmapSource source)
    {
        var width = source.PixelWidth;
        var height = source.PixelHeight;
        var format = source.Format;
        var stride = (width * format.BitsPerPixel + 7) / 8;
        var pixels = new byte[stride * height];
        source.CopyPixels(pixels, stride, 0);
        var result = BitmapSource.Create(width, height, source.DpiX, source.DpiY,
            format, source.Palette, pixels, stride);
        result.Freeze();
        return result;
    }

    private void EvictLruEntries()
    {
        while (_previewCache.Count > MaxCacheSize)
        {
            var oldest = _previewCache
                .OrderBy(x => x.Value.LastAccessedTicks)
                .First();
            _previewCache.TryRemove(oldest.Key, out _);
        }
    }

    /// <summary>
    ///     Converts an exposure time to a human-readable string.
    ///     Ported from ExifHelpers.ExposureTimeToHumanReadableString.
    /// </summary>
    private static string ExposureTimeToHumanReadableString(Rational? toProcess)
    {
        if (toProcess == null) return string.Empty;

        var roughValue = (double)toProcess.Value.Numerator / toProcess.Value.Denominator;

        if (roughValue >= 1) return $"{roughValue:N1}s";

        return $"1/{Math.Round((double)toProcess.Value.Denominator / toProcess.Value.Numerator, 1):N0}s";
    }

    /// <summary>
    ///     Extracts camera metadata (camera, lens, ISO, aperture, shutter speed) from a file.
    ///     Uses comprehensive extraction with XMP fallbacks, modeled after PhotoGenerator.PhotoMetadataFromFile.
    /// </summary>
    private static string ExtractCameraMetadata(string fullFilePath)
    {
        try
        {
            var exifIfdDirectory = ImageMetadataReader.ReadMetadata(fullFilePath).OfType<ExifIfd0Directory>().ToList();
            var exifSubIfdDirectory = ImageMetadataReader.ReadMetadata(fullFilePath).OfType<ExifSubIfdDirectory>()
                .ToList();
            var xmpDirectory = ImageMetadataReader.ReadMetadata(fullFilePath).OfType<XmpDirectory>().ToList();

            // Camera make and model
            var cameraMake = exifIfdDirectory.GetDescription(ExifDirectoryBase.TagMake)?.Trim() ??
                             exifSubIfdDirectory.GetDescription(ExifDirectoryBase.TagMake)?.Trim();
            var cameraModel = exifIfdDirectory.GetDescription(ExifDirectoryBase.TagModel)?.Trim() ??
                              exifSubIfdDirectory.GetDescription(ExifDirectoryBase.TagModel)?.Trim();

            // Lens with XMP fallbacks
            var lens = exifSubIfdDirectory.GetDescription(ExifDirectoryBase.TagLensModel)?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(lens) || lens == "----")
                lens = xmpDirectory.GetProperty(XmpConstants.NsExifAux, "Lens")?.Value ?? string.Empty;

            if (string.IsNullOrWhiteSpace(lens) || lens == "----")
            {
                lens = xmpDirectory.GetProperty(XmpConstants.NsCameraraw, "LensProfileName")?.Value ??
                       string.Empty;

                if (lens.StartsWith("Adobe ("))
                {
                    lens = lens[7..];
                    if (lens.EndsWith(")"))
                        lens = lens[..^1];
                }
            }

            if (lens == "----") lens = string.Empty;

            // ISO
            string? iso = null;
            var isoString = exifSubIfdDirectory.GetDescription(ExifDirectoryBase.TagIsoEquivalent);
            if (!string.IsNullOrWhiteSpace(isoString) && int.TryParse(isoString, out var isoValue))
                iso = $"ISO {isoValue}";

            // Focal length
            var focalLength = exifSubIfdDirectory.GetDescription(ExifDirectoryBase.TagFocalLength)?.Trim() ??
                              string.Empty;

            // Aperture with cleanup
            var aperture = exifSubIfdDirectory.GetDescription(ExifDirectoryBase.TagAperture)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(aperture))
                aperture = exifSubIfdDirectory.GetDescription(ExifDirectoryBase.TagFNumber)?.Trim() ?? string.Empty;
            aperture = ApertureCleanup(aperture);

            // Shutter speed with proper formatting
            var shutterSpeed = string.Empty;
            if (exifSubIfdDirectory.TryGetRational(ExifDirectoryBase.TagShutterSpeed, out var shutterValue))
                shutterSpeed = ShutterSpeedToHumanReadableString(shutterValue);
            else if (exifSubIfdDirectory.TryGetRational(ExifDirectoryBase.TagExposureTime, out var exposureValue))
                shutterSpeed = ExposureTimeToHumanReadableString(exposureValue);

            // Build display string
            var sb = new StringBuilder();

            // Camera line: combine make and model, avoiding redundancy
            var camera = BuildCameraString(cameraMake, cameraModel);
            if (!string.IsNullOrWhiteSpace(camera))
                sb.AppendLine(camera);

            if (!string.IsNullOrWhiteSpace(lens))
                sb.AppendLine(lens);

            // Settings line: focal, aperture, shutter, ISO
            var settings = new List<string>();
            if (!string.IsNullOrWhiteSpace(focalLength)) settings.Add(focalLength);
            if (!string.IsNullOrWhiteSpace(aperture)) settings.Add(aperture);
            if (!string.IsNullOrWhiteSpace(shutterSpeed)) settings.Add(shutterSpeed);
            if (!string.IsNullOrWhiteSpace(iso)) settings.Add(iso);

            if (settings.Count > 0)
                sb.AppendLine(string.Join("  ", settings));

            return sb.ToString().TrimEnd();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task<PreviewCacheEntry?> GenerateCacheEntry(string fullFilePath,
        CancellationToken cancellationToken)
    {
        var file = new FileInfo(fullFilePath);
        if (!file.Exists) return null;

        var bytes = await Task.Run(() => File.ReadAllBytes(file.FullName), cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var exifRotation = GetExifRotation(bytes);
        BitmapSource? source = null;

        try
        {
            source = await Task.Run(() =>
            {
                using var ms = new MemoryStream(bytes);
                var decoder = BitmapDecoder.Create(ms,
                    BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count > 0)
                {
                    var frame = decoder.Frames[0];
                    var rotated = ApplyRotation(frame, exifRotation);
                    if (!rotated.IsFrozen) rotated.Freeze();
                    return rotated;
                }

                return null;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // WIC decode failed
        }

        if (source == null) return null;

        source = DetachFromDecoder(source);

        cancellationToken.ThrowIfCancellationRequested();

        var histogramImage = await Task.Run(() => GenerateHistogram(source, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        var edgeOverlay = await Task.Run(() => GenerateEdgeOverlay(source, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        var metadataText = ExtractCameraMetadata(fullFilePath);

        var fileSizeMb = file.Length / 1024.0 / 1024.0;
        var displayTitle =
            $"{Path.GetFileName(fullFilePath)} — {source.PixelWidth}×{source.PixelHeight} — {fileSizeMb:N1} MB";

        return new PreviewCacheEntry
        {
            PreviewImage = source,
            HistogramImage = histogramImage,
            EdgeOverlayImage = edgeOverlay,
            DisplayTitle = displayTitle,
            FullFilePath = fullFilePath,
            MetadataOverlayText = metadataText
        };
    }

    /// <summary>
    ///     Generates a semi-transparent overlay image that highlights high-contrast edges
    ///     using a Laplacian convolution kernel. Bold edges appear as bright lines on a
    ///     transparent background, helping identify in-focus areas.
    /// </summary>
    private static BitmapSource? GenerateEdgeOverlay(BitmapSource source, CancellationToken cancellationToken)
    {
        try
        {
            // Convert to grayscale byte array
            var width = source.PixelWidth;
            var height = source.PixelHeight;

            // Work on a scaled-down version for performance if the image is very large,
            // then scale the result back up
            var maxDimension = Math.Max(width, height);
            var scale = maxDimension > 4000 ? 4000.0 / maxDimension : 1.0;
            var workWidth = (int)(width * scale);
            var workHeight = (int)(height * scale);

            BitmapSource workSource;
            if (scale < 1.0)
            {
                var scaled = new TransformedBitmap(source, new ScaleTransform(scale, scale));
                scaled.Freeze();
                workSource = scaled;
            }
            else
            {
                workSource = source;
            }

            // Convert to Bgra32
            var bgra = new FormatConvertedBitmap(workSource, PixelFormats.Bgra32, null, 0);
            bgra.Freeze();

            var stride = workWidth * 4;
            var pixels = new byte[stride * workHeight];
            bgra.CopyPixels(pixels, stride, 0);

            cancellationToken.ThrowIfCancellationRequested();

            // Convert to grayscale
            var gray = new byte[workWidth * workHeight];
            for (var i = 0; i < gray.Length; i++)
            {
                var offset = i * 4;
                // Luminance: 0.299R + 0.587G + 0.114B
                gray[i] = (byte)(0.114 * pixels[offset] + 0.587 * pixels[offset + 1] + 0.299 * pixels[offset + 2]);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Apply Laplacian kernel for edge detection:
            //  0 -1  0
            // -1  4 -1
            //  0 -1  0
            var edges = new byte[workWidth * workHeight];
            for (var y = 1; y < workHeight - 1; y++)
            {
                if (y % 500 == 0) cancellationToken.ThrowIfCancellationRequested();

                for (var x = 1; x < workWidth - 1; x++)
                {
                    var laplacian =
                        4 * gray[y * workWidth + x]
                        - gray[(y - 1) * workWidth + x]
                        - gray[(y + 1) * workWidth + x]
                        - gray[y * workWidth + (x - 1)]
                        - gray[y * workWidth + x + 1];

                    edges[y * workWidth + x] = (byte)Math.Clamp(Math.Abs(laplacian), 0, 255);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Threshold and bolden: any edge value above the threshold is drawn as a
            // bright cyan line on transparent background (BGRA)
            const byte threshold = 20;
            var outputPixels = new byte[stride * workHeight];

            for (var i = 0; i < edges.Length; i++)
                if (edges[i] > threshold)
                {
                    // Scale intensity: map threshold...255 to 128...255 for alpha
                    var intensity = (byte)Math.Clamp((edges[i] - threshold) * 255 / (255 - threshold), 128, 255);
                    var offset = i * 4;
                    outputPixels[offset] = 255; // B - cyan
                    outputPixels[offset + 1] = 255; // G
                    outputPixels[offset + 2] = 0; // R
                    outputPixels[offset + 3] = intensity; // A
                }

            // else leave as transparent (all zeros)
            cancellationToken.ThrowIfCancellationRequested();

            var result = BitmapSource.Create(workWidth, workHeight, 96, 96,
                PixelFormats.Bgra32, null, outputPixels, stride);
            result.Freeze();

            // Scale back to original size if we downscaled
            if (scale < 1.0)
            {
                var scaleBack = new TransformedBitmap(result,
                    new ScaleTransform(1.0 / scale, 1.0 / scale));
                scaleBack.Freeze();
                return scaleBack;
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     Generates a histogram image showing R, G, B, and Luminance channel distributions
    ///     with color-mixed clipping indicators for shadow and highlight extremes.
    /// </summary>
    private static BitmapSource? GenerateHistogram(BitmapSource source, CancellationToken cancellationToken)
    {
        try
        {
            const int plotWidth = 256;
            const int plotHeight = 36;
            const int pad = 2;
            const int clipZoneHeight = 8;
            const int totalWidth = pad + plotWidth + pad;
            const int totalHeight = pad + plotHeight + clipZoneHeight + pad;

            // Downsample large images for speed
            var maxDim = Math.Max(source.PixelWidth, source.PixelHeight);
            var sampleScale = maxDim > 2000 ? 2000.0 / maxDim : 1.0;

            var workSource = source;
            if (sampleScale < 1.0)
            {
                var scaled = new TransformedBitmap(source, new ScaleTransform(sampleScale, sampleScale));
                scaled.Freeze();
                workSource = scaled;
            }

            var bgra = new FormatConvertedBitmap(workSource, PixelFormats.Bgra32, null, 0);
            bgra.Freeze();

            var w = bgra.PixelWidth;
            var h = bgra.PixelHeight;
            var srcStride = w * 4;
            var pixels = new byte[srcStride * h];
            bgra.CopyPixels(pixels, srcStride, 0);

            cancellationToken.ThrowIfCancellationRequested();

            var histR = new int[256];
            var histG = new int[256];
            var histB = new int[256];
            var histL = new int[256];
            var totalPixels = 0L;

            for (var i = 0; i < pixels.Length; i += 4)
            {
                var pb = pixels[i];
                var pg = pixels[i + 1];
                var pr = pixels[i + 2];

                histB[pb]++;
                histG[pg]++;
                histR[pr]++;

                var lum = (byte)(0.299 * pr + 0.587 * pg + 0.114 * pb);
                histL[lum]++;
                totalPixels++;
            }

            if (totalPixels == 0) return null;

            cancellationToken.ThrowIfCancellationRequested();

            // Scale to the tallest interior bin (excluding 0 and 255) so that extreme
            // spikes at pure black/white don't compress the rest of the histogram.
            var maxCount = 1;
            for (var i = 1; i < 255; i++)
            {
                maxCount = Math.Max(maxCount, histR[i]);
                maxCount = Math.Max(maxCount, histG[i]);
                maxCount = Math.Max(maxCount, histB[i]);
                maxCount = Math.Max(maxCount, histL[i]);
            }

            var outStride = totalWidth * 4;
            var buf = new byte[outStride * totalHeight];

            // Transparent background (buffer is already zeroed)

            // Subtle vertical grid lines at values 64, 128, 192
            for (var gBin = 64; gBin < 256; gBin += 64)
                HistogramFillRect(buf, outStride, pad + gBin, pad, 1, plotHeight, 38, 38, 38, 100);

            // Subtle horizontal grid lines at 25%, 50%, 75% of plot height
            foreach (var frac in new[] { 0.25, 0.5, 0.75 })
            {
                var gy = pad + (int)(plotHeight * (1.0 - frac));
                HistogramFillRect(buf, outStride, pad, gy, plotWidth, 1, 38, 38, 38, 100);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Draw channel histograms back-to-front: Luminance, Blue, Green, Red
            HistogramBlendChannel(buf, outStride, histL, maxCount, pad, pad, plotHeight, 170, 170, 170, 45);
            HistogramBlendChannel(buf, outStride, histB, maxCount, pad, pad, plotHeight, 220, 70, 50, 85);
            HistogramBlendChannel(buf, outStride, histG, maxCount, pad, pad, plotHeight, 50, 200, 50, 85);
            HistogramBlendChannel(buf, outStride, histR, maxCount, pad, pad, plotHeight, 50, 50, 230, 85);

            cancellationToken.ThrowIfCancellationRequested();

            // Clipping indicators — color-mixed squares that combine the colors of all
            // channels exceeding the clip threshold into a single indicator per edge.
            // If only red clips you see red; if red + blue clip you see magenta; all three → white.
            var clipThreshold = totalPixels * 0.005;
            var clipTop = pad + plotHeight + 2;
            var indicatorSize = clipZoneHeight - 4;

            // Shadow clipping (bottom-left, bin 0)
            byte sR = 0, sG = 0, sB = 0;
            if (histR[0] > clipThreshold) sR = 220;
            if (histG[0] > clipThreshold) sG = 220;
            if (histB[0] > clipThreshold) sB = 220;
            if (sR > 0 || sG > 0 || sB > 0)
                HistogramFillRect(buf, outStride, pad, clipTop, indicatorSize, indicatorSize, sB, sG, sR, 255);

            // Highlight clipping (bottom-right, bin 255)
            byte hR = 0, hG = 0, hB = 0;
            if (histR[255] > clipThreshold) hR = 220;
            if (histG[255] > clipThreshold) hG = 220;
            if (histB[255] > clipThreshold) hB = 220;
            if (hR > 0 || hG > 0 || hB > 0)
                HistogramFillRect(buf, outStride, pad + plotWidth - indicatorSize, clipTop,
                    indicatorSize, indicatorSize, hB, hG, hR, 255);

            var result = BitmapSource.Create(totalWidth, totalHeight, 96, 96,
                PixelFormats.Pbgra32, null, buf, outStride);
            result.Freeze();
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async Task GeneratePreview(string fullFilePath, string displayTitle, CancellationToken cancellationToken)
    {
        try
        {
            await ThreadSwitcher.ResumeBackgroundAsync();

            IsLoading = true;
            StatusMessage = $"Loading {Path.GetFileName(fullFilePath)}...";
            DisplayTitle = displayTitle;

            if (cancellationToken.IsCancellationRequested) return;

            var file = new FileInfo(fullFilePath);
            if (!file.Exists)
            {
                StatusMessage = "File not found.";
                PreviewImage = null;
                EdgeOverlayImage = null;
                HistogramImage = null;
                return;
            }

            // Read file bytes once
            var bytes = await Task.Run(() => File.ReadAllBytes(file.FullName), cancellationToken)
                .ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested) return;

            var exifRotation = GetExifRotation(bytes);

            // Try to decode the full image
            BitmapSource? source = null;

            // Strategy 1: WPF/WIC full-frame decode
            try
            {
                source = await Task.Run(() =>
                {
                    using var ms = new MemoryStream(bytes);
                    var decoder = BitmapDecoder.Create(ms,
                        BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

                    if (decoder.Frames.Count > 0)
                    {
                        var frame = decoder.Frames[0];
                        var rotated = ApplyRotation(frame, exifRotation);
                        if (!rotated.IsFrozen) rotated.Freeze();
                        return rotated;
                    }

                    return null;
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                StatusMessage = "WIC could not decode; scanning for embedded JPEG...";
            }

            if (cancellationToken.IsCancellationRequested) return;

            // Strategy 2: Scan for largest embedded JPEG
            source ??= await Task.Run(() =>
            {
                BitmapSource? best = null;
                var bestPixelCount = 0L;

                for (var i = 0; i < bytes.Length - 2; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (bytes[i] != 0xFF || bytes[i + 1] != 0xD8 || bytes[i + 2] != 0xFF)
                        continue;

                    try
                    {
                        using var ms = new MemoryStream(bytes, i, bytes.Length - i);
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.StreamSource = ms;
                        bi.EndInit();
                        bi.Freeze();

                        var pixelCount = (long)bi.PixelWidth * bi.PixelHeight;
                        if (pixelCount > bestPixelCount)
                        {
                            bestPixelCount = pixelCount;
                            best = ApplyRotation(bi, exifRotation);
                            if (!best.IsFrozen) best.Freeze();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                        // Not a valid JPEG at this offset
                    }
                }

                return best;
            }, cancellationToken).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested) return;

            if (source == null)
            {
                StatusMessage = "Could not generate a preview for this file.";
                PreviewImage = null;
                EdgeOverlayImage = null;
                HistogramImage = null;
                return;
            }

            source = DetachFromDecoder(source);

            PreviewImage = source;
            PreviewImageLoaded?.Invoke(this, EventArgs.Empty);

            // Generate histogram
            StatusMessage = "Generating histogram...";
            var histogramImage = await Task.Run(() => GenerateHistogram(source, cancellationToken),
                cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested) return;
            HistogramImage = histogramImage;

            // Generate edge overlay
            StatusMessage = "Generating sharpness overlay...";
            var edgeOverlay = await Task.Run(() => GenerateEdgeOverlay(source, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested) return;

            EdgeOverlayImage = edgeOverlay;

            // Extract camera metadata
            var metadataText = ExtractCameraMetadata(fullFilePath);
            MetadataOverlayText = metadataText;

            var fileSizeMb = file.Length / 1024.0 / 1024.0;
            DisplayTitle =
                $"{Path.GetFileName(fullFilePath)} — {source.PixelWidth}×{source.PixelHeight} — {fileSizeMb:N1} MB";
            StatusMessage = fullFilePath;

            _previewCache[fullFilePath] = new PreviewCacheEntry
            {
                PreviewImage = source,
                HistogramImage = histogramImage,
                EdgeOverlayImage = edgeOverlay,
                DisplayTitle = DisplayTitle,
                FullFilePath = fullFilePath,
                MetadataOverlayText = metadataText
            };
            EvictLruEntries();

            // Clean up old temp file
            CleanupTempFile();
        }
        catch (OperationCanceledException)
        {
            // Expected when a new request cancels the current one
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static Rotation GetExifRotation(byte[] imageBytes)
    {
        try
        {
            using var ms = new MemoryStream(imageBytes);
            var directories = ImageMetadataReader.ReadMetadata(ms);
            var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            if (ifd0 != null && ifd0.TryGetUInt16(ExifDirectoryBase.TagOrientation, out var orientation))
                return orientation switch
                {
                    3 => Rotation.Rotate180,
                    6 => Rotation.Rotate90,
                    8 => Rotation.Rotate270,
                    _ => Rotation.Rotate0
                };
        }
        catch
        {
            // If orientation can't be read, assume normal
        }

        return Rotation.Rotate0;
    }

    private static void HistogramBlendChannel(byte[] buffer, int stride, int[] histogram, int maxCount,
        int plotLeft, int plotTop, int plotHeight, byte bVal, byte gVal, byte rVal, byte alpha)
    {
        var srcA = alpha / 255.0;
        var invA = 1.0 - srcA;

        for (var bin = 0; bin < 256; bin++)
        {
            var count = histogram[bin];
            if (count <= 0) continue;

            var barHeight = (int)((double)Math.Min(count, maxCount) / maxCount * plotHeight);
            if (barHeight < 1) barHeight = 1;

            var px = plotLeft + bin;

            for (var dy = 0; dy < barHeight; dy++)
            {
                var py = plotTop + plotHeight - 1 - dy;
                var offset = py * stride + px * 4;

                buffer[offset] = (byte)(bVal * srcA + buffer[offset] * invA);
                buffer[offset + 1] = (byte)(gVal * srcA + buffer[offset + 1] * invA);
                buffer[offset + 2] = (byte)(rVal * srcA + buffer[offset + 2] * invA);
                buffer[offset + 3] = (byte)Math.Min(255, alpha + buffer[offset + 3] * invA);
            }
        }
    }

    private static void HistogramFillRect(byte[] buffer, int stride, int x0, int y0, int width, int height,
        byte b, byte g, byte r, byte a)
    {
        // Store as premultiplied alpha values for Pbgra32 format
        var af = a / 255.0;
        var pb = (byte)(b * af);
        var pg = (byte)(g * af);
        var pr = (byte)(r * af);

        for (var y = y0; y < y0 + height; y++)
        for (var x = x0; x < x0 + width; x++)
        {
            var offset = y * stride + x * 4;
            buffer[offset] = pb;
            buffer[offset + 1] = pg;
            buffer[offset + 2] = pr;
            buffer[offset + 3] = a;
        }
    }

    [NonBlockingCommand]
    public async Task NextItemMessage()
    {
        WeakReferenceMessenger.Default.Send(new PhotoPreviewNextItemMessage());
    }

    private void OnPreviewCleared()
    {
        lock (_ctsLock)
        {
            _previewCts?.Cancel();
            _previewCts?.Dispose();
            _previewCts = null;

            _prefetchCts?.Cancel();
            _prefetchCts?.Dispose();
            _prefetchCts = null;
        }

        CurrentFilePath = string.Empty;
        PreviewImage = null;
        EdgeOverlayImage = null;
        HistogramImage = null;
        MetadataOverlayText = string.Empty;
        DisplayTitle = "Photo Preview";
        StatusMessage = string.Empty;
        IsLoading = false;

        _settingRatingInternally = true;
        RatingEntry.UserValue = 0;
        _settingRatingInternally = false;

        CleanupTempFile();
    }

    private void OnPreviewRequested(PhotoPreviewRequestData data)
    {
        CurrentFilePath = data.FullFilePath;
        _settingRatingInternally = true;
        RatingEntry.UserValue = data.Rating;
        _settingRatingInternally = false;

        // Cancel any in-progress generation and start a new one
        CancellationTokenSource newCts;
        lock (_ctsLock)
        {
            _previewCts?.Cancel();
            _previewCts?.Dispose();
            _previewCts = new CancellationTokenSource();
            newCts = _previewCts;
        }

        // Check if we have a cached preview for this file
        if (_previewCache.TryGetValue(data.FullFilePath, out var cached))
        {
            // If the underlying file has been deleted/moved, remove the stale cache entry
            // and fall through to GeneratePreview which handles missing files gracefully
            if (!File.Exists(data.FullFilePath))
            {
                _previewCache.TryRemove(data.FullFilePath, out _);
            }
            else
            {
                cached.LastAccessedTicks = Environment.TickCount64;
                StatusContext.RunBlockingTask(async () =>
                {
                    await ThreadSwitcher.ResumeBackgroundAsync();
                    IsLoading = true;

                    PreviewImage = cached.PreviewImage;
                    PreviewImageLoaded?.Invoke(this, EventArgs.Empty);
                    HistogramImage = cached.HistogramImage;
                    EdgeOverlayImage = cached.EdgeOverlayImage;
                    MetadataOverlayText = cached.MetadataOverlayText;
                    DisplayTitle = cached.DisplayTitle;
                    StatusMessage = cached.FullFilePath;

                    IsLoading = false;
                    CleanupTempFile();

                    PrefetchUpcoming(data.UpcomingFilePaths);
                });
                return;
            }
        }

        StatusContext.RunBlockingTask(async () =>
        {
            await GeneratePreview(data.FullFilePath, data.DisplayTitle, newCts.Token);

            PrefetchUpcoming(data.UpcomingFilePaths);
        });
    }

    private void OnRatingChanged(PhotoItemRatingChangedData data)
    {
        if (string.Equals(data.FullFilePath, CurrentFilePath, StringComparison.OrdinalIgnoreCase))
        {
            _settingRatingInternally = true;
            RatingEntry.UserValue = data.Rating;
            _settingRatingInternally = false;
        }
    }

    private void PrefetchUpcoming(List<string>? upcomingFilePaths)
    {
        if (upcomingFilePaths == null || upcomingFilePaths.Count == 0) return;

        // Cancel any previous prefetch
        CancellationTokenSource newPrefetchCts;
        lock (_ctsLock)
        {
            _prefetchCts?.Cancel();
            _prefetchCts?.Dispose();
            _prefetchCts = new CancellationTokenSource();
            newPrefetchCts = _prefetchCts;
        }

        EvictLruEntries();

        _ = Task.Run(async () =>
        {
            var toPrefetch = upcomingFilePaths
                .Where(f => !_previewCache.ContainsKey(f) && File.Exists(f))
                .ToList();

            await Parallel.ForEachAsync(toPrefetch,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = 3,
                    CancellationToken = newPrefetchCts.Token
                },
                async (filePath, ct) =>
                {
                    try
                    {
                        var entry = await GenerateCacheEntry(filePath, ct);
                        if (entry != null)
                        {
                            _previewCache.TryAdd(filePath, entry);
                            EvictLruEntries();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected during cancellation
                    }
                    catch
                    {
                        // Prefetch failures are non-critical
                    }
                });
        }, newPrefetchCts.Token);
    }

    /// <summary>
    ///     Raised on the background thread after a new PreviewImage has been set.
    ///     The window subscribes to this to calculate fit-to-window zoom.
    /// </summary>
    public event EventHandler? PreviewImageLoaded;

    [NonBlockingCommand]
    public async Task PreviousItemMessage()
    {
        WeakReferenceMessenger.Default.Send(new PhotoPreviewPreviousItemMessage());
    }

    [BlockingCommand]
    public async Task SetRating0() => await SetRatingInternal(0);

    [BlockingCommand]
    public async Task SetRating1() => await SetRatingInternal(1);

    [BlockingCommand]
    public async Task SetRating2() => await SetRatingInternal(2);

    [BlockingCommand]
    public async Task SetRating3() => await SetRatingInternal(3);

    [BlockingCommand]
    public async Task SetRating4() => await SetRatingInternal(4);

    [BlockingCommand]
    public async Task SetRating5() => await SetRatingInternal(5);

    private async Task SetRatingInternal(int rating)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        if (string.IsNullOrWhiteSpace(CurrentFilePath)) return;
        if (rating == RatingEntry.UserValue) return;

        _settingRatingInternally = true;
        RatingEntry.UserValue = rating;
        _settingRatingInternally = false;

        if (WriteRatingToFile)
            try
            {
                await WriteRatingWithExifTool(CurrentFilePath, rating);
            }
            catch (Exception ex)
            {
                _ = StatusContext.ToastError($"Failed to write rating to file: {ex.Message}");
                return;
            }

        WeakReferenceMessenger.Default.Send(
            new PhotoItemRatingChangedMessage(new PhotoItemRatingChangedData(CurrentFilePath, rating)));

        var stars = rating > 0 ? new string('★', rating) + new string('☆', 5 - rating) : "No Rating";
        _ = StatusContext.ToastSuccess($"Rating: {stars} ({rating})");
    }

    /// <summary>
    ///     Converts a shutter speed APEX value to a human-readable string.
    ///     Ported from ExifHelpers.ShutterSpeedToHumanReadableString.
    /// </summary>
    private static string ShutterSpeedToHumanReadableString(Rational? toProcess)
    {
        if (toProcess == null) return string.Empty;

        if (toProcess.Value.Numerator < 0)
            return Math.Round(Math.Pow(2, (double)-1 * toProcess.Value.Numerator / toProcess.Value.Denominator), 1)
                .ToString("N1") + "s";

        return
            $"1/{Math.Round(Math.Pow(2, (double)toProcess.Value.Numerator / toProcess.Value.Denominator), 1):N0}s";
    }

    [NonBlockingCommand]
    public async Task ToggleFilterUnrated()
    {
        FilterUnratedOnly = !FilterUnratedOnly;
        var state = FilterUnratedOnly ? "ON" : "OFF";
        await StatusContext.ToastSuccess($"Unrated filter: {state}");
    }

    [NonBlockingCommand]
    public async Task ToggleHistogram()
    {
        ShowHistogram = !ShowHistogram;
    }

    [NonBlockingCommand]
    public async Task ToggleLockZoom()
    {
        LockZoom = !LockZoom;
        var state = LockZoom ? "ON" : "OFF";
        await StatusContext.ToastSuccess($"Lock Zoom: {state}");
    }

    [NonBlockingCommand]
    public async Task ToggleMetadataOverlay()
    {
        ShowMetadataOverlay = !ShowMetadataOverlay;
    }

    [NonBlockingCommand]
    public async Task ToggleSharpness()
    {
        ShowEdgeOverlay = !ShowEdgeOverlay;
    }

    private static async Task WriteRatingWithExifTool(string filePath, int rating)
    {
        var (success, _, exifToolExe) = await FileLocationTools.FindDownloadUpdateExifTool();
        if (!success || exifToolExe == null)
            throw new InvalidOperationException("ExifTool executable not found or could not be downloaded.");

        var request = new ExifToolWriteRequest
        {
            Rating = rating
        };

        var file = new FileInfo(filePath);
        var result = await ExifToolWriter.WriteMetadataAsync(exifToolExe, request, new[] { file });

        if (!result.Success)
            throw new InvalidOperationException(string.Join("; ", result.Errors));
    }

    private class PreviewCacheEntry
    {
        public required string DisplayTitle { get; init; }
        public BitmapSource? EdgeOverlayImage { get; init; }
        public required string FullFilePath { get; init; }
        public BitmapSource? HistogramImage { get; init; }
        public long LastAccessedTicks { get; set; } = Environment.TickCount64;
        public string MetadataOverlayText { get; init; } = string.Empty;
        public BitmapSource? PreviewImage { get; init; }
    }
}