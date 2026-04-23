using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.WpfCommon.PhotoPreview;

[NotifyPropertyChanged]
public partial class PhotoPreviewWindow
{
    private const double MaxZoom = 10.0;
    private const double MinZoom = 0.05;
    private const double ZoomStep = 0.25;

    private bool _isPanning;
    private Point _panStart;
    private double _scrollStartH;
    private double _scrollStartV;

    // Lock Zoom state - stored as percentages (0.0-1.0) of scrollable extent
    private double _lockedScrollPercentageX;
    private double _lockedScrollPercentageY;

    public PhotoPreviewWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public required PhotoPreviewContext PreviewContext { get; set; }
    public required StatusControlContext StatusContext { get; set; }
    public string WindowTitle { get; set; } = "Photo Preview";

    /// <summary>
    ///     Creates a new instance — can be called from any thread.
    ///     Does not show the window; use PositionWindowAndShowOnUiThread().
    /// </summary>
    public static async Task<PhotoPreviewWindow> CreateInstance(bool writeRatingToFile = true)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var statusContext = await StatusControlContext.CreateInstance();
        var factoryContext = await PhotoPreviewContext.CreateInstance(statusContext, writeRatingToFile);

        var window = new PhotoPreviewWindow
        {
            StatusContext = statusContext,
            PreviewContext = factoryContext
        };

        await ThreadSwitcher.ResumeBackgroundAsync();

        window.PreviewContext.PreviewImageLoaded += window.OnPreviewImageLoaded;
        window.PreviewContext.PropertyChanged += window.OnPreviewContextPropertyChanged;

        await ThreadSwitcher.ResumeForegroundAsync();

