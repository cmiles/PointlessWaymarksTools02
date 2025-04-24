using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using PointlessWaymarks.AvaloniaCommon.Status;
using PointlessWaymarks.AvaloniaLlamaAspects;

namespace PointlessWaymarks.AvaloniaCommon.WindowScreenShot;

public partial class WindowScreenShotControl : UserControl
{
    public WindowScreenShotControl()
    {
        InitializeComponent();

        WindowScreenShotCommand = new AsyncRelayCommand<Window>(async window =>
        {
            if (window == null) return;

            StatusControlContext? statusContext = null;

            try
            {
                statusContext = (StatusControlContext)((dynamic)window.DataContext).StatusContext;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            var result = await AvaloniaCapture.TryWindowScreenShotToClipboardAsync(window);

            if (statusContext != null)
            {
                if (!result)
                    await statusContext.ToastError("Problem Copying Window to Clipboard");
                else
                    await statusContext.ToastSuccess("Copied to Clipboard");
            }
        });

        DataContext = this;
    }

    public AsyncRelayCommand<Window> WindowScreenShotCommand { get; }
} 