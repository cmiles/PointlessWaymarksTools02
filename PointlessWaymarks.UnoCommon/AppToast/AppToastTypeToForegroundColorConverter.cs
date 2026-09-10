using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace PointlessWaymarks.UnoCommon.AppToast;

public class AppToastTypeToForegroundColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language)
    {
        if (value is ToastType messageType)
            return messageType switch
            {
                ToastType.Success => new SolidColorBrush(Colors.White),
                ToastType.Error => new SolidColorBrush(Colors.White),
                ToastType.Info => new SolidColorBrush(Colors.White),
                ToastType.Warning => new SolidColorBrush(Colors.White),
                _ => new SolidColorBrush(Colors.Black)
            };

        return new SolidColorBrush(Colors.Black);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, string language)
    {
        throw new NotSupportedException();
    }
}
