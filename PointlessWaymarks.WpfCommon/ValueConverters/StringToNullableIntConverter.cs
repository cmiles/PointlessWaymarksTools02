using System.Globalization;
using System.Windows.Data;

namespace PointlessWaymarks.WpfCommon.ValueConverters;

public sealed class StringToNullableIntConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string stringValue)
            return null;

        if (string.IsNullOrWhiteSpace(stringValue))
            return null;

        if (int.TryParse(stringValue, NumberStyles.Integer, culture, out var result))
            return result;

        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
            return string.Empty;

        if (value is int intValue)
            return intValue.ToString(culture);

        return string.Empty;
    }
}