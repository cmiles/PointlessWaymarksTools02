using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace PointlessWaymarks.AvaloniaCommon.ValueConverter;

public class NullOrWhiteSpaceStringToHiddenVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (string.IsNullOrWhiteSpace(value?.ToString())) return false;

        return true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}