        return window;
    }

    private void FitImageToWindow()
    {
        if (PreviewContext.PreviewImage == null) return;

        var viewportWidth = ImageScrollViewer.ActualWidth;
        var viewportHeight = ImageScrollViewer.ActualHeight;

        if (viewportWidth <= 0 || viewportHeight <= 0) return;

        // Use Width/Height (DIPs) not PixelWidth/PixelHeight — Stretch="None"
        // renders at DIP size, which differs from pixel size when DPI ≠ 96.
        var imageWidth = PreviewContext.PreviewImage.Width;
        var imageHeight = PreviewContext.PreviewImage.Height;

        if (imageWidth <= 0 || imageHeight <= 0) return;

        var scaleX = viewportWidth / imageWidth;
        var scaleY = viewportHeight / imageHeight;
        PreviewContext.ZoomLevel = Math.Clamp(Math.Min(scaleX, scaleY), MinZoom, MaxZoom);
    }

    private void ImageScrollViewer_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Ignore clicks on scrollbar areas (beyond the viewport)
        var pos = e.GetPosition(ImageScrollViewer);
        if (pos.X > ImageScrollViewer.ViewportWidth || pos.Y > ImageScrollViewer.ViewportHeight) return;

        _isPanning = true;
        _panStart = pos;
        _scrollStartH = ImageScrollViewer.HorizontalOffset;
        _scrollStartV = ImageScrollViewer.VerticalOffset;
        ImageScrollViewer.CaptureMouse();
        Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void ImageScrollViewer_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning) return;

        var current = e.GetPosition(ImageScrollViewer);
        ImageScrollViewer.ScrollToHorizontalOffset(_scrollStartH - (current.X - _panStart.X));
        ImageScrollViewer.ScrollToVerticalOffset(_scrollStartV - (current.Y - _panStart.Y));
        e.Handled = true;
    }

    private void ImageScrollViewer_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPanning) return;

        _isPanning = false;
        ImageScrollViewer.ReleaseMouseCapture();
        Cursor = Cursors.Arrow;
        e.Handled = true;
    }

    private void ImageScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;

        var oldZoom = PreviewContext.ZoomLevel;
        var factor = e.Delta > 0 ? 1.15 : 1.0 / 1.15;
        var newZoom = Math.Clamp(oldZoom * factor, MinZoom, MaxZoom);

        if (Math.Abs(newZoom - oldZoom) < 0.0001) return;

        // Get the mouse position in the content's un-scaled coordinate system
        var contentPoint = e.GetPosition(ImageContainer);
        // Get the mouse position relative to the viewport
        var viewportPoint = e.GetPosition(ImageScrollViewer);

        PreviewContext.ZoomLevel = newZoom;

        // Force layout so the ScrollViewer recalculates extents with the new zoom
        ImageScrollViewer.UpdateLayout();

        // Find where the same content point now appears in the viewport
        var newViewportPoint = ImageContainer.TranslatePoint(contentPoint, ImageScrollViewer);

        // Adjust scroll offsets so the content point stays under the cursor
        ImageScrollViewer.ScrollToHorizontalOffset(
            ImageScrollViewer.HorizontalOffset + newViewportPoint.X - viewportPoint.X);
        ImageScrollViewer.ScrollToVerticalOffset(
            ImageScrollViewer.VerticalOffset + newViewportPoint.Y - viewportPoint.Y);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        PreviewContext.PreviewImageLoaded -= OnPreviewImageLoaded;
        PreviewContext.PropertyChanged -= OnPreviewContextPropertyChanged;
        PreviewContext.Cleanup();
        base.OnClosing(e);
    }

    private void OnPreviewContextPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PhotoPreviewContext.DisplayTitle))
            Dispatcher.InvokeAsync(() => WindowTitle = $"Photo Preview - {PreviewContext.DisplayTitle}");
    }

    private async void OpenFileInExplorer_OnClick(object sender, RoutedEventArgs e)
    {
        var path = PreviewContext.CurrentFilePath;
        if (!string.IsNullOrWhiteSpace(path))
            await ProcessHelpers.OpenExplorerWindowForFile(path);
    }

    private void OnPreviewImageLoaded(object? sender, EventArgs e)
    {
        // This outer lambda runs at Normal priority (9), which is BEFORE the
        // DataBind-priority (8) binding update for the new PreviewImage.
        // That means the old image is still displayed, so we can capture its
        // scroll state accurately.
        Dispatcher.InvokeAsync(() =>
        {
            if (PreviewContext.LockZoom && PreviewContext.PreviewImage != null)
            {
                // Capture scroll position and zoom while the old image is still laid out
                SaveScrollPosition();
                var savedZoom = PreviewContext.ZoomLevel;

                // Restore at Loaded priority (6) — after the image binding update
                // (DataBind=8) and layout pass (Render=7) have completed.
                Dispatcher.InvokeAsync(() =>
                {
                    PreviewContext.ZoomLevel = savedZoom;
                    ImageScrollViewer.UpdateLayout();
                    RestoreScrollPosition();
                }, System.Windows.Threading.DispatcherPriority.Loaded);
            }
            else
            {
                // Fit-to-window also needs to run after the new image has been laid out
                Dispatcher.InvokeAsync(FitImageToWindow,
                    System.Windows.Threading.DispatcherPriority.Loaded);
            }
        });
    }

    /// <summary>
    ///     Saves the current scroll position as percentages of the scrollable extent.
    ///     Call this before navigating to capture the user's view position.
    /// </summary>
    private void SaveScrollPosition()
    {
        var extentWidth = ImageScrollViewer.ExtentWidth - ImageScrollViewer.ViewportWidth;
        var extentHeight = ImageScrollViewer.ExtentHeight - ImageScrollViewer.ViewportHeight;

        _lockedScrollPercentageX = extentWidth > 0
            ? ImageScrollViewer.HorizontalOffset / extentWidth
            : 0.5; // Default to center if no scrollable extent

        _lockedScrollPercentageY = extentHeight > 0
            ? ImageScrollViewer.VerticalOffset / extentHeight
            : 0.5; // Default to center if no scrollable extent

        // Clamp to valid range
        _lockedScrollPercentageX = Math.Clamp(_lockedScrollPercentageX, 0.0, 1.0);
        _lockedScrollPercentageY = Math.Clamp(_lockedScrollPercentageY, 0.0, 1.0);
    }

    /// <summary>
    ///     Restores the scroll position from stored percentages.
    ///     Handles different aspect ratios by clamping to valid scroll extents.
    /// </summary>
    private void RestoreScrollPosition()
    {
        var extentWidth = ImageScrollViewer.ExtentWidth - ImageScrollViewer.ViewportWidth;
        var extentHeight = ImageScrollViewer.ExtentHeight - ImageScrollViewer.ViewportHeight;

        // Calculate target offsets from percentages
        var targetH = extentWidth > 0 ? _lockedScrollPercentageX * extentWidth : 0;
        var targetV = extentHeight > 0 ? _lockedScrollPercentageY * extentHeight : 0;

        // Clamp to valid scroll ranges (smart fallback for different aspect ratios)
        targetH = Math.Clamp(targetH, 0, Math.Max(0, extentWidth));
        targetV = Math.Clamp(targetV, 0, Math.Max(0, extentHeight));

        ImageScrollViewer.ScrollToHorizontalOffset(targetH);
        ImageScrollViewer.ScrollToVerticalOffset(targetV);
    }

    private void ZoomActual_OnClick(object sender, RoutedEventArgs e)
    {
        PreviewContext.ZoomLevel = 1.0;
    }

    private void ZoomFit_OnClick(object sender, RoutedEventArgs e)
    {
        FitImageToWindow();
    }

    private void ZoomIn_OnClick(object sender, RoutedEventArgs e)
    {
        PreviewContext.ZoomLevel = Math.Min(PreviewContext.ZoomLevel + ZoomStep, MaxZoom);
    }

    private void ZoomOut_OnClick(object sender, RoutedEventArgs e)
    {
        PreviewContext.ZoomLevel = Math.Max(PreviewContext.ZoomLevel - ZoomStep, MinZoom);
    }
}
