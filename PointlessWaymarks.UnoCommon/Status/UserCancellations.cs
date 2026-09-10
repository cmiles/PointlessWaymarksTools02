using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PointlessWaymarks.UnoCommon.Status;

public partial class UserCancellations : ObservableObject
{
    public UserCancellations()
    {
        Cancel = new RelayCommand(() =>
        {
            CancelSource?.Cancel();
            IsEnabled = false;
            Description = "Canceling...";
        });
    }

    [ObservableProperty] private RelayCommand? _cancel;
    [ObservableProperty] private CancellationTokenSource? _cancelSource;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private bool _isEnabled = true;
}
