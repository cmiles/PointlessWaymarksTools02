using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.PhotoPreviewGui;

[NotifyPropertyChanged]
public partial class MainWindow
{
    private const double MaxZoom = 10.0;
    private const double MinZoom = 0.05;
    private const double ZoomStep = 0.25;

    private bool _isPanning;
    private Point _panStart;
    private double _scrollStartH;
    private double _scrollStartV;

    // Lock Zoom state - stored as percentages (0.0-1.0) of scrollable extent
    private double _lockedScrollPercentageX = 0.5;
    private double _lockedScrollPercentageY = 0.5;
    private string? _displayedFilePath;
    private bool _pendingRestoreScroll;

    public MainWindow()
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
    public static async Task<MainWindow> CreateInstance()
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var statusContext = await StatusControlContext.CreateInstance();
        var factoryContext = await PhotoPreviewContext.CreateInstance(statusContext);

        var window = new MainWindow
        {
            StatusContext = statusContext,
            PreviewContext = factoryContext
        };

        await ThreadSwitcher.ResumeBackgroundAsync();

        window.PreviewContext.PreviewClearing += window.OnPreviewClearing;
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
        if (PreviewContext.LockZoom)
        {
            SaveScrollPosition();
        }
        e.Handled = true;
    }

    private void ImageScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_pendingRestoreScroll && (e.ExtentWidthChange != 0 || e.ExtentHeightChange != 0 || e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0))
        {
            RestoreScrollPosition();
        }
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

        if (PreviewContext.LockZoom)
        {
            SaveScrollPosition();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        PreviewContext.PreviewClearing -= OnPreviewClearing;
        PreviewContext.PreviewImageLoaded -= OnPreviewImageLoaded;
        PreviewContext.PropertyChanged -= OnPreviewContextPropertyChanged;
        PreviewContext.Cleanup();
        base.OnClosing(e);
    }

    private void OnPreviewContextPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PhotoPreviewContext.DisplayTitle))
            Dispatcher.InvokeAsync(() => WindowTitle = $"Photo Preview - {PreviewContext.DisplayTitle}");
        else if (e.PropertyName == nameof(PhotoPreviewContext.LockZoom))
        {
            if (PreviewContext.LockZoom)
                Dispatcher.InvokeAsync(SaveScrollPosition);
        }
    }

    private void OnPreviewClearing(object? sender, EventArgs e)
    {
        void Action()
        {
            if (PreviewContext.LockZoom)
                SaveScrollPosition();
            _displayedFilePath = null;
        }

        if (Dispatcher.CheckAccess())
            Action();
        else
            Dispatcher.Invoke(Action);
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
        // That means the old image is still displayed (if one was displayed and not cleared),
        // so we can capture its scroll state accurately.
        Dispatcher.InvokeAsync(() =>
        {
            var targetFilePath = PreviewContext.CurrentFilePath;

            if (PreviewContext.LockZoom)
            {
                // Only capture scroll position if a previous image was actively displayed in the window
                // (i.e. not coming from a cleared / empty state).
                if (!string.IsNullOrEmpty(_displayedFilePath) && MainImage.Source != null)
                {
                    SaveScrollPosition();
                }

                var savedZoom = PreviewContext.ZoomLevel;
                _pendingRestoreScroll = true;

                // Restore at Loaded priority (6) — after the image binding update
                // (DataBind=8) and layout pass (Render=7) have completed.
                Dispatcher.InvokeAsync(() =>
                {
                    _displayedFilePath = targetFilePath;
                    PreviewContext.ZoomLevel = savedZoom;
                    ImageScrollViewer.UpdateLayout();
                    RestoreScrollPosition();
                }, System.Windows.Threading.DispatcherPriority.Loaded);
            }
            else
            {
                _pendingRestoreScroll = false;
                // Fit-to-window also needs to run after the new image has been laid out
                Dispatcher.InvokeAsync(() =>
                {
                    _displayedFilePath = targetFilePath;
                    FitImageToWindow();
                }, System.Windows.Threading.DispatcherPriority.Loaded);
            }
        });
    }

    /// <summary>
    ///     Saves the current scroll position as percentages of the scrollable extent.
    ///     Call this before navigating or clearing to capture the user's view position.
    /// </summary>
    private void SaveScrollPosition()
    {
        if (_pendingRestoreScroll || MainImage.Source == null) return;

        var extentWidth = ImageScrollViewer.ExtentWidth - ImageScrollViewer.ViewportWidth;
        var extentHeight = ImageScrollViewer.ExtentHeight - ImageScrollViewer.ViewportHeight;

        if (extentWidth <= 0 && extentHeight <= 0) return;

        if (extentWidth > 0)
        {
            _lockedScrollPercentageX = Math.Clamp(ImageScrollViewer.HorizontalOffset / extentWidth, 0.0, 1.0);
        }

        if (extentHeight > 0)
        {
            _lockedScrollPercentageY = Math.Clamp(ImageScrollViewer.VerticalOffset / extentHeight, 0.0, 1.0);
        }
    }

    /// <summary>
    ///     Restores the scroll position from stored percentages.
    ///     Handles different aspect ratios by clamping to valid scroll extents.
    /// </summary>
    private void RestoreScrollPosition()
    {
        var extentWidth = ImageScrollViewer.ExtentWidth - ImageScrollViewer.ViewportWidth;
        var extentHeight = ImageScrollViewer.ExtentHeight - ImageScrollViewer.ViewportHeight;

        // Check expected dimensions from PreviewImage if ScrollViewer extents haven't updated yet
        if (PreviewContext.PreviewImage is BitmapSource bmp && (extentWidth <= 0 || extentHeight <= 0))
        {
            var viewportW = ImageScrollViewer.ViewportWidth > 0 ? ImageScrollViewer.ViewportWidth : ImageScrollViewer.ActualWidth;
            var viewportH = ImageScrollViewer.ViewportHeight > 0 ? ImageScrollViewer.ViewportHeight : ImageScrollViewer.ActualHeight;
            var expectedW = (bmp.Width * PreviewContext.ZoomLevel) - viewportW;
            var expectedH = (bmp.Height * PreviewContext.ZoomLevel) - viewportH;

            // If the image is expected to be scrollable but ScrollViewer hasn't updated its extents yet,
            // keep _pendingRestoreScroll true so ScrollChanged will apply it once extents are ready.
            if ((expectedW > 1 && extentWidth <= 0) || (expectedH > 1 && extentHeight <= 0))
            {
                _pendingRestoreScroll = true;
                return;
            }
        }

        // Calculate target offsets from percentages
        var targetH = extentWidth > 0 ? _lockedScrollPercentageX * extentWidth : 0;
        var targetV = extentHeight > 0 ? _lockedScrollPercentageY * extentHeight : 0;

        // Clamp to valid scroll ranges (smart fallback for different aspect ratios)
        targetH = Math.Clamp(targetH, 0, Math.Max(0, extentWidth));
        targetV = Math.Clamp(targetV, 0, Math.Max(0, extentHeight));

        ImageScrollViewer.ScrollToHorizontalOffset(targetH);
        ImageScrollViewer.ScrollToVerticalOffset(targetV);

        _pendingRestoreScroll = false;
    }

    private void ZoomActual_OnClick(object sender, RoutedEventArgs e)
    {
        PreviewContext.ZoomLevel = 1.0;
        if (PreviewContext.LockZoom)
        {
            ImageScrollViewer.UpdateLayout();
            SaveScrollPosition();
        }
    }

    private void ZoomFit_OnClick(object sender, RoutedEventArgs e)
    {
        FitImageToWindow();
        if (PreviewContext.LockZoom)
        {
            ImageScrollViewer.UpdateLayout();
            SaveScrollPosition();
        }
    }

    private void ZoomIn_OnClick(object sender, RoutedEventArgs e)
    {
        PreviewContext.ZoomLevel = Math.Min(PreviewContext.ZoomLevel + ZoomStep, MaxZoom);
        if (PreviewContext.LockZoom)
        {
            ImageScrollViewer.UpdateLayout();
            SaveScrollPosition();
        }
    }

    private void ZoomOut_OnClick(object sender, RoutedEventArgs e)
    {
        PreviewContext.ZoomLevel = Math.Max(PreviewContext.ZoomLevel - ZoomStep, MinZoom);
        if (PreviewContext.LockZoom)
        {
            ImageScrollViewer.UpdateLayout();
            SaveScrollPosition();
        }
    }
}
