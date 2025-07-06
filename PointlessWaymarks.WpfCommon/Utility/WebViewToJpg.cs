using System.IO;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using OneOf;
using OneOf.Types;
using Ookii.Dialogs.Wpf;
using PointlessWaymarks.WpfCommon.Status;
using SkiaSharp;

namespace PointlessWaymarks.WpfCommon.Utility;

public static class WebViewToJpg
{
    public static async Task<string?> SaveByteArrayAsJpg(byte[]? byteArray, string suggestedStartingDirectory,
        StatusControlContext statusContext)
    {
        if (byteArray is null) return null;

        var saveDialog = new VistaSaveFileDialog { Filter = "jpg files (*.jpg;*.jpeg)|*.jpg;*.jpeg" };

        var suggestedDirectoryIsValid = !string.IsNullOrWhiteSpace(suggestedStartingDirectory) &&
                                        Directory.Exists(suggestedStartingDirectory);

        if (suggestedDirectoryIsValid)
            saveDialog.FileName = $"{suggestedStartingDirectory}\\";

        if (!saveDialog.ShowDialog() ?? true) return null;

        var newFilename = saveDialog.FileName;

        if (!(Path.GetExtension(newFilename).Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
              Path.GetExtension(newFilename).Equals(".jpeg", StringComparison.OrdinalIgnoreCase)))
            newFilename += ".jpg";

        await ThreadSwitcher.ResumeBackgroundAsync();

        statusContext.Progress($"Writing {byteArray.Length} image bytes");

        await File.WriteAllBytesAsync(newFilename, byteArray);

        await statusContext.ToastSuccess($"Screenshot saved to {newFilename}");

        return newFilename;
    }

    public static async Task<OneOf<Success<byte[]>, Error<string>>> SaveCurrentPageAsJpeg(
        WebView2CompositionControl webContentWebView, IProgress<string>? progress)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var maxHeight = 300000;

        progress?.Report("Starting Capture");

        // Get the device pixel ratio first - this is crucial for proper scaling
        var devicePixelRatio = double.Parse(
            await webContentWebView.CoreWebView2.ExecuteScriptAsync("window.devicePixelRatio")
        );

        progress?.Report($"Device Pixel Ratio: {devicePixelRatio}");

        var viewPortTopUserPositionString =
            await webContentWebView.CoreWebView2.ExecuteScriptAsync("visualViewport.pageTop");
        var viewPortTopUserPosition = (int)decimal.Parse(viewPortTopUserPositionString);

        var viewPortLeftUserPositionString =
            await webContentWebView.CoreWebView2.ExecuteScriptAsync("visualViewport.pageLeft");
        var viewPortLeftUserPosition = (int)decimal.Parse(viewPortLeftUserPositionString);

        progress?.Report(
            $"Current Top {viewPortTopUserPosition}, Left {viewPortLeftUserPosition} - Turning off Overflow");

        await webContentWebView.CoreWebView2.ExecuteScriptAsync(
            "document.querySelector('body').style.overflowY='visible'");
        await webContentWebView.CoreWebView2.ExecuteScriptAsync(
            "document.querySelector('body').style.overflow='hidden'");

        var documentHeightArray = await webContentWebView.CoreWebView2.ExecuteScriptAsync(
            """
            [
                document.documentElement.clientHeight,
                document.body ? document.body.scrollHeight : 0,
                document.documentElement.scrollHeight,
                document.body ? document.body.offsetHeight : 0,
                document.documentElement.offsetHeight
            ]
            """);
        var documentHeightCssPixels = JsonSerializer.Deserialize<List<int>>(documentHeightArray)!.Max();

        var documentWidthArray = await webContentWebView.CoreWebView2.ExecuteScriptAsync(
            """
            [
                document.documentElement.clientWidth,
                document.body ? document.body.scrollWidth : 0,
                document.documentElement.scrollWidth,
                document.body ? document.body.offsetWidth : 0,
                document.documentElement.offsetWidth
            ]
            """);
        var documentWidthCssPixels = JsonSerializer.Deserialize<List<int>>(documentWidthArray)!.Max();

        // Get viewport dimensions in CSS pixels
        var viewportHeightCssPixels =
            (int)decimal.Parse(await webContentWebView.CoreWebView2.ExecuteScriptAsync("visualViewport.height"));
        var viewportWidthCssPixels = 
            (int)decimal.Parse(await webContentWebView.CoreWebView2.ExecuteScriptAsync("visualViewport.width"));

        // Convert to physical pixels for image operations
        var documentHeight = (int)(documentHeightCssPixels * devicePixelRatio);
        var documentWidth = (int)(documentWidthCssPixels * devicePixelRatio);
        var viewportHeight = (int)(viewportHeightCssPixels * devicePixelRatio);
        var viewportWidth = (int)(viewportWidthCssPixels * devicePixelRatio);

