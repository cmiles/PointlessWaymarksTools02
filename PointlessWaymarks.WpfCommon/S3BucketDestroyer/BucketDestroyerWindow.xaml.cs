using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.WpfCommon.S3BucketDestroyer;

/// <summary>
///     Interaction logic for BucketDestroyerWindow.xaml
/// </summary>
[NotifyPropertyChanged]
public partial class BucketDestroyerWindow
{
    public BucketDestroyerWindow(BucketDestroyerContext context)
    {
        InitializeComponent();
        DataContext = context;
    }

    public static async Task<BucketDestroyerWindow> CreateInstanceAndShow()
    {
        var dataContext = await BucketDestroyerContext.CreateInstance(null);

        await ThreadSwitcher.ResumeForegroundAsync();

        var window = new BucketDestroyerWindow(dataContext);

        window.PositionWindowAndShow();

        return window;
    }
}