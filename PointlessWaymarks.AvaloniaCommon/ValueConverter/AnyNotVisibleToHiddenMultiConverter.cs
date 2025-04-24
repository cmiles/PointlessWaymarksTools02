using System.Globalization;

namespace PointlessWaymarks.AvaloniaCommon.ValueConverter;

public class AnyNotVisibleToHiddenMultiConverter
{
    public object Convert(object[]? values, Type? targetType, object? parameter, CultureInfo culture)
    {
        if (values == null || values.Length == 0) return false;

        foreach (var value in values)
            if (value is bool visibility && visibility != false)
                return false;

        return true;
    }
}