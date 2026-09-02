using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.WpfCommon.AppMessages;
using Serilog;
using TinyIpc.Messaging;

namespace PointlessWaymarks.WpfCommon.PhotoPreview;

public class PhotoPreviewIpcChannel : IDisposable, IAsyncDisposable
{
    private readonly TinyMessageBus _messageBus;
    private readonly WorkQueue<string> _sendQueue;
    private bool _disposed;
    private bool _boundToMessenger;

    public Guid InstanceId { get; }
    public string ChannelName { get; }

    public event EventHandler<PhotoPreviewRequestIpcDto>? PreviewRequestReceived;
    public event EventHandler<PhotoPreviewClearIpcDto>? ClearPreviewReceived;
    public event EventHandler<PhotoItemRatingChangedIpcDto>? RatingChangedReceived;
    public event EventHandler<PhotoPreviewNavigateIpcDto>? NavigateReceived;
    public event EventHandler<PhotoPreviewFilterUnratedIpcDto>? FilterUnratedReceived;
    public event EventHandler<PhotoPreviewCloseIpcDto>? CloseReceived;

    public PhotoPreviewIpcChannel(string channelId, Guid? instanceId = null)
    {
        InstanceId = instanceId ?? Guid.NewGuid();
        ChannelName = BuildChannelName(channelId);
        _messageBus = new TinyMessageBus(ChannelName);

        _sendQueue = new WorkQueue<string>
        {
            Processor = async msg =>
            {
                try
                {
                    if (!_disposed)
                    {
                        await _messageBus.PublishAsync(BinaryData.FromString(msg)).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    Log.ForContext("Channel", ChannelName).Error(ex, "Error publishing IPC message");
                }
            }
        };

        _messageBus.MessageReceived += OnMessageReceived;
    }

    public static string BuildChannelName(string channelId)
    {
        var cleaned = channelId.Trim();
        if (cleaned.StartsWith("PointlessWaymarks.PhotoPreview.", StringComparison.OrdinalIgnoreCase))
            return cleaned;
        return $"PointlessWaymarks.PhotoPreview.{cleaned}";
    }

    public void BindToMessenger()
    {
        if (_boundToMessenger) return;
        _boundToMessenger = true;

        WeakReferenceMessenger.Default.Register<PhotoPreviewNextItemMessage>(this,
            (_, _) => PublishNavigate("Next"));
        WeakReferenceMessenger.Default.Register<PhotoPreviewPreviousItemMessage>(this,
            (_, _) => PublishNavigate("Previous"));
        WeakReferenceMessenger.Default.Register<PhotoItemRatingChangedMessage>(this,
            (_, m) =>
            {
                if (m.Value.SenderId != InstanceId)
                {
                    PublishRatingChanged(m.Value.FullFilePath, m.Value.Rating);
                }
            });
        WeakReferenceMessenger.Default.Register<PhotoPreviewFilterUnratedMessage>(this,
            (_, m) => PublishFilterUnrated(m.Value));

        PreviewRequestReceived += (_, dto) =>
        {
            WeakReferenceMessenger.Default.Send(
                new PhotoPreviewRequestMessage(new PhotoPreviewRequestData(dto.FilePath, dto.Title, dto.Rating,
                    dto.UpcomingFilePaths)));
        };

        ClearPreviewReceived += (_, _) =>
        {
            WeakReferenceMessenger.Default.Send(new PhotoPreviewClearMessage());
        };

        RatingChangedReceived += (_, dto) =>
        {
            WeakReferenceMessenger.Default.Send(
                new PhotoItemRatingChangedMessage(new PhotoItemRatingChangedData(dto.FilePath, dto.Rating,
                    dto.SenderId)));
        };

        FilterUnratedReceived += (_, dto) =>
        {
            WeakReferenceMessenger.Default.Send(
                new PhotoPreviewFilterUnratedMessage(dto.FilterUnratedOnly));
        };
    }

    private void OnMessageReceived(object? sender, TinyMessageReceivedEventArgs e)
    {
        try
        {
            var rawJson = e.Message.ToString();
            if (string.IsNullOrWhiteSpace(rawJson)) return;

            var envelope = JsonSerializer.Deserialize<PhotoPreviewIpcEnvelope>(rawJson);
            if (envelope == null || envelope.SenderId == InstanceId) return;

            switch (envelope.MessageType)
            {
                case PhotoPreviewIpcMessageType.PreviewRequest:
                    var previewDto = envelope.DeserializePayload<PhotoPreviewRequestIpcDto>();
                    if (previewDto != null) PreviewRequestReceived?.Invoke(this, previewDto);
                    break;
                case PhotoPreviewIpcMessageType.ClearPreview:
                    var clearDto = envelope.DeserializePayload<PhotoPreviewClearIpcDto>();
                    if (clearDto != null) ClearPreviewReceived?.Invoke(this, clearDto);
                    break;
                case PhotoPreviewIpcMessageType.RatingChanged:
                    var ratingDto = envelope.DeserializePayload<PhotoItemRatingChangedIpcDto>();
                    if (ratingDto != null) RatingChangedReceived?.Invoke(this, ratingDto);
                    break;
                case PhotoPreviewIpcMessageType.Navigate:
                    var navDto = envelope.DeserializePayload<PhotoPreviewNavigateIpcDto>();
                    if (navDto != null) NavigateReceived?.Invoke(this, navDto);
                    break;
                case PhotoPreviewIpcMessageType.FilterUnrated:
                    var filterDto = envelope.DeserializePayload<PhotoPreviewFilterUnratedIpcDto>();
                    if (filterDto != null) FilterUnratedReceived?.Invoke(this, filterDto);
                    break;
                case PhotoPreviewIpcMessageType.Close:
                    var closeDto = envelope.DeserializePayload<PhotoPreviewCloseIpcDto>();
                    if (closeDto != null) CloseReceived?.Invoke(this, closeDto);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.ForContext("Channel", ChannelName).Error(ex, "Error handling incoming IPC message");
        }
    }

    public void Publish<T>(PhotoPreviewIpcMessageType type, T data)
    {
        if (_disposed) return;
        var envelope = PhotoPreviewIpcEnvelope.Create(type, data, InstanceId);
        var json = JsonSerializer.Serialize(envelope);
        _sendQueue.Enqueue(json);
    }

    public void PublishPreviewRequest(string filePath, string title, int rating, List<string>? upcomingFilePaths = null)
    {
        Publish(PhotoPreviewIpcMessageType.PreviewRequest,
            new PhotoPreviewRequestIpcDto(filePath, title, rating, upcomingFilePaths));
    }

    public void PublishClearPreview()
    {
        Publish(PhotoPreviewIpcMessageType.ClearPreview, new PhotoPreviewClearIpcDto(DateTime.UtcNow));
    }

    public void PublishRatingChanged(string filePath, int rating)
    {
        Publish(PhotoPreviewIpcMessageType.RatingChanged,
            new PhotoItemRatingChangedIpcDto(filePath, rating, InstanceId));
    }

    public void PublishNavigate(string direction)
    {
        Publish(PhotoPreviewIpcMessageType.Navigate, new PhotoPreviewNavigateIpcDto(direction));
    }

    public void PublishFilterUnrated(bool filterUnratedOnly)
    {
        Publish(PhotoPreviewIpcMessageType.FilterUnrated, new PhotoPreviewFilterUnratedIpcDto(filterUnratedOnly));
    }

    public void PublishClose()
    {
        Publish(PhotoPreviewIpcMessageType.Close, new PhotoPreviewCloseIpcDto(DateTime.UtcNow));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_boundToMessenger)
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }
        _messageBus.MessageReceived -= OnMessageReceived;
        _messageBus.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (_boundToMessenger)
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }
        _messageBus.MessageReceived -= OnMessageReceived;
        _messageBus.Dispose();
        GC.SuppressFinalize(this);
        await Task.CompletedTask;
    }
}
