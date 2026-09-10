using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PointlessWaymarks.UnoCommon.AppToast;

public sealed partial class AppToastControl : UserControl
{
    public AppToastControl()
    {
        InitializeComponent();
    }

    private void OnCloseToastClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AppToastMessage message } && DataContext is AppToastContext context)
        {
            _ = context.DisposeToast(message);
        }
    }
}
