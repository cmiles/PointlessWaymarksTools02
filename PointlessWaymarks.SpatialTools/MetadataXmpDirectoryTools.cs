using MetadataExtractor.Formats.Xmp;
using XmpCore;

namespace PointlessWaymarks.SpatialTools;

/// <summary>
///     Extension methods for working with lists of XmpDirectory,
///     allowing retrieval of the first matching XMP value across multiple directories.
/// </summary>
public static class MetadataXmpDirectoryTools
{
    /// <summary>
    ///     Gets a property value from the first XmpDirectory that contains it.
    /// </summary>
    public static string? GetPropertyValue(this IEnumerable<XmpDirectory> directories, string schemaNs, string propName)
    {
        foreach (var directory in directories)
        {
            var value = directory.XmpMeta?.GetProperty(schemaNs, propName)?.Value;
            if (!string.IsNullOrEmpty(value))
                return value;
        }

        return null;
    }

    /// <summary>
    ///     Gets a property from the first XmpDirectory that contains it.
    /// </summary>
    public static IXmpProperty? GetProperty(this IEnumerable<XmpDirectory> directories, string schemaNs,
        string propName)
    {
        foreach (var directory in directories)
        {
            var prop = directory.XmpMeta?.GetProperty(schemaNs, propName);
            if (prop != null)
                return prop;
        }

        return null;
    }

    /// <summary>
    ///     Gets an integer property value from the first XmpDirectory that contains it.
    /// </summary>
    public static int? GetPropertyInteger(this IEnumerable<XmpDirectory> directories, string schemaNs, string propName)
    {
        foreach (var directory in directories)
        {
            var xmpMeta = directory.XmpMeta;
            if (xmpMeta?.DoesPropertyExist(schemaNs, propName) == true)
                return xmpMeta.GetPropertyInteger(schemaNs, propName);
        }

        return null;
    }

    /// <summary>
    ///     Gets all XMP properties from all directories as a list of name/value pairs.
    /// </summary>
    public static List<(string Path, string Value)> GetXmpProperties(this IEnumerable<XmpDirectory> directories)
    {
        var result = new List<(string Path, string Value)>();

        foreach (var directory in directories)
        {
            var properties = directory.GetXmpProperties();

            foreach (var property in properties)
                result.Add((property.Key, property.Value));
        }

        return result;
    }


    /// <summary>
    ///     Gets an array item value from the first XmpDirectory that contains it.
    /// </summary>
    public static string? GetArrayItemValue(this IEnumerable<XmpDirectory> directories, string schemaNs,
        string arrayName, int itemIndex)
    {
        foreach (var directory in directories)
        {
            var value = directory.XmpMeta?.GetArrayItem(schemaNs, arrayName, itemIndex)?.Value;
            if (!string.IsNullOrEmpty(value))
                return value;
        }

        return null;
    }

    /// <summary>
    ///     Gets an array item from the first XmpDirectory that contains it.
    /// </summary>
    public static IXmpProperty? GetArrayItem(this IEnumerable<XmpDirectory> directories, string schemaNs,
        string arrayName, int itemIndex)
    {
        foreach (var directory in directories)
        {
            var item = directory.XmpMeta?.GetArrayItem(schemaNs, arrayName, itemIndex);
            if (item != null)
                return item;
        }

        return null;
    }

    /// <summary>
    ///     Gets a struct field value from the first XmpDirectory that contains it.
    /// </summary>
    public static string? GetStructFieldValue(this IEnumerable<XmpDirectory> directories, string schemaNs,
        string structName, string fieldNs, string fieldName)
    {
        foreach (var directory in directories)
        {
            var value = directory.XmpMeta?.GetStructField(schemaNs, structName, fieldNs, fieldName)?.Value;
            if (!string.IsNullOrEmpty(value))
                return value;
        }

        return null;
    }

