using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PointlessWaymarks.WpfCommon.ValueConverters;

public sealed class IntegerGreaterThanZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return Visibility.Collapsed;

        // Handle integer types directly
        if (value is int intValue)
            return intValue > 0 ? Visibility.Visible : Visibility.Collapsed;

        // Handle collection Count property or any numeric type
        if (value is IConvertible convertible)
            try
            {
                var numericValue = System.Convert.ToInt32(convertible);
                return numericValue > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch
            {
                // Failed to convert to integer
                return Visibility.Collapsed;
            }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Convert back isn't typically used for this converter, but we'll implement it anyway
        if (value is Visibility visibility)
            return visibility == Visibility.Visible ? 1 : 0;

        return 0;
    }
}