        progress?.Report($"Document (CSS): {documentWidthCssPixels}x{documentHeightCssPixels}, " +
                         $"Viewport (CSS): {viewportWidthCssPixels}x{viewportHeightCssPixels}");
        progress?.Report($"Document (Physical): {documentWidth}x{documentHeight}, " +
                         $"Viewport (Physical): {viewportWidth}x{viewportHeight}");

        if (documentHeightCssPixels > viewportHeightCssPixels)
        {
            progress?.Report("Document Height Greater than Viewport Height - Fixed and Sticky Positioning to Absolute");
            await webContentWebView.CoreWebView2.ExecuteScriptAsync("""
                                                                    var x = document.querySelectorAll('*');
                                                                    for(var i=0; i<x.length; i++) {
                                                                        elementStyle = getComputedStyle(x[i]);
                                                                        if(elementStyle.position=="fixed" || elementStyle.position=="sticky") {
                                                                            x[i].style.position="absolute";
                                                                        }
                                                                    }
                                                                    """);
        }

        var horizontalChunksCssPixels = (int)Math.Ceiling((double)documentWidthCssPixels / viewportWidthCssPixels);

        // Initialize collection for vertical chunks
        var verticalImageBytesList = new List<byte[]>();
        var rowScrollPositions = new List<int>(); // Track actual scroll positions (CSS pixels)
        var currentScrollPositionCssPixels = 0;
        var isEndOfPage = false;
        var rowIndex = 0;
        var accumulatedHeight = 0;

        // Continue scrolling and capturing until we reach the end of the page or maxHeight
        while (!isEndOfPage && accumulatedHeight < maxHeight)
        {
            rowIndex++;
            progress?.Report($"Processing Row {rowIndex}");

            // Store this row's scroll position (CSS pixels)
            rowScrollPositions.Add(currentScrollPositionCssPixels);

            // Create row image in physical pixels
            using var rowImage = new SKBitmap(documentWidth, viewportHeight);
            using var rowCanvas = new SKCanvas(rowImage);
            var currentWidth = 0;

            for (var j = 0; j < horizontalChunksCssPixels; j++)
            {
                progress?.Report($"Processing Column {j + 1} of {horizontalChunksCssPixels} in Row {rowIndex}");

                // Scroll in CSS pixels
                var scrollToViewFunction = $$"""
                                             window.scrollTo({
                                                 top: {{currentScrollPositionCssPixels}},
                                                 left: {{j * viewportWidthCssPixels}},
                                                 behavior: "instant"
                                             });
                                             """;
                await webContentWebView.CoreWebView2.ExecuteScriptAsync(scrollToViewFunction);
                await Task.Delay(1000);

                // Get the actual scroll position after scrolling (CSS pixels)
                var actualScrollPositionCssPixels =
                    (int)decimal.Parse(await webContentWebView.CoreWebView2.ExecuteScriptAsync("window.scrollY"));

                // Update the stored scroll position if it's different from what we requested
                if (rowIndex == rowScrollPositions.Count)
                    rowScrollPositions[rowIndex - 1] = actualScrollPositionCssPixels;

                // Capture image (in physical pixels)
                using var stream = new MemoryStream();
                await webContentWebView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,
                    stream);
                var imageBytes = stream.ToArray();

                using var image = SKBitmap.Decode(imageBytes);

                var sourceRect = new SKRect(0, 0, image.Width, image.Height);
                var destRect = new SKRect(currentWidth, 0, currentWidth + image.Width, image.Height);

                if (j == horizontalChunksCssPixels - 1 && horizontalChunksCssPixels > 1)
                {
                    // For the last chunk, calculate remaining width in physical pixels
                    var remainingWidthCssPixels = documentWidthCssPixels - (j * viewportWidthCssPixels);
                    var neededLastImageWidth = (int)(remainingWidthCssPixels * devicePixelRatio);
                    
                    if (neededLastImageWidth == 0) 
                        neededLastImageWidth = viewportWidth;
                    
                    // Only use the needed portion of the source image
                    sourceRect = new SKRect(0, 0, neededLastImageWidth, image.Height);
                    // Position it correctly at the current width
                    destRect = new SKRect(currentWidth, 0, currentWidth + neededLastImageWidth, image.Height);
                    currentWidth += neededLastImageWidth;
                }
                else
                {
                    currentWidth += image.Width;
                }

                rowCanvas.DrawBitmap(image, sourceRect, destRect);
            }

            progress?.Report($"Row {rowIndex} - Encoding Image");

            // Save completed row
            using var rowImageEncoded = SKImage.FromBitmap(rowImage);
            using var rowImageStream = new MemoryStream();
            rowImageEncoded.Encode(SKEncodedImageFormat.Png, 100).SaveTo(rowImageStream);
            verticalImageBytesList.Add(rowImageStream.ToArray());

            // Calculate next scroll position (CSS pixels)
            var prevScrollPositionCssPixels = currentScrollPositionCssPixels;
            currentScrollPositionCssPixels += viewportHeightCssPixels;

