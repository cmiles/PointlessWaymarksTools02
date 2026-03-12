using CommunityToolkit.Mvvm.Messaging.Messages;

namespace PointlessWaymarks.WpfCommon.AppMessages;

public class FeatureIntersectFileAddRequestMessage((object sender, List<string> files) message)
    : ValueChangedMessage<(object sender, List<string> files)>(message);