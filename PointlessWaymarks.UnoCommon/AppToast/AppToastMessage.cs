using CommunityToolkit.Mvvm.ComponentModel;

namespace PointlessWaymarks.UnoCommon.AppToast;

public partial class AppToastMessage : ObservableObject
{
    [ObservableProperty] private bool _userMustDismiss;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private DateTime _addedOn;
    [ObservableProperty] private ToastType _messageType;
}
