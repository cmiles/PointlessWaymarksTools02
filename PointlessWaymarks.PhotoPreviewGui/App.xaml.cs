using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Messaging;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.WpfCommon.AppMessages;
using PointlessWaymarks.WpfCommon.PhotoPreview;
using PointlessWaymarks.WpfCommon.Utility;
using Serilog;

namespace PointlessWaymarks.PhotoPreviewGui;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private PhotoPreviewIpcChannel? _ipcChannel;
    private MainWindow? _previewWindow;

    public App()
    {
        LogTools.StandardStaticLoggerForDefaultLogDirectory("PwPhotoPreviewGui");
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string? channelId = null;
        string? initialFile = null;
        string? initialTitle = null;
        var initialRating = 0;

        for (var i = 0; i < e.Args.Length; i++)
        {
            var arg = e.Args[i];
            if (arg.Equals("--channel-id", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-c", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < e.Args.Length)
                {
                    channelId = e.Args[++i];
                }
            }
            else if (arg.Equals("--file", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("-f", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < e.Args.Length)
                {
                    initialFile = e.Args[++i];
                }
            }
            else if (arg.Equals("--title", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("-t", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < e.Args.Length)
                {
                    initialTitle = e.Args[++i];
                }
            }
            else if (arg.Equals("--rating", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("-r", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < e.Args.Length && int.TryParse(e.Args[++i], out var parsedRating))
                {
                    initialRating = parsedRating;
                }
            }
            else if (!arg.StartsWith("-") && File.Exists(arg))
            {
                initialFile = arg;
            }
        }

        _previewWindow = await PhotoPreviewGui.MainWindow.CreateInstance();
        MainWindow = _previewWindow;

        if (!string.IsNullOrWhiteSpace(channelId))
        {
            _ipcChannel = new PhotoPreviewIpcChannel(channelId);
            _ipcChannel.BindToMessenger();
            _ipcChannel.CloseReceived += (_, _) =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    _previewWindow?.Close();
                });
            };
        }

        _previewWindow.Closed += async (_, _) =>
        {
            if (_ipcChannel != null)
            {
                await _ipcChannel.DisposeAsync();
            }
            Shutdown();
        };

        await _previewWindow.PositionWindowAndShowOnUiThread();

        if (!string.IsNullOrWhiteSpace(initialFile) && File.Exists(initialFile))
        {
            initialTitle ??= Path.GetFileName(initialFile);
            WeakReferenceMessenger.Default.Send(
                new PhotoPreviewRequestMessage(new PhotoPreviewRequestData(initialFile, initialTitle, initialRating, null)));
        }
    }

    public static bool HandleApplicationException(Exception ex)
    {
        Log.Error(ex, "Application Reached HandleApplicationException thru App_DispatcherUnhandledException");

        var msg = $"Something went wrong...\r\n\r\n{ex.Message}\r\n\r\n" + "The error has been logged...\r\n\r\n" +
                  "Do you want to continue?";

        var res = MessageBox.Show(msg, "Photo Preview App Error", MessageBoxButton.YesNo,
            MessageBoxImage.Error,
            MessageBoxResult.Yes);

        return res != MessageBoxResult.No;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (!HandleApplicationException(e.Exception))
            Environment.Exit(1);

        e.Handled = true;
    }
}
