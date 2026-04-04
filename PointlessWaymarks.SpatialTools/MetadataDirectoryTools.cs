using MetadataExtractor;
using Directory = MetadataExtractor.Directory;

namespace PointlessWaymarks.SpatialTools;

/// <summary>
///     Extension methods for working with lists of MetadataExtractor directories,
///     allowing retrieval of the first matching value across multiple directories.
/// </summary>
public static class MetadataDirectoryTools
{
    /// <summary>
    ///     Checks if any directory in the collection contains the specified tag.
    /// </summary>
    public static bool ContainsTag(this IEnumerable<Directory> directories, int tagType)
    {
        foreach (var directory in directories)
            if (directory.ContainsTag(tagType))
                return true;

        return false;
    }

    /// <summary>
    ///     Gets the description for a tag from the first directory that contains it.
    /// </summary>
    public static string? GetDescription(this IEnumerable<Directory> directories, int tagType)
    {
        foreach (var directory in directories)
        {
            var description = directory.GetDescription(tagType);
            if (!string.IsNullOrEmpty(description))
                return description;
        }

        return null;
    }

    /// <summary>
    ///     Gets the object value for a tag from the first directory that contains it.
    /// </summary>
    public static object? GetObject(this IEnumerable<Directory> directories, int tagType)
    {
        foreach (var directory in directories)
        {
            var value = directory.GetObject(tagType);
            if (value != null)
                return value;
        }

        return null;
    }

    /// <summary>
    ///     Tries to get a Boolean value for a tag from the first directory that contains it.
    /// </summary>
    public static bool TryGetBoolean(this IEnumerable<Directory> directories, int tagType,
        out bool value)
    {
        foreach (var directory in directories)
            if (directory.TryGetBoolean(tagType, out value))
                return true;

        value = default;
        return false;
    }

    /// <summary>
    ///     Tries to get a byte array value for a tag from the first directory that contains it.
    /// </summary>
    public static bool TryGetByteArray(this IEnumerable<Directory> directories, int tagType,
        out byte[]? value)
    {
        foreach (var directory in directories)
        {
            value = directory.GetByteArray(tagType);
            if (value != null)
                return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    ///     Tries to get a Double value for a tag from the first directory that contains it.
    /// </summary>
    public static bool TryGetDouble(this IEnumerable<Directory> directories, int tagType,
        out double value)
    {
        foreach (var directory in directories)
            if (directory.TryGetDouble(tagType, out value))
                return true;

        value = default;
        return false;
    }

    /// <summary>
    ///     Tries to get an Int32 value for a tag from the first directory that contains it.
    /// </summary>
    public static bool TryGetInt32(this IEnumerable<Directory> directories, int tagType,
        out int value)
    {
        foreach (var directory in directories)
            if (directory.TryGetInt32(tagType, out value))
                return true;

        value = default;
        return false;
    }

    /// <summary>
    ///     Tries to get an Int64 value for a tag from the first directory that contains it.
    /// </summary>
    public static bool TryGetInt64(this IEnumerable<Directory> directories, int tagType,
        out long value)
    {
        foreach (var directory in directories)
            if (directory.TryGetInt64(tagType, out value))
                return true;

        value = default;
        return false;
    }

    /// <summary>
    ///     Tries to get a Rational value for a tag from the first directory that contains it.
    /// </summary>
    public static bool TryGetRational(this IEnumerable<Directory> directories, int tagType,
        out Rational value)
    {
        foreach (var directory in directories)
            if (directory.TryGetRational(tagType, out value))
                return true;

        value = default;
        return false;
    }

    /// <summary>
    ///     Tries to get a Single (float) value for a tag from the first directory that contains it.
    /// </summary>
    public static bool TryGetSingle(this IEnumerable<Directory> directories, int tagType,
        out float value)
    {
        foreach (var directory in directories)
            if (directory.TryGetSingle(tagType, out value))
                return true;

        value = default;
        return false;
    }

    /// <summary>
    ///     Tries to get a UInt16 value for a tag from the first directory that contains it.
    /// </summary>
    public static bool TryGetUInt16(this IEnumerable<Directory> directories, int tagType,
        out ushort value)
    {
        foreach (var directory in directories)
            if (directory.TryGetUInt16(tagType, out value))
                return true;

        value = default;
        return false;
    }

    /// <summary>
    ///     Tries to get a UInt32 value for a tag from the first directory that contains it.
    /// </summary>
    public static bool TryGetUInt32(this IEnumerable<Directory> directories, int tagType,
        out uint value)
    {
        foreach (var directory in directories)
            if (directory.TryGetUInt32(tagType, out value))
                return true;

        value = default;
        return false;
    }
}