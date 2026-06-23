using System.Dynamic;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon;
using PointlessWaymarks.WpfCommon.Status;

namespace PointlessWaymarks.WpfCommon.PhotoPreview;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class RatingChangeNotification
{
    public string Message { get; set; } = string.Empty;
    public bool HasError { get; set; }
    public required StatusControlContext StatusContext { get; init; }
    public required PhotoPreviewContext ParentContext { get; init; }

    public static RatingChangeNotification CreateInstance(PhotoPreviewContext previewContext, string message)
    {
        var toReturn = new RatingChangeNotification
        {
            Message = message,
            StatusContext = previewContext.StatusContext,
            ParentContext = previewContext
        };
        
        toReturn.BuildCommands();

        return toReturn;
    }
    
    [NonBlockingCommand]
    public async Task Dismiss()
    {
        await ThreadSwitcher.ResumeForegroundAsync();
        ParentContext.RatingNotifications.Remove(this);
    }
}
