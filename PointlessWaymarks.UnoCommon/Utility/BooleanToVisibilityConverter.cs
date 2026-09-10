using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace PointlessWaymarks.UnoCommon.Utility;

public class BooleanToVisibilityConverter : IValueConverter
{
    public bool Inverse { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, string language)
    {
        var boolValue = value is bool b && b;
        if (Inverse) boolValue = !boolValue;
        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, string language)
    {
        var isVisible = value is Visibility v && v == Visibility.Visible;
        return Inverse ? !isVisible : isVisible;
    }
}
