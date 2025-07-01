using Microsoft.Playwright;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PointlessWaymarks.VisualWebWork;

public static class PlaywrightScreenShot
{
    private static bool _playwrightInitialized;

    public static async Task<ScreenshotResult> CaptureHtmlScreenshot(string htmlContent, IProgress<string>? progress, int browserWidth = 1920, int? maxHeight = null)
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

    public static async Task<ScreenshotResult> CaptureScreenshot(string url, IProgress<string>? progress, int browserWidth = 1920, int? maxHeight = null)
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
            
            await page.GotoAsync(url);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            return await CapturePageScreenshot(page, progress, browserWidth, maxHeight);
        }
        catch (Exception ex)
        {
            return ScreenshotResult.CreateError($"Screenshot capture failed: {ex.Message}");
        }
    }

    private static async Task<ScreenshotResult> CapturePageScreenshot(IPage page, IProgress<string>? progress, int browserWidth = 1920, int? maxHeight = null)
    {
        try
        {
            progress?.Report("Getting page dimensions");

            // Turn off overflow to prevent scrollbars
            await page.EvaluateAsync(@"
                document.querySelector('body').style.overflowY='visible';
                document.querySelector('body').style.overflow='hidden';
            ");
            
            // Get full page dimensions
            var documentHeight = await page.EvaluateAsync<int>(@"Math.max(
                document.documentElement.clientHeight,
                document.body ? document.body.scrollHeight : 0,
                document.documentElement.scrollHeight,
                document.body ? document.body.offsetHeight : 0,
                document.documentElement.offsetHeight
            )");

            // If maxHeight is specified and document height exceeds it, cap the height
            if (maxHeight.HasValue && documentHeight > maxHeight.Value)
            {
                documentHeight = maxHeight.Value;
            }

            var documentWidth = browserWidth;

            var viewportHeight = await page.EvaluateAsync<int>("window.innerHeight");
            var viewportWidth = await page.EvaluateAsync<int>("window.innerWidth");

            progress?.Report($"Document Height {documentHeight}, Width {documentWidth}");
            progress?.Report($"Viewport Height {viewportHeight}, Width {viewportWidth}");

            var verticalChunks = (int)Math.Ceiling((double)documentHeight / viewportHeight);
            var horizontalChunks = (int)Math.Ceiling((double)documentWidth / viewportWidth);

            var verticalImageBytesList = new List<byte[]>();

            for (var i = 0; i < verticalChunks; i++)
            {
                progress?.Report($"Processing Row {i + 1} of {verticalChunks}");

                using var rowImage = new SKBitmap(documentWidth, viewportHeight);
                using var rowCanvas = new SKCanvas(rowImage);
                var currentWidth = 0;

                for (var j = 0; j < horizontalChunks; j++)
                {
                    progress?.Report($"Processing Column {j + 1} of {horizontalChunks} in Row {i + 1}");

                    // Scroll to position
                    await page.EvaluateAsync($@"
                        window.scrollTo({{
                            top: {i * viewportHeight},
                            left: {j * viewportWidth},
                            behavior: 'instant'
                        }});
                    ");

                    // Wait for any dynamic content to settle
                    await Task.Delay(500);

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
                        destRect = new SKRect(currentWidth - (image.Width - neededLastImageWidth), 0,
                            currentWidth + neededLastImageWidth, image.Height);
                        currentWidth += neededLastImageWidth;
                    }
                    else
                    {
                        currentWidth += image.Width;
                    }

                    rowCanvas.DrawBitmap(image, sourceRect, destRect);
                }

                progress?.Report($"Row {i + 1} - Encoding Image");

                using var rowImageEncoded = SKImage.FromBitmap(rowImage);
                using var rowImageStream = new MemoryStream();
                rowImageEncoded.Encode(SKEncodedImageFormat.Png, 100).SaveTo(rowImageStream);
                verticalImageBytesList.Add(rowImageStream.ToArray());
            }

            using var finalImage = new SKBitmap(documentWidth, documentHeight);
            using var canvas = new SKCanvas(finalImage);
            var currentHeight = 0;

            for (var i = 0; i < verticalImageBytesList.Count; i++)
            {
                progress?.Report($"Final Image Assembly - Row {i + 1} of {verticalImageBytesList.Count}");

                using var image = SKBitmap.Decode(verticalImageBytesList[i]);
                var sourceRect = new SKRect(0, 0, image.Width, image.Height);
                var destRect = new SKRect(0, currentHeight, image.Width, currentHeight + image.Height);

                if (i == verticalImageBytesList.Count - 1 && verticalImageBytesList.Count > 1)
                {
                    var neededLastImageHeight = documentHeight % viewportHeight;
                    if (neededLastImageHeight == 0) neededLastImageHeight = viewportHeight;
                    destRect = new SKRect(0, currentHeight - (image.Height - neededLastImageHeight), image.Width,
                        currentHeight + neededLastImageHeight);
                    currentHeight += neededLastImageHeight;
                }
                else
                {
                    currentHeight += image.Height;
                }

                canvas.DrawBitmap(image, sourceRect, destRect);
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

    private static async Task EnsurePlaywrightInitialized()
    {
        if (_playwrightInitialized) return;

        var exitCode = Program.Main(["install", "chromium"]);
        if (exitCode != 0) throw new Exception($"Playwright installation failed with exit code {exitCode}");

        _playwrightInitialized = true;
    }
}