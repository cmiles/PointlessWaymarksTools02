using CommunityToolkit.Mvvm.Messaging.Messages;

namespace PointlessWaymarks.WpfCommon.AppMessages;

public class FileMetadataLocationUpdateMessage((object sender, List<string> writtenFiles) message)
    : ValueChangedMessage<(object sender, List<string> writtenFiles)>(message);