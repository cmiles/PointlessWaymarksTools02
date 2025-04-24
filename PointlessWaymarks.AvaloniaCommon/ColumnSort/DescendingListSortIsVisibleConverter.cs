using System.ComponentModel;
using System.Globalization;
using Avalonia.Data.Converters;

namespace PointlessWaymarks.AvaloniaCommon.ColumnSort;

public sealed class DescendingListSortIsVisibleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            null => false,
            ListSortDirection v => v == ListSortDirection.Descending,
            _ => false
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
} 