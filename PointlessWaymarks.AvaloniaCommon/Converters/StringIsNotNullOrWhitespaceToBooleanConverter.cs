using System.Globalization;
using Avalonia.Data.Converters;

namespace PointlessWaymarks.AvaloniaCommon.Converters;

public class StringIsNotNullOrWhitespaceToBooleanConverter : IValueConverter
{
    public static readonly StringIsNotNullOrWhitespaceToBooleanConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter,
        CultureInfo culture)
    {
        switch (value)
        {
            case null:
                return false;
            case string stringValue:
                return !string.IsNullOrWhiteSpace(stringValue);
        }

        return string.IsNullOrWhiteSpace(value.ToString()) && false;
    }

    public object ConvertBack(object? value, Type targetType,
        object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}