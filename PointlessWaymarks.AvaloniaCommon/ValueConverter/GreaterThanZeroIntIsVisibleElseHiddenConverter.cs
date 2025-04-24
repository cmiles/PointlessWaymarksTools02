using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace PointlessWaymarks.AvaloniaCommon.ValueConverter;

public class GreaterThanZeroIntIsVisibleElseHiddenConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value as int?) switch
        {
            null => false,
            < 1 => false,
            _ => true
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}