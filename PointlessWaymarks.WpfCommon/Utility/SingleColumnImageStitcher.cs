using System.IO;
using PointlessWaymarks.CommonTools;
using SkiaSharp;

namespace PointlessWaymarks.WpfCommon.Utility;

/// <summary>
///     Utilities for vertical single-column image stitching (e.g. manual scroll captures).
///     Detects static headers and footers (so header only appears on the first shot and footer on the last),
///     determines vertical overlap by image matching, and stitches slices into a single seamless image.
/// </summary>
public static class SingleColumnImageStitcher
{
    /// <summary>
    ///     Detects the height (in pixels from the top) of a static/fixed header identical across all shots.
    /// </summary>
    public static int DetectStaticHeaderHeight(IReadOnlyList<SKBitmap> images, double maxDiffThreshold = 6.0,
        double maxFractionOfHeight = 0.4)
    {
        if (images.Count < 2) return 0;

        var first = images[0];
        var width = first.Width;
        var height = first.Height;
        var maxHeaderRows = (int)Math.Min(height * maxFractionOfHeight, 400);

        // Pre-fetch pixel arrays for fast access
        var pixelArrays = new SKColor[images.Count][];
        for (var i = 0; i < images.Count; i++)
        {
            if (images[i].Width != width || images[i].Height != height) return 0;
            pixelArrays[i] = images[i].Pixels;
        }

        var headerHeight = 0;
        for (var y = 0; y < maxHeaderRows; y++)
        {
            var isIdenticalAcrossAll = true;
            var offset = y * width;

            for (var imgIndex = 1; imgIndex < images.Count; imgIndex++)
            {
                var diff = CalculateRowAverageDifference(pixelArrays[0], pixelArrays[imgIndex], offset, width);
                if (diff > maxDiffThreshold)
                {
                    isIdenticalAcrossAll = false;
                    break;
                }
            }

            if (isIdenticalAcrossAll)
                headerHeight = y + 1;
            else
                break;
        }

        return headerHeight;
    }

    /// <summary>
    ///     Detects the height (in pixels from the bottom) of a static/fixed footer identical across all shots.
    /// </summary>
    public static int DetectStaticFooterHeight(IReadOnlyList<SKBitmap> images, double maxDiffThreshold = 6.0,
        double maxFractionOfHeight = 0.4)
    {
        if (images.Count < 2) return 0;

        var first = images[0];
        var width = first.Width;
        var height = first.Height;
        var maxFooterRows = (int)Math.Min(height * maxFractionOfHeight, 400);

        var pixelArrays = new SKColor[images.Count][];
        for (var i = 0; i < images.Count; i++)
        {
            if (images[i].Width != width || images[i].Height != height) return 0;
            pixelArrays[i] = images[i].Pixels;
        }

        var footerHeight = 0;
        for (var offsetFromBottom = 0; offsetFromBottom < maxFooterRows; offsetFromBottom++)
        {
            var y = height - 1 - offsetFromBottom;
            var offset = y * width;
            var isIdenticalAcrossAll = true;

            for (var imgIndex = 1; imgIndex < images.Count; imgIndex++)
            {
                var diff = CalculateRowAverageDifference(pixelArrays[0], pixelArrays[imgIndex], offset, width);
                if (diff > maxDiffThreshold)
                {
                    isIdenticalAcrossAll = false;
                    break;
                }
            }

            if (isIdenticalAcrossAll)
                footerHeight = offsetFromBottom + 1;
            else
                break;
        }

        return footerHeight;
    }

