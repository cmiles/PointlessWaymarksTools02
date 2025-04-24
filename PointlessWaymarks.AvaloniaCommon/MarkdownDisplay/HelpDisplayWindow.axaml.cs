using Avalonia.Controls;
using PointlessWaymarks.AvaloniaCommon.Status;
using PointlessWaymarks.AvaloniaCommon.Utility;
using PointlessWaymarks.AvaloniaLlamaAspects;

namespace PointlessWaymarks.AvaloniaCommon.MarkdownDisplay;

public partial class HelpDisplayWindow : Window
{
    public HelpDisplayWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public required HelpDisplayContext HelpContext { get; set; }
    public required StatusControlContext StatusContext { get; set; }
    public string WindowTitle { get; set; } = "Help and Information";

    public static async Task CreateInstanceAndShow(List<string> markdown, string windowTitle)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var factoryStatusContext = await StatusControlContext.CreateInstance();

        var window = new HelpDisplayWindow
        {
            HelpContext = new HelpDisplayContext(markdown),
            StatusContext = factoryStatusContext,
            WindowTitle = windowTitle
        };

        await window.PositionWindowAndShowOnUiThread();
    }
} 