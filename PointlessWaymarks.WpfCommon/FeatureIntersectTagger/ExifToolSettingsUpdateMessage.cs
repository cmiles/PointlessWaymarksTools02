using CommunityToolkit.Mvvm.Messaging.Messages;

namespace PointlessWaymarks.WpfCommon.FeatureIntersectTagger;

public class ExifToolSettingsUpdateMessage((object sender, string exifToolFullName) message)
    : ValueChangedMessage<(object sender, string exifToolFullName)>(message);