    /// <summary>
    ///     Detects the height (in pixels beyond globalHeaderHeight) of an inner sticky header identical across
    ///     inner/scrolled shots (e.g. secondary navigation or sticky mini-profile bars that appear on scroll).
    /// </summary>
    public static int DetectInnerHeaderHeight(IReadOnlyList<SKBitmap> images, int globalHeaderHeight,
        double maxDiffThreshold = 6.0, double maxFractionOfHeight = 0.4)
    {
        if (images.Count < 3) return 0;

        var width = images[0].Width;
        var height = images[0].Height;
        var maxHeaderRows = (int)Math.Min(height * maxFractionOfHeight, 400);

        if (globalHeaderHeight >= maxHeaderRows) return 0;

        var innerPixelArrays = new List<SKColor[]>();
        var endIdx = images.Count == 3 ? 3 : images.Count - 1;
        for (var i = 1; i < endIdx; i++)
        {
            if (images[i].Width != width || images[i].Height != height) return 0;
            innerPixelArrays.Add(images[i].Pixels);
        }

        if (innerPixelArrays.Count < 2) return 0;

        var innerHeaderHeight = 0;
        for (var y = globalHeaderHeight; y < maxHeaderRows; y++)
        {
            var isIdenticalAcrossInner = true;
            var offset = y * width;

            for (var i = 1; i < innerPixelArrays.Count; i++)
            {
                var diff = CalculateRowAverageDifference(innerPixelArrays[0], innerPixelArrays[i], offset, width);
                if (diff > maxDiffThreshold)
                {
                    isIdenticalAcrossInner = false;
                    break;
                }
            }

            if (isIdenticalAcrossInner)
                innerHeaderHeight = y - globalHeaderHeight + 1;
            else
                break;
        }

        return innerHeaderHeight;
    }

    /// <summary>
    ///     Detects the height (in pixels beyond globalFooterHeight) of an inner sticky footer identical across
    ///     inner/scrolled shots (e.g. floating widgets or sticky bottom banners).
    /// </summary>
    public static int DetectInnerFooterHeight(IReadOnlyList<SKBitmap> images, int globalFooterHeight,
        double maxDiffThreshold = 6.0, double maxFractionOfHeight = 0.4)
    {
        if (images.Count < 2) return 0;

        var width = images[0].Width;
        var height = images[0].Height;
        var maxFooterRows = (int)Math.Min(height * maxFractionOfHeight, 400);

        if (globalFooterHeight >= maxFooterRows) return 0;

        if (images.Count >= 3)
        {
            var innerPixelArrays = new List<SKColor[]>();
            var endIdx = images.Count == 3 ? 3 : images.Count - 1;
            for (var i = 1; i < endIdx; i++)
            {
                if (images[i].Width != width || images[i].Height != height) return 0;
                innerPixelArrays.Add(images[i].Pixels);
            }

            if (innerPixelArrays.Count >= 2)
            {
                var innerFooterHeight = 0;
                for (var offsetFromBottom = globalFooterHeight; offsetFromBottom < maxFooterRows; offsetFromBottom++)
                {
                    var y = height - 1 - offsetFromBottom;
                    var offset = y * width;
                    var isIdenticalAcrossInner = true;

                    for (var i = 1; i < innerPixelArrays.Count; i++)
                    {
                        var diff = CalculateRowAverageDifference(innerPixelArrays[0], innerPixelArrays[i], offset, width);
                        if (diff > maxDiffThreshold)
                        {
                            isIdenticalAcrossInner = false;
                            break;
                        }
                    }

                    if (isIdenticalAcrossInner)
                        innerFooterHeight = offsetFromBottom - globalFooterHeight + 1;
                    else
                        break;
                }

                if (innerFooterHeight > 0) return innerFooterHeight;
            }
        }

        if (images.Count >= 3)
        {
            var p0 = images[0].Pixels;
            var p1 = images[1].Pixels;
            var innerFooterHeight = 0;

            for (var offsetFromBottom = globalFooterHeight; offsetFromBottom < maxFooterRows; offsetFromBottom++)
            {
                var y = height - 1 - offsetFromBottom;
                var offset = y * width;
                var diff = CalculateRowAverageDifference(p0, p1, offset, width);
                if (diff <= maxDiffThreshold)
                    innerFooterHeight = offsetFromBottom - globalFooterHeight + 1;
                else
                    break;
            }

            return innerFooterHeight;
        }

        return 0;
    }

    private static double CalculateRowAverageDifference(SKColor[] pixelsA, SKColor[] pixelsB, int rowOffset, int width,
        int stepX = 2)
    {
        long totalDiff = 0;
        var sampleCount = 0;

        for (var x = 0; x < width; x += stepX)
        {
            var cA = pixelsA[rowOffset + x];
            var cB = pixelsB[rowOffset + x];
            totalDiff += Math.Abs(cA.Red - cB.Red) + Math.Abs(cA.Green - cB.Green) + Math.Abs(cA.Blue - cB.Blue);
            sampleCount++;
        }

        return sampleCount == 0 ? 0 : (double)totalDiff / (sampleCount * 3);
    }

