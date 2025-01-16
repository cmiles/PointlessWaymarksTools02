using System.Globalization;
using System.Windows.Data;
using PointlessWaymarks.CommonTools;

namespace PointlessWaymarks.WpfCommon.ValueConverters;

public sealed class FileSizeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        switch (value)
        {
            case null:
                return null;
            case long standardFileSize:
                return FileAndFolderTools.GetBytesReadable(standardFileSize);
        }

        return long.TryParse(value.ToString(), out var stringParsedElevation) ? FileAndFolderTools.GetBytesReadable(stringParsedElevation) : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}