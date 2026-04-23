using CommunityToolkit.Mvvm.Messaging.Messages;

namespace PointlessWaymarks.WpfCommon.AppMessages;

/// <summary>
///     Sent by the Photo Preview Window to request the host navigate to the next item.
/// </summary>
public class PhotoPreviewNextItemMessage() : ValueChangedMessage<object>(new());

/// <summary>
///     Sent by the Photo Preview Window to request the host navigate to the previous item.
/// </summary>
public class PhotoPreviewPreviousItemMessage() : ValueChangedMessage<object>(new());

/// <summary>
///     Sent by either the Photo Preview Window or the photo list to notify
///     that a photo group's rating has changed. Contains the primary file
///     path so each side can match the item and the new rating.
/// </summary>
public class PhotoItemRatingChangedMessage(PhotoItemRatingChangedData data)
    : ValueChangedMessage<PhotoItemRatingChangedData>(data);

public record PhotoItemRatingChangedData(string FullFilePath, int Rating);

/// <summary>
///     Sent by the Photo Preview Window to inform the host that the
///     "show only unrated" filter has been toggled.
/// </summary>
public class PhotoPreviewFilterUnratedMessage(bool filterUnratedOnly)
    : ValueChangedMessage<bool>(filterUnratedOnly);

/// <summary>
///     Sent by the host to tell the Photo Preview Window that there is
///     nothing to preview (e.g. the list is empty or has no visible items).
/// </summary>
public class PhotoPreviewClearMessage() : ValueChangedMessage<object>(new());