    /// <summary>
    ///     Finds the best downward vertical shift S of imageB relative to imageA.
    ///     yB = yA - S.
    /// </summary>
    public static int FindBestVerticalOffset(SKBitmap imageA, SKBitmap imageB, int activeTopA, int activeBottomA,
        int activeTopB, int activeBottomB, int minOverlapRows = 12)
    {
        var width = Math.Min(imageA.Width, imageB.Width);
        var heightA = imageA.Height;
        var heightB = imageB.Height;

        activeBottomA = Math.Min(activeBottomA, heightA);
        activeBottomB = Math.Min(activeBottomB, heightB);

        var pixelsA = imageA.Pixels;
        var pixelsB = imageB.Pixels;

        var maxShift = activeBottomA - activeTopB - minOverlapRows;
        if (maxShift < 0) return Math.Max(0, activeBottomA - activeTopB);

        var bestShift = 0;
        var bestScore = double.MaxValue;

        // Coarse search: step 3, col step 4, row step 2
        for (var s = 0; s <= maxShift; s += 3)
        {
            var yStartA = Math.Max(activeTopA, activeTopB + s);
            var yEndA = Math.Min(activeBottomA, activeBottomB + s);
            var overlap = yEndA - yStartA;
            if (overlap < minOverlapRows) continue;

            var score = CalculateShiftScore(pixelsA, imageA.Width, pixelsB, imageB.Width, width, s, yStartA, yEndA,
                stepY: 2, stepX: 4);

            if (score < bestScore)
            {
                bestScore = score;
                bestShift = s;
            }
        }

        // Fine search around best coarse shift: step 1, full resolution
        var fineStart = Math.Max(0, bestShift - 6);
        var fineEnd = Math.Min(maxShift, bestShift + 6);

        var fineBestShift = bestShift;
        var fineBestScore = double.MaxValue;

        for (var s = fineStart; s <= fineEnd; s++)
        {
            var yStartA = Math.Max(activeTopA, activeTopB + s);
            var yEndA = Math.Min(activeBottomA, activeBottomB + s);
            var overlap = yEndA - yStartA;
            if (overlap < minOverlapRows) continue;

            var score = CalculateShiftScore(pixelsA, imageA.Width, pixelsB, imageB.Width, width, s, yStartA, yEndA,
                stepY: 1, stepX: 1);

            if (score < fineBestScore)
            {
                fineBestScore = score;
                fineBestShift = s;
            }
        }

        // If the match error is too high (e.g. no visual overlap at all, user scrolled completely past),
        // fallback to appending imageB right after imageA's active region
        if (fineBestScore > 45.0)
        {
            return Math.Max(0, activeBottomA - activeTopB);
        }

        return fineBestShift;
    }

    private static double CalculateShiftScore(SKColor[] pixelsA, int strideA, SKColor[] pixelsB, int strideB,
        int width, int shift, int yStartA, int yEndA, int stepY, int stepX)
    {
        long totalDiff = 0;
        long sampleCount = 0;

        for (var yA = yStartA; yA < yEndA; yA += stepY)
        {
            var yB = yA - shift;
            var offsetA = yA * strideA;
            var offsetB = yB * strideB;

            for (var x = 0; x < width; x += stepX)
            {
                var cA = pixelsA[offsetA + x];
                var cB = pixelsB[offsetB + x];
                totalDiff += Math.Abs(cA.Red - cB.Red) + Math.Abs(cA.Green - cB.Green) + Math.Abs(cA.Blue - cB.Blue);
                sampleCount++;
            }
        }

        return sampleCount == 0 ? double.MaxValue : (double)totalDiff / (sampleCount * 3);
    }

    private static bool HasInnerHeader(SKColor[] pixels, SKColor[] templatePixels, int globalHeaderHeight,
        int innerHeaderHeight, int width, double maxDiffThreshold = 6.0)
    {
        if (innerHeaderHeight <= 0) return false;

        for (var y = globalHeaderHeight; y < globalHeaderHeight + innerHeaderHeight; y++)
        {
            var offset = y * width;
            var diff = CalculateRowAverageDifference(templatePixels, pixels, offset, width);
            if (diff > maxDiffThreshold) return false;
        }

        return true;
    }

