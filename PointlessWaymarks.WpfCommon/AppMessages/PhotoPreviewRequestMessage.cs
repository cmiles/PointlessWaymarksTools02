using CommunityToolkit.Mvvm.Messaging.Messages;

namespace PointlessWaymarks.WpfCommon.AppMessages;

/// <summary>
///     Sent to request the Photo Preview Window to display a preview of the specified file.
///     The window is responsible for generating the preview image.
/// </summary>
public class PhotoPreviewRequestMessage(PhotoPreviewRequestData data)
    : ValueChangedMessage<PhotoPreviewRequestData>(data);

public record PhotoPreviewRequestData(
    string FullFilePath, string DisplayTitle, int Rating,
    List<string>? UpcomingFilePaths = null);
