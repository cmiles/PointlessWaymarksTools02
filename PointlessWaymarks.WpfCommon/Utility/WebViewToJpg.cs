using System.Globalization;
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

    public static async Task<string?> SaveByteArrayAsJpgToFilename(byte[]? byteArray, string fileName,
        StatusControlContext statusContext)
    {
        if (byteArray is null) return null;
        if (string.IsNullOrWhiteSpace(fileName)) return null;

        var newFilename = fileName;

        if (!(Path.GetExtension(newFilename).Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
              Path.GetExtension(newFilename).Equals(".jpeg", StringComparison.OrdinalIgnoreCase)))
            newFilename += ".jpg";

        await ThreadSwitcher.ResumeBackgroundAsync();

        statusContext.Progress($"Writing {byteArray.Length} image bytes");

        // Ensure target directory exists (dialog-less workflow)
        var dir = Path.GetDirectoryName(newFilename);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

        await File.WriteAllBytesAsync(newFilename, byteArray);

        await statusContext.ToastSuccess($"Screenshot saved to {newFilename}");

        return newFilename;
    }

    private static JsonElement ParseScriptResult(string scriptResultJson)
    {
        var doc = JsonDocument.Parse(scriptResultJson);
        if (doc.RootElement.ValueKind == JsonValueKind.String)
        {
            var innerJson = doc.RootElement.GetString();
            if (!string.IsNullOrWhiteSpace(innerJson) &&
                (innerJson.TrimStart().StartsWith('{') || innerJson.TrimStart().StartsWith('[')))
            {
                return JsonDocument.Parse(innerJson).RootElement.Clone();
            }
        }

        return doc.RootElement.Clone();
    }

    private static async Task<byte[]?> CaptureOpenSilverViaDevTools(
        WebView2CompositionControl webContentWebView,
        double devicePixelRatio,
        int maxHeight,
        IProgress<string>? progress)
    {
        progress?.Report("OpenSilver WebAssembly Detected - Analyzing Page Structure for DevTools Capture");

        var pageMeasureScript = """
                                (() => {
                                    var docEl = document.documentElement;
                                    var body = document.body;
                                    var openSilverRoot = document.getElementById('opensilver-root') || document.querySelector('.opensilver-root-element');
                                    var vHeight = (window.visualViewport && window.visualViewport.height) ? window.visualViewport.height : (window.innerHeight || (docEl ? docEl.clientHeight : 0));
                                    var vWidth = (window.visualViewport && window.visualViewport.width) ? window.visualViewport.width : (window.innerWidth || (docEl ? docEl.clientWidth : 0));

                                    var totalHeight = Math.max(
                                        docEl ? docEl.clientHeight : 0,
                                        docEl ? docEl.scrollHeight : 0,
                                        docEl ? docEl.offsetHeight : 0,
                                        body ? body.clientHeight : 0,
                                        body ? body.scrollHeight : 0,
                                        body ? body.offsetHeight : 0,
                                        openSilverRoot ? openSilverRoot.scrollHeight : 0,
                                        openSilverRoot ? openSilverRoot.offsetHeight : 0,
                                        openSilverRoot ? openSilverRoot.clientHeight : 0,
                                        vHeight
                                    );
                                    var totalWidth = Math.max(
                                        docEl ? docEl.clientWidth : 0,
                                        docEl ? docEl.scrollWidth : 0,
                                        docEl ? docEl.offsetWidth : 0,
                                        body ? body.clientWidth : 0,
                                        body ? body.scrollWidth : 0,
                                        body ? body.offsetWidth : 0,
                                        openSilverRoot ? openSilverRoot.scrollWidth : 0,
                                        openSilverRoot ? openSilverRoot.offsetWidth : 0,
                                        openSilverRoot ? openSilverRoot.clientWidth : 0,
                                        vWidth
                                    );

                                    if (openSilverRoot && openSilverRoot.children) {
                                        for (var i = 0; i < openSilverRoot.children.length; i++) {
                                            var child = openSilverRoot.children[i];
                                            if (child.scrollHeight && child.scrollHeight > totalHeight) totalHeight = child.scrollHeight;
                                            if (child.scrollWidth && child.scrollWidth > totalWidth) totalWidth = child.scrollWidth;
                                            if (child.offsetHeight && child.offsetHeight > totalHeight) totalHeight = child.offsetHeight;
                                            if (child.offsetWidth && child.offsetWidth > totalWidth) totalWidth = child.offsetWidth;
                                        }
                                    }

                                    return {
                                        totalHeight: Math.ceil(totalHeight),
                                        totalWidth: Math.ceil(totalWidth),
                                        viewportHeight: Math.ceil(vHeight),
                                        viewportWidth: Math.ceil(vWidth)
                                    };
                                })()
                                """;

        try
        {
            var targetInfoJson = await webContentWebView.CoreWebView2.ExecuteScriptAsync(pageMeasureScript)
                .WaitAsync(TimeSpan.FromSeconds(5));
            var targetInfo = ParseScriptResult(targetInfoJson);

            var viewportHeightCssPixels = targetInfo.TryGetProperty("viewportHeight", out var vhProp)
                ? Math.Max(1, (int)vhProp.GetDouble())
                : 1080;
            var viewportWidthCssPixels = targetInfo.TryGetProperty("viewportWidth", out var vwProp)
                ? Math.Max(1, (int)vwProp.GetDouble())
                : 1920;

            var rawHeight = targetInfo.TryGetProperty("totalHeight", out var thProp)
                ? (int)thProp.GetDouble()
                : viewportHeightCssPixels;
            var rawWidth = targetInfo.TryGetProperty("totalWidth", out var twProp)
                ? (int)twProp.GetDouble()
                : viewportWidthCssPixels;

            // Safe clamping to avoid GPU texture allocation crashes (max dimension 16384 physical pixels)
            var maxDimensionCss = (int)(16384 / Math.Max(1.0, devicePixelRatio));
            var documentHeightCssPixels = Math.Clamp(Math.Max(rawHeight, viewportHeightCssPixels), 100,
                Math.Min(maxHeight, maxDimensionCss));
            var documentWidthCssPixels = Math.Clamp(Math.Max(rawWidth, viewportWidthCssPixels), 100, maxDimensionCss);

            // Convert to physical pixels for progress reporting
            var documentHeight = (int)(documentHeightCssPixels * devicePixelRatio);
            var documentWidth = (int)(documentWidthCssPixels * devicePixelRatio);
            var viewportHeight = (int)(viewportHeightCssPixels * devicePixelRatio);
            var viewportWidth = (int)(viewportWidthCssPixels * devicePixelRatio);

            progress?.Report($"OpenSilver Document (CSS): {documentWidthCssPixels}x{documentHeightCssPixels}, " +
                             $"Viewport (CSS): {viewportWidthCssPixels}x{viewportHeightCssPixels}");
            progress?.Report($"OpenSilver Document (Physical): {documentWidth}x{documentHeight}, " +
                             $"Viewport (Physical): {viewportWidth}x{viewportHeight}");

            progress?.Report("Setting Device Emulation for Full Page Capture");
            var setMetricsParams =
                $$"""{"width": {{documentWidthCssPixels}}, "height": {{documentHeightCssPixels}}, "deviceScaleFactor": {{devicePixelRatio.ToString(CultureInfo.InvariantCulture)}}, "mobile": false}""";
            await webContentWebView.CoreWebView2
                .CallDevToolsProtocolMethodAsync("Emulation.setDeviceMetricsOverride", setMetricsParams)
                .WaitAsync(TimeSpan.FromSeconds(10));

            await Task.Delay(1000);

            // Re-check if dimensions expanded after layout pass (with timeout and error isolation)
            try
            {
                var updatedInfoJson = await webContentWebView.CoreWebView2.ExecuteScriptAsync(pageMeasureScript)
                    .WaitAsync(TimeSpan.FromSeconds(5));
                var updatedInfo = ParseScriptResult(updatedInfoJson);
                var updatedHeight = updatedInfo.TryGetProperty("totalHeight", out var uthProp)
                    ? Math.Clamp((int)uthProp.GetDouble(), 100, Math.Min(maxHeight, maxDimensionCss))
                    : documentHeightCssPixels;
                var updatedWidth = updatedInfo.TryGetProperty("totalWidth", out var utwProp)
                    ? Math.Clamp((int)utwProp.GetDouble(), 100, maxDimensionCss)
                    : documentWidthCssPixels;

                if (updatedHeight > documentHeightCssPixels || updatedWidth > documentWidthCssPixels)
                {
                    documentHeightCssPixels = Math.Max(documentHeightCssPixels, updatedHeight);
                    documentWidthCssPixels = Math.Max(documentWidthCssPixels, updatedWidth);
                    progress?.Report(
                        $"Updated Document Dimensions after Layout: {documentWidthCssPixels}x{documentHeightCssPixels}");
                    var updatedMetricsParams =
                        $$"""{"width": {{documentWidthCssPixels}}, "height": {{documentHeightCssPixels}}, "deviceScaleFactor": {{devicePixelRatio.ToString(CultureInfo.InvariantCulture)}}, "mobile": false}""";
                    await webContentWebView.CoreWebView2
                        .CallDevToolsProtocolMethodAsync("Emulation.setDeviceMetricsOverride", updatedMetricsParams)
                        .WaitAsync(TimeSpan.FromSeconds(10));
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                progress?.Report(
                    $"Layout dimension re-check skipped ({ex.Message}), continuing with initial dimensions.");
            }

            progress?.Report("Capturing Screenshot via DevTools Protocol");
            var captureParams = $$"""
                                  {
                                      "format": "jpeg",
                                      "quality": 95,
                                      "fromSurface": true,
                                      "clip": {
                                          "x": 0,
                                          "y": 0,
                                          "width": {{documentWidthCssPixels}},
                                          "height": {{documentHeightCssPixels}},
                                          "scale": 1
                                      }
                                  }
                                  """;
            var captureResultJson = await webContentWebView.CoreWebView2
                .CallDevToolsProtocolMethodAsync("Page.captureScreenshot", captureParams)
                .WaitAsync(TimeSpan.FromSeconds(15));
            using var captureDoc = JsonDocument.Parse(captureResultJson);
            if (captureDoc.RootElement.TryGetProperty("data", out var dataProp))
            {
                var base64Data = dataProp.GetString();
                if (!string.IsNullOrEmpty(base64Data))
                {
                    return Convert.FromBase64String(base64Data);
                }
            }
        }
        catch (Exception ex)
        {
            progress?.Report($"DevTools Protocol Capture failed ({ex.Message}), falling back to Chunked Capture");
        }
        finally
        {
            try
            {
                await webContentWebView.CoreWebView2
                    .CallDevToolsProtocolMethodAsync("Emulation.clearDeviceMetricsOverride", "{}")
                    .WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Ignore cleanup error
            }
        }

        return null;
    }

    public static async Task<OneOf<Success<byte[]>, Error<string>>> SaveCurrentPageAsJpeg(
        WebView2CompositionControl webContentWebView, IProgress<string>? progress)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var maxHeight = 300000;

        progress?.Report("Starting Capture");

        // Get the device pixel ratio first - this is crucial for proper scaling
        var devicePixelRatioString = await webContentWebView.CoreWebView2
            .ExecuteScriptAsync("window.devicePixelRatio || 1")
            .WaitAsync(TimeSpan.FromSeconds(5));
        var devicePixelRatio =
            double.TryParse(devicePixelRatioString, NumberStyles.Any, CultureInfo.InvariantCulture, out var dpr) &&
            dpr > 0
                ? dpr
                : 1.0;

        progress?.Report($"Device Pixel Ratio: {devicePixelRatio}");

        var viewPortTopUserPositionString =
            await webContentWebView.CoreWebView2.ExecuteScriptAsync(
                    "(window.visualViewport && window.visualViewport.pageTop !== undefined) ? window.visualViewport.pageTop : (window.pageYOffset || 0)")
                .WaitAsync(TimeSpan.FromSeconds(5));
        var viewPortTopUserPosition = (int)(decimal.TryParse(viewPortTopUserPositionString, NumberStyles.Any,
            CultureInfo.InvariantCulture, out var vTop)
            ? vTop
            : 0);

        var viewPortLeftUserPositionString =
            await webContentWebView.CoreWebView2.ExecuteScriptAsync(
                    "(window.visualViewport && window.visualViewport.pageLeft !== undefined) ? window.visualViewport.pageLeft : (window.pageXOffset || 0)")
                .WaitAsync(TimeSpan.FromSeconds(5));
        var viewPortLeftUserPosition = (int)(decimal.TryParse(viewPortLeftUserPositionString, NumberStyles.Any,
            CultureInfo.InvariantCulture, out var vLeft)
            ? vLeft
            : 0);

        // Probe for OpenSilver / Silverlight WebAssembly
        var isOpenSilverScript = """
                                 (() => {
                                     var hasOpenSilverRoot = !!document.getElementById('opensilver-root') || !!document.querySelector('.opensilver-root-element');
                                     var hasOpenSilverScript = Array.from(document.scripts).some(s => s.src && (s.src.toLowerCase().includes('opensilver') || s.src.toLowerCase().includes('cshtml5')));
                                     var hasOpenSilverGlobal = typeof window.OpenSilver !== 'undefined' || typeof window.CSHTML5 !== 'undefined';
                                     var hasOpenSilverElements = !!document.querySelector('[class*="opensilver-"]');

                                     return hasOpenSilverRoot || hasOpenSilverScript || hasOpenSilverGlobal || hasOpenSilverElements;
                                 })()
                                 """;
        var isOpenSilverResultJson = await webContentWebView.CoreWebView2.ExecuteScriptAsync(isOpenSilverScript)
            .WaitAsync(TimeSpan.FromSeconds(5));
        var isOpenSilver = false;
        try
        {
            var parsed = ParseScriptResult(isOpenSilverResultJson);
            if (parsed.ValueKind == JsonValueKind.True || parsed.ValueKind == JsonValueKind.False)
                isOpenSilver = parsed.GetBoolean();
            else if (parsed.ValueKind == JsonValueKind.String)
                isOpenSilver = bool.TryParse(parsed.GetString(), out var b) && b;
        }
        catch
        {
            isOpenSilver = false;
        }

        byte[]? capturedImageBytes = null;

        if (isOpenSilver)
        {
            capturedImageBytes =
                await CaptureOpenSilverViaDevTools(webContentWebView, devicePixelRatio, maxHeight, progress);
        }

        // Main proven chunked-capture path for regular web pages (or as fallback)
        if (capturedImageBytes is null || capturedImageBytes.Length == 0)
        {
            progress?.Report(
                $"Current Top {viewPortTopUserPosition}, Left {viewPortLeftUserPosition} - Disabling Smooth Scrolling, Unlocking Page Layout, and Hiding Scrollbars");

            var pagePrepScript = """
                                 (() => {
                                     var style = document.getElementById('pwt-screenshot-override-style');
                                     if (!style) {
                                         style = document.createElement('style');
                                         style.id = 'pwt-screenshot-override-style';
                                         (document.head || document.documentElement).appendChild(style);
                                     }
                                     style.innerHTML = `
                                         * {
                                             scroll-behavior: auto !important;
                                             animation: none !important;
                                             transition: none !important;
                                         }
                                         html {
                                             scroll-behavior: auto !important;
                                             overflow: visible !important;
                                             overflow-y: visible !important;
                                             height: auto !important;
                                             min-height: 100% !important;
                                             max-height: none !important;
                                         }
                                         body {
                                             scroll-behavior: auto !important;
                                             overflow: visible !important;
                                             overflow-y: visible !important;
                                             height: auto !important;
                                             min-height: 100% !important;
                                             max-height: none !important;
                                             position: static !important;
                                         }
                                         ::-webkit-scrollbar {
                                             display: none !important;
                                         }
                                     `;

                                     if (document.documentElement) {
                                         document.documentElement.style.setProperty('scroll-behavior', 'auto', 'important');
                                         document.documentElement.style.setProperty('overflow', 'visible', 'important');
                                         document.documentElement.style.setProperty('overflow-y', 'visible', 'important');
                                         document.documentElement.style.setProperty('height', 'auto', 'important');
                                         document.documentElement.style.setProperty('max-height', 'none', 'important');
                                     }
                                     if (document.body) {
                                         document.body.style.setProperty('scroll-behavior', 'auto', 'important');
                                         document.body.style.setProperty('overflow', 'visible', 'important');
                                         document.body.style.setProperty('overflow-y', 'visible', 'important');
                                         document.body.style.setProperty('height', 'auto', 'important');
                                         document.body.style.setProperty('max-height', 'none', 'important');
                                         if (window.getComputedStyle(document.body).position === 'fixed') {
                                             document.body.style.setProperty('position', 'static', 'important');
                                         }
                                     }

                                     // Unlock scrollable containers / clipping elements (e.g. SPAs, modals, authwalls, LinkedIn)
                                     var all = document.querySelectorAll('*');
                                     for (var i = 0; i < all.length; i++) {
                                         var el = all[i];
                                         if (el === document.documentElement || el === document.body) continue;
                                         var s = window.getComputedStyle(el);
                                         if ((s.overflowY === 'hidden' || s.overflowY === 'auto' || s.overflowY === 'scroll' ||
                                              s.overflow === 'hidden' || s.overflow === 'auto' || s.overflow === 'scroll') &&
                                             (s.height.includes('px') || s.height.includes('%') || s.height.includes('vh') || s.maxHeight !== 'none')) {
                                             if (el.scrollHeight > el.clientHeight + 10) {
                                                 el.style.setProperty('height', 'auto', 'important');
                                                 el.style.setProperty('max-height', 'none', 'important');
                                                 el.style.setProperty('overflow', 'visible', 'important');
                                                 el.style.setProperty('overflow-y', 'visible', 'important');
                                             }
                                         }
                                     }
                                 })()
                                 """;
            await webContentWebView.CoreWebView2.ExecuteScriptAsync(pagePrepScript)
                .WaitAsync(TimeSpan.FromSeconds(5));

            var pageMeasureScript = """
                                    (() => {
                                        var docEl = document.documentElement;
                                        var body = document.body;
                                        var vHeight = (window.visualViewport && window.visualViewport.height) ? window.visualViewport.height : (window.innerHeight || (docEl ? docEl.clientHeight : 0));
                                        var vWidth = (window.visualViewport && window.visualViewport.width) ? window.visualViewport.width : (window.innerWidth || (docEl ? docEl.clientWidth : 0));

                                        var totalHeight = Math.max(
                                            docEl ? docEl.clientHeight : 0,
                                            docEl ? docEl.scrollHeight : 0,
                                            docEl ? docEl.offsetHeight : 0,
                                            body ? body.clientHeight : 0,
                                            body ? body.scrollHeight : 0,
                                            body ? body.offsetHeight : 0,
                                            vHeight
                                        );
                                        var totalWidth = Math.max(
                                            docEl ? docEl.clientWidth : 0,
                                            docEl ? docEl.scrollWidth : 0,
                                            docEl ? docEl.offsetWidth : 0,
                                            body ? body.clientWidth : 0,
                                            body ? body.scrollWidth : 0,
                                            body ? body.offsetWidth : 0,
                                            vWidth
                                        );

                                        var all = document.querySelectorAll('*');
                                        for (var i = 0; i < all.length; i++) {
                                            var el = all[i];
                                            if (el.scrollHeight && el.scrollHeight > totalHeight) totalHeight = el.scrollHeight;
                                            if (el.offsetHeight && el.offsetHeight > totalHeight) totalHeight = el.offsetHeight;
                                            if (el.scrollWidth && el.scrollWidth > totalWidth) totalWidth = el.scrollWidth;
                                            if (el.offsetWidth && el.offsetWidth > totalWidth) totalWidth = el.offsetWidth;
                                            var rect = el.getBoundingClientRect();
                                            if (rect && (rect.bottom + window.scrollY) > totalHeight) {
                                                var pos = window.getComputedStyle(el).position;
                                                if (pos !== 'fixed') {
                                                    totalHeight = Math.max(totalHeight, rect.bottom + window.scrollY);
                                                }
                                            }
                                        }

                                        return {
                                            totalHeight: Math.ceil(totalHeight),
                                            totalWidth: Math.ceil(totalWidth),
                                            viewportHeight: Math.ceil(vHeight),
                                            viewportWidth: Math.ceil(vWidth)
                                        };
                                    })()
                                    """;

            var dimensionJson = await webContentWebView.CoreWebView2.ExecuteScriptAsync(pageMeasureScript)
                .WaitAsync(TimeSpan.FromSeconds(5));
            var dimensionInfo = ParseScriptResult(dimensionJson);

            var viewportHeightCssPixels = dimensionInfo.TryGetProperty("viewportHeight", out var vhProp)
                ? Math.Max(1, (int)vhProp.GetDouble())
                : 1080;
            var viewportWidthCssPixels = dimensionInfo.TryGetProperty("viewportWidth", out var vwProp)
                ? Math.Max(1, (int)vwProp.GetDouble())
                : 1920;

            var documentHeightCssPixels = dimensionInfo.TryGetProperty("totalHeight", out var thProp)
                ? Math.Max((int)thProp.GetDouble(), viewportHeightCssPixels)
                : viewportHeightCssPixels;
            var documentWidthCssPixels = dimensionInfo.TryGetProperty("totalWidth", out var twProp)
                ? Math.Max((int)twProp.GetDouble(), viewportWidthCssPixels)
                : viewportWidthCssPixels;

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
                progress?.Report(
                    "Document Height Greater than Viewport Height - Fixed and Sticky Positioning to Absolute");
                await webContentWebView.CoreWebView2.ExecuteScriptAsync("""
                                                                        var x = document.querySelectorAll('*');
                                                                        for(var i=0; i<x.length; i++) {
                                                                            var elementStyle = window.getComputedStyle(x[i]);
                                                                            if(elementStyle.position=="fixed" || elementStyle.position=="sticky") {
                                                                                x[i].style.setProperty('position', 'absolute', 'important');
                                                                            }
                                                                        }
                                                                        """).WaitAsync(TimeSpan.FromSeconds(5));
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
                                                 (() => {
                                                     var targetTop = {{currentScrollPositionCssPixels}};
                                                     var targetLeft = {{j * viewportWidthCssPixels}};
                                                     window.scrollTo({
                                                         top: targetTop,
                                                         left: targetLeft,
                                                         behavior: "instant"
                                                     });
                                                     if (document.documentElement) {
                                                         document.documentElement.scrollTop = targetTop;
                                                         document.documentElement.scrollLeft = targetLeft;
                                                     }
                                                     if (document.body) {
                                                         document.body.scrollTop = targetTop;
                                                         document.body.scrollLeft = targetLeft;
                                                     }
                                                     if (document.scrollingElement) {
                                                         document.scrollingElement.scrollTop = targetTop;
                                                         document.scrollingElement.scrollLeft = targetLeft;
                                                     }
                                                 })()
                                                 """;
                    await webContentWebView.CoreWebView2.ExecuteScriptAsync(scrollToViewFunction)
                        .WaitAsync(TimeSpan.FromSeconds(5));
                    await Task.Delay(1000);

                    // Get the actual scroll position after scrolling (CSS pixels)
                    var actualScrollPositionString = await webContentWebView.CoreWebView2.ExecuteScriptAsync(
                        "(window.scrollY || window.pageYOffset || (document.documentElement ? document.documentElement.scrollTop : 0) || (document.body ? document.body.scrollTop : 0) || (document.scrollingElement ? document.scrollingElement.scrollTop : 0) || 0)"
                    ).WaitAsync(TimeSpan.FromSeconds(5));
                    var actualScrollPositionCssPixels = (int)(decimal.TryParse(actualScrollPositionString,
                        NumberStyles.Any, CultureInfo.InvariantCulture, out var actScroll)
                        ? actScroll
                        : 0);

                    // Update the stored scroll position if it's different from what we requested
                    if (rowIndex == rowScrollPositions.Count)
                        rowScrollPositions[rowIndex - 1] = actualScrollPositionCssPixels;

                    // Capture image (in physical pixels)
                    using var stream = new MemoryStream();
                    await webContentWebView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,
                        stream);
                    var imageBytes = stream.ToArray();

                    using var image = SKImage.FromEncodedData(imageBytes);

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

                    rowCanvas.DrawImage(image, sourceRect, destRect, SKSamplingOptions.Default);
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
                var scrollToNextPositionFunction = $$"""
                                                     (() => {
                                                         var targetTop = {{currentScrollPositionCssPixels}};
                                                         window.scrollTo({
                                                             top: targetTop,
                                                             left: 0,
                                                             behavior: "instant"
                                                         });
                                                         if (document.documentElement) {
                                                             document.documentElement.scrollTop = targetTop;
                                                             document.documentElement.scrollLeft = 0;
                                                         }
                                                         if (document.body) {
                                                             document.body.scrollTop = targetTop;
                                                             document.body.scrollLeft = 0;
                                                         }
                                                         if (document.scrollingElement) {
                                                             document.scrollingElement.scrollTop = targetTop;
                                                             document.scrollingElement.scrollLeft = 0;
                                                         }
                                                     })()
                                                     """;
                await webContentWebView.CoreWebView2.ExecuteScriptAsync(scrollToNextPositionFunction)
                    .WaitAsync(TimeSpan.FromSeconds(5));
                await Task.Delay(250);

                // Get the actual scroll position after scrolling (CSS pixels)
                var newScrollPositionString = await webContentWebView.CoreWebView2.ExecuteScriptAsync(
                    "(window.scrollY || window.pageYOffset || (document.documentElement ? document.documentElement.scrollTop : 0) || (document.body ? document.body.scrollTop : 0) || (document.scrollingElement ? document.scrollingElement.scrollTop : 0) || 0)"
                ).WaitAsync(TimeSpan.FromSeconds(5));
                var newScrollPositionCssPixels = (int)(decimal.TryParse(newScrollPositionString,
                    NumberStyles.Any, CultureInfo.InvariantCulture, out var nScroll)
                    ? nScroll
                    : 0);

                // Update accumulated height (physical pixels)
                if (rowIndex == 1)
                {
                    // First row - full height
                    accumulatedHeight = viewportHeight;
                }
                else
                {
                    // Calculate the overlap with previous row (CSS pixels)
                    var overlapCssPixels = rowScrollPositions[rowIndex - 2] + viewportHeightCssPixels -
                                           newScrollPositionCssPixels;
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

                using var image = SKImage.FromEncodedData(verticalImageBytesList[i]);

                if (i == 0)
                {
                    // First row - include full height
                    var sourceRect = new SKRect(0, 0, image.Width, Math.Min(image.Height, finalDocumentHeight));
                    var destRect = new SKRect(0, 0, image.Width, Math.Min(image.Height, finalDocumentHeight));
                    canvas.DrawImage(image, sourceRect, destRect, SKSamplingOptions.Default);
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

                    canvas.DrawImage(image, sourceRect, destRect, SKSamplingOptions.Default);
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
            capturedImageBytes = imageStream.ToArray();
        }

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

        return new Success<byte[]>(capturedImageBytes);
    }
}