    private static bool HasInnerFooter(SKColor[] pixels, SKColor[] templatePixels, int globalFooterHeight,
        int innerFooterHeight, int width, int height, double maxDiffThreshold = 6.0)
    {
        if (innerFooterHeight <= 0) return false;

        for (var offsetFromBottom = globalFooterHeight;
             offsetFromBottom < globalFooterHeight + innerFooterHeight;
             offsetFromBottom++)
        {
            var y = height - 1 - offsetFromBottom;
            var offset = y * width;
            var diff = CalculateRowAverageDifference(templatePixels, pixels, offset, width);
            if (diff > maxDiffThreshold) return false;
        }

        return true;
    }

    /// <summary>
    ///     Stitches a vertical sequence of images into a single SKBitmap canvas.
    ///     Identical top headers appear only on the first shot; identical bottom footers appear only on the last shot.
    ///     Inner headers and inner footers appearing on intermediate/scrolled frames are detected and eliminated.
    /// </summary>
    public static SKBitmap StitchVertical(IReadOnlyList<SKBitmap> images, IProgress<string>? progress = null)
    {
        if (images.Count == 0) return new SKBitmap(1, 1);

        if (images.Count == 1)
        {
            progress?.Report("Single image provided, copying image");
            return images[0].Copy();
        }

        var width = images[0].Width;
        var height = images[0].Height;

        progress?.Report($"Detecting static header and footer across {images.Count} images");
        var globalHeaderHeight = DetectStaticHeaderHeight(images);
        var globalFooterHeight = DetectStaticFooterHeight(images);

        if (globalHeaderHeight > 0)
            progress?.Report($"Detected static header of {globalHeaderHeight}px");
        if (globalFooterHeight > 0)
            progress?.Report($"Detected static footer of {globalFooterHeight}px");

        var innerHeaderHeight = DetectInnerHeaderHeight(images, globalHeaderHeight);
        var innerFooterHeight = DetectInnerFooterHeight(images, globalFooterHeight);

        if (innerHeaderHeight > 0)
            progress?.Report($"Detected inner scroll header of {innerHeaderHeight}px");
        if (innerFooterHeight > 0)
            progress?.Report($"Detected inner scroll footer of {innerFooterHeight}px");

        var pixelArrays = new SKColor[images.Count][];
        for (var i = 0; i < images.Count; i++)
        {
            pixelArrays[i] = images[i].Pixels;
        }

        var activeTops = new int[images.Count];
        var activeBottoms = new int[images.Count];

        for (var i = 0; i < images.Count; i++)
        {
            if (i == 0)
            {
                activeTops[i] = 0;
            }
            else
            {
                var hasInnerH = innerHeaderHeight > 0 &&
                    HasInnerHeader(pixelArrays[i], pixelArrays[1], globalHeaderHeight, innerHeaderHeight, width);
                activeTops[i] = hasInnerH ? globalHeaderHeight + innerHeaderHeight : globalHeaderHeight;
            }

            var hasInnerF = innerFooterHeight > 0 &&
                HasInnerFooter(pixelArrays[i], pixelArrays[1], globalFooterHeight, innerFooterHeight, width, height);

            if (i == images.Count - 1)
            {
                activeBottoms[i] = hasInnerF ? height - (globalFooterHeight + innerFooterHeight) : height;
            }
            else
            {
                activeBottoms[i] = hasInnerF ? height - (globalFooterHeight + innerFooterHeight) : height - globalFooterHeight;
            }
        }

        // Calculate slices for each image
        var slices = new List<(int imageIndex, SKRectI sourceRect)>();

        for (var i = 0; i < images.Count; i++)
        {
            var activeTop = activeTops[i];
            var activeBottom = activeBottoms[i];

            if (i < images.Count - 1)
            {
                var nextActiveTop = activeTops[i + 1];
                var nextActiveBottom = activeBottoms[i + 1];

                progress?.Report($"Matching image {i + 1} and image {i + 2}");
                var shift = FindBestVerticalOffset(images[i], images[i + 1], activeTop, activeBottom,
                    nextActiveTop, nextActiveBottom);

                // Cutoff for current image: rows from activeTop to (nextActiveTop + shift)
                var cutoff = Math.Min(activeBottom, nextActiveTop + shift);
                if (cutoff <= activeTop)
                {
                    // No new rows from this step (e.g. duplicate frame or zero shift)
                    if (i == 0 && activeTop > 0)
                    {
                        slices.Add((i, new SKRectI(0, 0, width, activeTop)));
                    }
                }
                else
                {
                    slices.Add((i, new SKRectI(0, activeTop, width, cutoff)));
                }
            }
            else
            {
                // Last image: from activeTop to activeBottom
                if (activeBottom > activeTop)
                {
                    slices.Add((i, new SKRectI(0, activeTop, width, activeBottom)));
                }
            }
        }

        // Calculate total canvas height
        var totalCanvasHeight = 0;
        foreach (var slice in slices)
        {
            totalCanvasHeight += slice.sourceRect.Height;
        }

        if (totalCanvasHeight <= 0) totalCanvasHeight = height;

        progress?.Report($"Assembling final stitched canvas: {width}x{totalCanvasHeight}px");

        var destination = new SKBitmap(width, totalCanvasHeight);
        using var canvas = new SKCanvas(destination);

        var currentY = 0;
        for (var s = 0; s < slices.Count; s++)
        {
            var slice = slices[s];
            if (slice.sourceRect.Height <= 0) continue;

            using var img = SKImage.FromBitmap(images[slice.imageIndex]);
            var srcRect = new SKRect(slice.sourceRect.Left, slice.sourceRect.Top, slice.sourceRect.Right,
                slice.sourceRect.Bottom);
            var destRect = new SKRect(0, currentY, width, currentY + slice.sourceRect.Height);

            canvas.DrawImage(img, srcRect, destRect, SKSamplingOptions.Default);
            currentY += slice.sourceRect.Height;
        }

        return destination;
    }

