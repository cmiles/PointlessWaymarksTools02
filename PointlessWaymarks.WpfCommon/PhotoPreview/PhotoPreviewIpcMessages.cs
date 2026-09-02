using System.Text.Json;
using System.Text.Json.Serialization;

namespace PointlessWaymarks.WpfCommon.PhotoPreview;

public enum PhotoPreviewIpcMessageType
{
    PreviewRequest,
    ClearPreview,
    RatingChanged,
    Navigate,
    FilterUnrated,
    Close
}

public class PhotoPreviewIpcEnvelope
{
    public PhotoPreviewIpcMessageType MessageType { get; set; }
    public string Payload { get; set; } = string.Empty;
    public Guid SenderId { get; set; } = Guid.NewGuid();

    public static PhotoPreviewIpcEnvelope Create<T>(PhotoPreviewIpcMessageType type, T data, Guid? senderId = null)
    {
        return new PhotoPreviewIpcEnvelope
        {
            MessageType = type,
            Payload = JsonSerializer.Serialize(data),
            SenderId = senderId ?? Guid.NewGuid()
        };
    }

    public T? DeserializePayload<T>()
    {
        if (string.IsNullOrWhiteSpace(Payload)) return default;
        return JsonSerializer.Deserialize<T>(Payload);
    }
}

public record PhotoPreviewRequestIpcDto(
    string FilePath,
    string Title,
    int Rating,
    List<string>? UpcomingFilePaths = null
);

public record PhotoItemRatingChangedIpcDto(
    string FilePath,
    int Rating,
    Guid SenderId
);

public record PhotoPreviewNavigateIpcDto(
    string Direction
);

public record PhotoPreviewFilterUnratedIpcDto(
    bool FilterUnratedOnly
);

public record PhotoPreviewClearIpcDto(
    DateTime Timestamp
);

public record PhotoPreviewCloseIpcDto(
    DateTime Timestamp
);
