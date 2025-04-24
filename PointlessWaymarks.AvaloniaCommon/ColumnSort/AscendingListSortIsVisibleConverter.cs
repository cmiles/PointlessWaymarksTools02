using System.ComponentModel;
using System.Globalization;
using Avalonia.Data.Converters;

namespace PointlessWaymarks.AvaloniaCommon.ColumnSort;

public sealed class AscendingListSortIsVisibleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            null => false,
            ListSortDirection v => v == ListSortDirection.Ascending,
            _ => false
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
} 