    /// <summary>
    ///     Gets a struct field from the first XmpDirectory that contains it.
    /// </summary>
    public static IXmpProperty? GetStructField(this IEnumerable<XmpDirectory> directories, string schemaNs,
        string structName, string fieldNs, string fieldName)
    {
        foreach (var directory in directories)
        {
            var field = directory.XmpMeta?.GetStructField(schemaNs, structName, fieldNs, fieldName);
            if (field != null)
                return field;
        }

        return null;
    }

    /// <summary>
    ///     Gets a qualifier value from the first XmpDirectory that contains it.
    /// </summary>
    public static string? GetQualifierValue(this IEnumerable<XmpDirectory> directories, string schemaNs,
        string propName, string qualNs, string qualName)
    {
        foreach (var directory in directories)
        {
            var value = directory.XmpMeta?.GetQualifier(schemaNs, propName, qualNs, qualName)?.Value;
            if (!string.IsNullOrEmpty(value))
                return value;
        }

        return null;
    }

    /// <summary>
    ///     Gets a qualifier from the first XmpDirectory that contains it.
    /// </summary>
    public static IXmpProperty? GetQualifier(this IEnumerable<XmpDirectory> directories, string schemaNs,
        string propName, string qualNs, string qualName)
    {
        foreach (var directory in directories)
        {
            var qualifier = directory.XmpMeta?.GetQualifier(schemaNs, propName, qualNs, qualName);
            if (qualifier != null)
                return qualifier;
        }

        return null;
    }

    /// <summary>
    ///     Checks if any XmpDirectory contains the specified property.
    /// </summary>
    public static bool DoesPropertyExist(this IEnumerable<XmpDirectory> directories, string schemaNs, string propName)
    {
        foreach (var directory in directories)
            if (directory.XmpMeta?.DoesPropertyExist(schemaNs, propName) == true)
                return true;

        return false;
    }

    /// <summary>
    ///     Checks if any XmpDirectory contains the specified array item.
    /// </summary>
    public static bool DoesArrayItemExist(this IEnumerable<XmpDirectory> directories, string schemaNs,
        string arrayName, int itemIndex)
    {
        foreach (var directory in directories)
            if (directory.XmpMeta?.DoesArrayItemExist(schemaNs, arrayName, itemIndex) == true)
                return true;

        return false;
    }

    /// <summary>
    ///     Checks if any XmpDirectory contains the specified struct field.
    /// </summary>
    public static bool DoesStructFieldExist(this IEnumerable<XmpDirectory> directories, string schemaNs,
        string structName, string fieldNs, string fieldName)
    {
        foreach (var directory in directories)
            if (directory.XmpMeta?.DoesStructFieldExist(schemaNs, structName, fieldNs, fieldName) == true)
                return true;

        return false;
    }

    /// <summary>
    ///     Checks if any XmpDirectory contains the specified qualifier.
    /// </summary>
    public static bool DoesQualifierExist(this IEnumerable<XmpDirectory> directories, string schemaNs,
        string propName, string qualNs, string qualName)
    {
        foreach (var directory in directories)
            if (directory.XmpMeta?.DoesQualifierExist(schemaNs, propName, qualNs, qualName) == true)
                return true;

        return false;
    }

    /// <summary>
    ///     Counts array items from the first XmpDirectory that has the array.
    /// </summary>
    public static int CountArrayItems(this IEnumerable<XmpDirectory> directories, string schemaNs, string arrayName)
    {
        foreach (var directory in directories)
        {
            var count = directory.XmpMeta?.CountArrayItems(schemaNs, arrayName) ?? 0;
            if (count > 0)
                return count;
        }

        return 0;
    }

    /// <summary>
    ///     Gets the first non-null XmpMeta from the directories.
    /// </summary>
    public static IXmpMeta? GetFirstXmpMeta(this IEnumerable<XmpDirectory> directories)
    {
        foreach (var directory in directories)
            if (directory.XmpMeta != null)
                return directory.XmpMeta;

        return null;
    }
}
