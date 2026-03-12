using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.WpfCommon.FeatureIntersectTagger;

[NotifyPropertyChanged]
public partial class FeatureIntersectTaggerWindow
{
    public FeatureIntersectTaggerWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public required FeatureIntersectTaggerContext FeatureIntersectTaggerContent { get; set; }
    public required StatusControlContext StatusContext { get; set; }
    public string WindowTitle { get; set; } = "Feature Intersect Tagger";

    public static async Task<FeatureIntersectTaggerWindow> CreateInstance(List<string>? initialFilesToTag = null)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var statusContext = await StatusControlContext.CreateInstance();
        var context = await FeatureIntersectTaggerContext.CreateInstance(statusContext, null);

        var window = new FeatureIntersectTaggerWindow
        {
            StatusContext = statusContext,
            FeatureIntersectTaggerContent = context
        };

        window.PositionWindowAndShow();

        if (initialFilesToTag is { Count: > 0 } && context.FilesToTagFileList != null)
            statusContext.RunFireAndForgetNonBlockingTask(async () =>
            {
                await context.FilesToTagFileList.AddFilesToTag(initialFilesToTag);
            });

        return window;
    }
}