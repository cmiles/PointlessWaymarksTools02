using Microsoft.Playwright;
using SkiaSharp;

namespace PointlessWaymarks.VisualWebWork;

public static class PlaywrightScreenShot
{
    private static bool _playwrightInitialized;

    public static async Task<ScreenshotResult> CaptureHtmlScreenshot(string htmlContent, IProgress<string>? progress,
        int browserWidth = 1920, int? maxHeight = null)
    {
        try
        {
            await EnsurePlaywrightInitialized();

            progress?.Report("Initializing Playwright");

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            progress?.Report("Opening page");
            var page = await browser.NewPageAsync();

            // Set initial viewport size
            await page.SetViewportSizeAsync(browserWidth, 1080);

            // Set the HTML content
            await page.SetContentAsync(htmlContent);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            return await CapturePageScreenshot(page, progress, browserWidth, maxHeight);
        }
        catch (Exception ex)
        {
            return ScreenshotResult.CreateError($"Screenshot capture failed: {ex.Message}");
        }
    }

    private static async Task<ScreenshotResult> CapturePageScreenshot(IPage page,
        IProgress<string>? progress, int browserWidth = 1920, int? maxHeight = null,
        List<string>? hideElementSelectors = null)
    {
        try
        {
            progress?.Report("Preparing page for screenshot");

            // Turn off overflow to prevent scrollbars
            await page.EvaluateAsync(@"
            document.querySelector('body').style.overflowY='visible';
            document.querySelector('body').style.overflow='hidden';
        ");

            // Hide elements that match the provided selectors
            if (hideElementSelectors != null && hideElementSelectors.Any())
            {
                progress?.Report($"Hiding {hideElementSelectors.Count} elements based on provided selectors");

                foreach (var selector in hideElementSelectors)
                    try
                    {
                        // Hide all elements matching the selector (no state preservation)
                        await page.EvaluateAsync($@"
                        document.querySelectorAll('{EscapeJsString(selector)}').forEach(element => element.style.display = 'none')");
                    }
                    catch (Exception ex)
                    {
                        progress?.Report(
                            $"Warning: Failed to hide elements with selector '{selector}': {ex.Message}");
                        // Continue with the remaining selectors even if one fails
                    }
            }

            // Set viewport width
            var documentWidth = browserWidth;
            var viewportHeight = await page.EvaluateAsync<int>("window.innerHeight");
            var viewportWidth = await page.EvaluateAsync<int>("window.innerWidth");

            progress?.Report($"Viewport Height {viewportHeight}, Width {viewportWidth}");

            // Horizontal chunks calculation remains the same
            var horizontalChunks = (int)Math.Ceiling((double)documentWidth / viewportWidth);

            // Initialize collection for vertical chunks
            var verticalImageBytesList = new List<byte[]>();
            var rowScrollPositions = new List<int>(); // Track actual scroll positions
            var currentScrollPosition = 0;
            var isEndOfPage = false;
            var rowIndex = 0;
            var accumulatedHeight = 0;

            // Continue scrolling and capturing until we reach the end of the page or maxHeight
            while (!isEndOfPage && (!maxHeight.HasValue || accumulatedHeight < maxHeight.Value))
            {
                rowIndex++;
                progress?.Report($"Processing Row {rowIndex}");

                // Store this row's scroll position
                rowScrollPositions.Add(currentScrollPosition);

                using var rowImage = new SKBitmap(documentWidth, viewportHeight);
                using var rowCanvas = new SKCanvas(rowImage);
                var currentWidth = 0;

                for (var j = 0; j < horizontalChunks; j++)
                {
                    progress?.Report($"Processing Column {j + 1} of {horizontalChunks} in Row {rowIndex}");

                    // Scroll to position
                    await page.EvaluateAsync($@"
                    window.scrollTo({{
                        top: {currentScrollPosition},
                        left: {j * viewportWidth},
                        behavior: 'instant'
                    }});");

                    // Get the actual scroll position after scrolling
                    var actualScrollPosition = await page.EvaluateAsync<int>("window.scrollY");

                    // Update the stored scroll position if it's different from what we requested
                    if (rowIndex == rowScrollPositions.Count)
                        rowScrollPositions[rowIndex - 1] = actualScrollPosition;

                    // Wait for any dynamic content to settle
                    await Task.Delay(1000);

                    // Capture the current viewport
                    var screenshot = await page.ScreenshotAsync(new PageScreenshotOptions
                    {
                        Type = ScreenshotType.Png
                    });

                    using var image = SKBitmap.Decode(screenshot);
                    var sourceRect = new SKRect(0, 0, image.Width, image.Height);
                    var destRect = new SKRect(currentWidth, 0, currentWidth + image.Width, image.Height);

                    if (j == horizontalChunks - 1 && horizontalChunks > 1)
                    {
                        var neededLastImageWidth = documentWidth % viewportWidth;
                        if (neededLastImageWidth == 0) neededLastImageWidth = viewportWidth;

                        // Take the rightmost portion of the source image
                        sourceRect = new SKRect(image.Width - neededLastImageWidth, 0, image.Width, image.Height);

                        // Position it correctly in the destination without adjustment
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

                using var rowImageEncoded = SKImage.FromBitmap(rowImage);
                using var rowImageStream = new MemoryStream();
                rowImageEncoded.Encode(SKEncodedImageFormat.Png, 100).SaveTo(rowImageStream);
                verticalImageBytesList.Add(rowImageStream.ToArray());

                // Calculate next scroll position
                var prevScrollPosition = currentScrollPosition;
                currentScrollPosition += viewportHeight;

                // Attempt to scroll to next position
                await page.EvaluateAsync($"window.scrollTo(0, {currentScrollPosition})");

                // Get the actual scroll position after scrolling
                var newScrollPosition = await page.EvaluateAsync<int>("window.scrollY");

                // Update accumulated height
                if (rowIndex == 1)
                {
                    // First row - full height
                    accumulatedHeight = viewportHeight;
                }
                else
                {
                    // Calculate the overlap with previous row
                    var overlap = rowScrollPositions[rowIndex - 2] + viewportHeight - newScrollPosition;
                    var effectiveHeight = viewportHeight - Math.Max(0, overlap);
                    accumulatedHeight += effectiveHeight;
                }

                // Check if we've reached the end of the page:
                // 1. If we couldn't scroll further from the previous position
                // 2. If we've reached maxHeight (if specified)
                if (newScrollPosition <= prevScrollPosition ||
                    (maxHeight.HasValue && accumulatedHeight >= maxHeight.Value))
                {
                    isEndOfPage = true;
                    progress?.Report("Reached end of page or maxHeight");
                }

                // Update currentScrollPosition to the actual position
                currentScrollPosition = newScrollPosition;
            }

            progress?.Report($"Finished capturing {rowIndex} rows with accumulated height of {accumulatedHeight}");

            // Calculate the final height to ensure we don't exceed maxHeight
            var finalDocumentHeight =
                maxHeight.HasValue ? Math.Min(accumulatedHeight, maxHeight.Value) : accumulatedHeight;

            progress?.Report($"Final Document Height: {finalDocumentHeight}, Width: {documentWidth}");

            // Assemble final image
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
                    // Calculate the overlap with the previous row
                    var previousRowPosition = rowScrollPositions[i - 1];
                    var currentRowPosition = rowScrollPositions[i];
                    var overlap = previousRowPosition + viewportHeight - currentRowPosition;

                    // Skip pixels that overlap with the previous row
                    var sourceStartY = Math.Max(0, overlap);

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

            progress?.Report("Screenshot capture complete");

            return ScreenshotResult.CreateSuccess(finalImageBytes);
        }
        catch (Exception ex)
        {
            return ScreenshotResult.CreateError($"Screenshot capture failed: {ex.Message}");
        }
    }

    public static async Task<ScreenshotResult> CaptureScreenshot(string url, IProgress<string>? progress,
        int browserWidth = 1920, int? maxHeight = null, List<string>? hideElementSelectors = null)
    {
        try
        {
            await EnsurePlaywrightInitialized();

            progress?.Report("Initializing Playwright");

            using var playwright = await Playwright.CreateAsync();
            await using var browser =
                await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });

            progress?.Report($"Opening page - {url}");

            var page = await browser.NewPageAsync();

            // Set initial viewport size
            await page.SetViewportSizeAsync(browserWidth, 1080);

            await page.GotoAsync(url);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            return await CapturePageScreenshot(page, progress, browserWidth, maxHeight, hideElementSelectors);
        }
        catch (Exception ex)
        {
            return ScreenshotResult.CreateError($"Screenshot capture failed: {ex.Message}");
        }
    }

    private static Task EnsurePlaywrightInitialized()
    {
        if (_playwrightInitialized) return Task.CompletedTask;

        var exitCode = Program.Main(["install", "chromium"]);
        if (exitCode != 0) throw new Exception($"Playwright installation failed with exit code {exitCode}");

        _playwrightInitialized = true;
        return Task.CompletedTask;
    }

    // Helper method to escape special characters in JS strings
    private static string EscapeJsString(string input)
    {
        return input
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }
}