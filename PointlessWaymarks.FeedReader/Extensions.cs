using System.Net;
using System.Text;
using System.Xml.Linq;

namespace PointlessWaymarks.FeedReader;

/// <summary>
///     Extension methods
/// </summary>
internal static class Extensions
{
    /// <summary>
    ///     Decodes a html encoded string
    /// </summary>
    /// <param name="text">html text</param>
    /// <returns>decoded html</returns>
    public static string? HtmlDecode(this string? text)
    {
        return WebUtility.HtmlDecode(text);
    }

    /// <summary>
    ///     Determines whether this string and another string object have the same value.
    /// </summary>
    /// <param name="text">the string</param>
    /// <param name="compareTo">the string to compare to</param>
    /// <returns></returns>
    public static bool EqualsIgnoreCase(this string? text, string? compareTo)
    {
        if (text == null)
            return compareTo == null;
        return text.Equals(compareTo, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Determines whether this string equals one of the given strings.
    /// </summary>
    /// <param name="text">the string</param>
    /// <param name="compareTo">all strings to compare to</param>
    /// <returns></returns>
    public static bool EqualsIgnoreCase(this string? text, params string?[] compareTo)
    {
        foreach (var value in compareTo)
            if (text.EqualsIgnoreCase(value))
                return true;

        return false;
    }

    /// <summary>
    ///     Converts a string to UTF-8
    /// </summary>
    /// <param name="text">text to convert</param>
    /// <returns>text as utf8 encoded string</returns>
    public static string ToUtf8(this string text)
    {
        return Encoding.UTF8.GetString(Encoding.GetEncoding(0).GetBytes(text));
    }

    /// <summary>
    ///     Converts a string to UTF-8
    /// </summary>
    /// <param name="text">text to convert</param>
    /// <param name="encoding">the encoding of the text</param>
    /// <returns>text as utf8 encoded string</returns>
    public static string ToUtf8(this string text, Encoding encoding)
    {
        if (Equals(encoding, Encoding.UTF8))
            return text;

        if (Equals(encoding, Encoding.GetEncoding(0)))
            return text;

        var utf = Encoding.UTF8;
        var convertedBytes = Encoding.Convert(encoding, utf, encoding.GetBytes(text));
        return Encoding.UTF8.GetString(convertedBytes);
    }

    /// <summary>
    ///     Gets the value of a xml element encoded as utf8
    /// </summary>
    /// <param name="element">the xml element</param>
    /// <returns>value of the element utf8 encoded</returns>
    public static string? GetValue(this XElement? element)
    {
        return element?.Value;
    }

    /// <summary>
    ///     Gets the value of the element "name"
    /// </summary>
    /// <param name="element">xml element</param>
    /// <param name="name">name of the element</param>
    /// <returns>the value of the XElement</returns>
    public static string? GetValue(this XElement? element, string name)
    {
        return element?.GetElement(name).GetValue();
    }

    /// <summary>
    ///     Gets the value of the element "name"
    /// </summary>
    /// <param name="element">xml element</param>
    /// <param name="namespacePrefix">the namespace prefix of the element that should be returned</param>
    /// <param name="name">name of the element</param>
    /// <returns>the value of the XElement</returns>
    public static string? GetValue(this XElement? element, string? namespacePrefix, string name)
    {
        return element?.GetElement(namespacePrefix, name).GetValue();
    }

    /// <summary>
    ///     Gets the value of the given attribute
    /// </summary>
    /// <param name="attribute">the xml attribute</param>
    /// <returns>value</returns>
    public static string? GetValue(this XAttribute? attribute)
    {
        return attribute?.Value;
    }

    /// <summary>
    ///     Gets the value of the attribute <paramref name="name" />
    /// </summary>
    /// <param name="element">the xml element</param>
    /// <param name="name">the name of the attribute</param>
    /// <returns>value of the attribute</returns>
    public static string? GetAttributeValue(this XElement? element, string name)
    {
        return element.GetAttribute(name)?.GetValue();
    }

    /// <summary>
    ///     Gets the attribute <paramref name="name" /> of the given XElement
    /// </summary>
    /// <param name="element">the xml element</param>
    /// <param name="name">the name of the attribute</param>
    /// <returns>the xml attribute</returns>
    public static XAttribute? GetAttribute(this XElement? element, string name)
    {
        var splitName = SplitName(name);
        return element?.GetAttribute(splitName.Item1, splitName.Item2);
    }

    /// <summary>
    ///     Gets the attribute with the namespace <paramref name="namespacePrefix" /> and name <paramref name="name" /> of the
    ///     given XElement
    /// </summary>
    /// <param name="element">the xml element</param>
    /// <param name="namespacePrefix">the namespace prefix of the attribute</param>
    /// <param name="name">the name of the attribute</param>
    /// <returns>the xml attribute</returns>
    public static XAttribute? GetAttribute(this XElement element, string? namespacePrefix, string? name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        if (string.IsNullOrEmpty(namespacePrefix))
            return element.Attribute(name);

        var xNamespace = element.GetNamespacePrefix(namespacePrefix);

        return element.Attribute((xNamespace ?? string.Empty) + name);
    }

    /// <summary>
    ///     Gets the element of the given XElement
    /// </summary>
    /// <param name="element">the xml element</param>
    /// <param name="name">Name of the element that should be returned</param>
    /// <returns>the "name" element of the XElement</returns>
    public static XElement? GetElement(this XElement? element, string name)
    {
        var splitName = SplitName(name);
        return element?.GetElement(splitName.Item1, splitName.Item2);
    }

    /// <summary>
    ///     Gets the element of the given XElement
    /// </summary>
    /// <param name="element">the xml element</param>
    /// <param name="namespacePrefix">the namespace prefix of the element that should be returned</param>
    /// <param name="name">Name of the element that should be returned</param>
    /// <returns>the "name" element with the prefix "namespacePrefix" of the XElement</returns>
    public static XElement? GetElement(this XElement? element, string? namespacePrefix, string name)
    {
        var xNamespace = element.GetNamespacePrefix(namespacePrefix);
        if (xNamespace == null) return null;
        return element?.Element(xNamespace + name);
    }

    /// <summary>
    ///     Gets all elements of the given XElement
    /// </summary>
    /// <param name="element">the xml element</param>
    /// <param name="name">Name of the elements that should be returned</param>
    /// <returns>all "name" elements of the given XElement</returns>
    public static IEnumerable<XElement?>? GetElements(this XElement? element, string name)
    {
        var splitName = SplitName(name);
        return element.GetElements(splitName.Item1, splitName.Item2);
    }

    /// <summary>
    ///     Gets all elements of the given XElement
    /// </summary>
    /// <param name="element">the xml element</param>
    /// <param name="namespacePrefix">the namespace prefix of the elements that should be returned</param>
    /// <param name="name">Name of the elements that should be returned</param>
    /// <returns>all "name" elements of the given XElement</returns>
    public static IEnumerable<XElement?>? GetElements(this XElement? element, string? namespacePrefix, string name)
    {
        var xNamespace = element.GetNamespacePrefix(namespacePrefix);
        if (xNamespace == null) return null;
        return element?.Elements(xNamespace + name);
    }

    /// <summary>
    ///     Gets the namespace prefix of the given XElement
    /// </summary>
    /// <param name="element">the xml element</param>
    /// <returns>the namespace prefix</returns>
    public static XNamespace? GetNamespacePrefix(this XElement element)
    {
        return element.GetNamespacePrefix(null);
    }

    /// <summary>
    ///     Gets the namespace prefix of the given XElement, if namespacePrefix is null or empty, it returns the default
    ///     namespace.
    /// </summary>
    /// <param name="element">the xml element</param>
    /// <param name="namespacePrefix">the namespace prefix</param>
    /// <returns>the namespace prefix or default namespace if the <paramref name="namespacePrefix" /> is null or empty</returns>
    public static XNamespace? GetNamespacePrefix(this XElement? element, string? namespacePrefix)
    {
        var xNamespace = string.IsNullOrWhiteSpace(namespacePrefix)
            ? element?.GetDefaultNamespace()
            : element?.GetNamespaceOfPrefix(namespacePrefix);
        return xNamespace;
    }

    /// <summary>
    ///     splits the name into namespace and name if it contains a :
    ///     if it does not contain a namespace, item1 is null and item2 is the original name
    /// </summary>
    /// <param name="name">the input name</param>
    /// <returns>split namespace and name, item1 is null if namespace is empty</returns>
    private static Tuple<string?, string> SplitName(string name)
    {
        string? xNamespace = null;
        if (name.Contains(":"))
        {
            var pos = name.IndexOf(':');
            xNamespace = name[..pos];
            name = name[(pos + 1)..];
        }

        return new Tuple<string?, string>(xNamespace, name);
    }
}