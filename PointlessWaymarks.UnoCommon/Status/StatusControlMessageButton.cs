using CommunityToolkit.Mvvm.ComponentModel;

namespace PointlessWaymarks.UnoCommon.Status;

public partial class StatusControlMessageButton : ObservableObject
{
    [ObservableProperty] private bool _isDefault;
    [ObservableProperty] private string _messageText = string.Empty;
}