    /// <summary>
    ///     Stitches a sequence of encoded image byte arrays into a single JPEG byte array.
    /// </summary>
    public static async Task<byte[]> StitchImageBytesVertical(IReadOnlyList<byte[]> imageBytesList,
        int jpegQuality = 100, IProgress<string>? progress = null)
    {
        if (imageBytesList.Count == 0) return [];

        if (imageBytesList.Count == 1) return imageBytesList[0];

        return await Task.Run(() =>
        {
            var bitmaps = new List<SKBitmap>();
            try
            {
                foreach (var bytes in imageBytesList)
                {
                    var bmp = SKBitmap.Decode(bytes);
                    if (bmp != null) bitmaps.Add(bmp);
                }

                if (bitmaps.Count == 0) return [];

                using var stitched = StitchVertical(bitmaps, progress);
                using var image = SKImage.FromBitmap(stitched);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, jpegQuality);
                return data.ToArray();
            }
            finally
            {
                foreach (var b in bitmaps) b.Dispose();
            }
        });
    }

    /// <summary>
    ///     Stitches a sequence of image files into a single JPEG file.
    /// </summary>
    public static async Task<string> StitchImageFilesVertical(IReadOnlyList<string> imageFiles,
        string outputFileFullName, int jpegQuality = 100, IProgress<string>? progress = null)
    {
        progress?.Report($"Loading {imageFiles.Count} image files for vertical stitching");

        var outputFileName = Path.ChangeExtension(outputFileFullName, ".jpg");

        var bitmaps = new List<SKBitmap>();
        try
        {
            foreach (var file in imageFiles)
            {
                if (!File.Exists(file)) continue;
                var bmp = SKBitmap.Decode(file);
                if (bmp != null) bitmaps.Add(bmp);
            }

            if (bitmaps.Count == 0)
                throw new InvalidOperationException("No valid images could be loaded from the provided file paths.");

            var stitched = await Task.Run(() => StitchVertical(bitmaps, progress));
            try
            {
                using var image = SKImage.FromBitmap(stitched);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, jpegQuality);

                var directory = Path.GetDirectoryName(outputFileName);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                await using var stream = File.OpenWrite(outputFileName);
                data.SaveTo(stream);
            }
            finally
            {
                stitched.Dispose();
            }

            return outputFileName;
        }
        finally
        {
            foreach (var b in bitmaps) b.Dispose();
        }
    }
}