            // Attempt to scroll to next position (CSS pixels)
            await webContentWebView.CoreWebView2.ExecuteScriptAsync($"window.scrollTo(0, {currentScrollPositionCssPixels})");

            // Get the actual scroll position after scrolling (CSS pixels)
            var newScrollPositionCssPixels =
                (int)decimal.Parse(await webContentWebView.CoreWebView2.ExecuteScriptAsync("window.scrollY"));

            // Update accumulated height (physical pixels)
            if (rowIndex == 1)
            {
                // First row - full height
                accumulatedHeight = viewportHeight;
            }
            else
            {
                // Calculate the overlap with previous row (CSS pixels)
                var overlapCssPixels = rowScrollPositions[rowIndex - 2] + viewportHeightCssPixels - newScrollPositionCssPixels;
                // Convert to physical pixels
                var overlapPhysical = (int)(overlapCssPixels * devicePixelRatio);
                var effectiveHeight = viewportHeight - Math.Max(0, overlapPhysical);
                accumulatedHeight += effectiveHeight;
            }

            // Check if we've reached the end of the page or maxHeight
            if (newScrollPositionCssPixels <= prevScrollPositionCssPixels ||
                accumulatedHeight > maxHeight)
            {
                isEndOfPage = true;
                progress?.Report("Reached end of page or maxHeight");
            }

            // Update current position to actual position
            currentScrollPositionCssPixels = newScrollPositionCssPixels;
        }

        progress?.Report($"Finished capturing {rowIndex} rows with accumulated height of {accumulatedHeight}px");

        // Calculate the final height to ensure we don't exceed maxHeight
        var finalDocumentHeight = Math.Min(accumulatedHeight, maxHeight);

        progress?.Report($"Final Document Height: {finalDocumentHeight}px, Width: {documentWidth}px");

        // Assemble final image (in physical pixels)
        using var finalImage = new SKBitmap(documentWidth, finalDocumentHeight);
        using var canvas = new SKCanvas(finalImage);
        var currentHeight = 0;

        for (var i = 0; i < verticalImageBytesList.Count; i++)
        {
            progress?.Report($"Final Image Assembly - Row {i + 1} of {verticalImageBytesList.Count}");

            using var image = SKBitmap.Decode(verticalImageBytesList[i]);

            if (i == 0)
            {
                // First row - include full height
                var sourceRect = new SKRect(0, 0, image.Width, Math.Min(image.Height, finalDocumentHeight));
                var destRect = new SKRect(0, 0, image.Width, Math.Min(image.Height, finalDocumentHeight));
                canvas.DrawBitmap(image, sourceRect, destRect);
                currentHeight = Math.Min(image.Height, finalDocumentHeight);
            }
            else
            {
                // Calculate the overlap with the previous row (CSS pixels)
                var previousRowPositionCss = rowScrollPositions[i - 1];
                var currentRowPositionCss = rowScrollPositions[i];
                var overlapCssPixels = previousRowPositionCss + viewportHeightCssPixels - currentRowPositionCss;
                
                // Convert to physical pixels for image operations
                var overlapPhysical = (int)(overlapCssPixels * devicePixelRatio);

                // Skip pixels that overlap with the previous row
                var sourceStartY = Math.Max(0, overlapPhysical);

                // Calculate how much of this row's content to include
                var remainingHeight = finalDocumentHeight - currentHeight;
                var availableHeight = image.Height - sourceStartY;
                var heightToInclude = Math.Min(remainingHeight, availableHeight);

                if (heightToInclude <= 0)
                    break; // We've reached the end of our final height

                var sourceRect = new SKRect(0, sourceStartY, image.Width, sourceStartY + heightToInclude);
                var destRect = new SKRect(0, currentHeight, image.Width, currentHeight + heightToInclude);

                canvas.DrawBitmap(image, sourceRect, destRect);
                currentHeight += heightToInclude;
            }

            // If we've reached our target height, stop
            if (currentHeight >= finalDocumentHeight)
                break;
        }

        progress?.Report("Encoding final image");

        using var imageStream = new MemoryStream();
        using var finalImageEncoded = SKImage.FromBitmap(finalImage);
        finalImageEncoded.Encode(SKEncodedImageFormat.Jpeg, 100).SaveTo(imageStream);
        var finalImageBytes = imageStream.ToArray();

        await ThreadSwitcher.ResumeForegroundAsync();

        progress?.Report("Capture Complete - Reloading and Scrolling to Original Position");

        webContentWebView.Reload();

        var scrollBackToUserPositionFunction = $$"""
                                                 window.scrollTo({
                                                    top: {{viewPortTopUserPosition}},
                                                    left: {{viewPortLeftUserPosition}},
                                                    behavior: "instant"
                                                 });
                                                 """;

        await webContentWebView.CoreWebView2.ExecuteScriptAsync(scrollBackToUserPositionFunction);

        return new Success<byte[]>(finalImageBytes);
    }
}