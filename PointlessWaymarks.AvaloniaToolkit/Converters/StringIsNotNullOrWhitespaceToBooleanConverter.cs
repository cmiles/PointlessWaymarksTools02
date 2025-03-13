using System.Globalization;
using Avalonia.Data.Converters;

namespace PointlessWaymarks.AvaloniaToolkit.Converters;

public class StringIsNotNullOrWhitespaceToBooleanConverter : IValueConverter
{
    public static readonly StringIsNotNullOrWhitespaceToBooleanConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter,
        CultureInfo culture)
    {
        if (value == null) return false;
        if (value is string stringValue) return !string.IsNullOrWhiteSpace(stringValue);
        if (string.IsNullOrWhiteSpace(value.ToString())) return false;
        return false;
    }

    public object ConvertBack(object? value, Type targetType,
        object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}