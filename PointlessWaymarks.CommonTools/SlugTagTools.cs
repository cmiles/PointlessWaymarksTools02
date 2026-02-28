using System.Text;
using System.Text.RegularExpressions;

namespace PointlessWaymarks.CommonTools;

public static class SlugTagTools
{
    private static string? ConvertEdgeCases(char c, bool toLower)
    {
        var swap = c switch
        {
            'ı' => "i",
            'ł' => "l",
            'Ł' => toLower ? "l" : "L",
            'đ' => "d",
            'ß' => "ss",
            'ø' => "o",
            'Þ' => "th",
            _ => null
        };

        return swap;
    }

    /// <summary>
    ///     This is intended for use in the live processing of user input where you want to create slug like strings but to be
    ///     friendly to typed input (for example so trailing spaces must be allowed to avoid fighting the user) - in general
    ///     this is not as strict as CreateSpacedString.
    /// </summary>
    /// <param name="toLower"></param>
    /// <param name="value"></param>
    /// <param name="allowedBeyondAtoZ1To9"></param>
    /// <returns></returns>
    public static string CreateRelaxedInputSpacedString(bool toLower, string? value,
        List<char>? allowedBeyondAtoZ1To9 = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        allowedBeyondAtoZ1To9 ??= [];

        var normalized = value.Normalize(NormalizationForm.FormKD);

        var len = normalized.Length;
        var sb = new StringBuilder(len);

        for (var i = 0; i < len; i++)
        {
            var c = normalized[i];
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9' || allowedBeyondAtoZ1To9.Contains(c))
            {
                sb.Append(c);
            }
            else if (c is >= 'A' and <= 'Z')
            {
                // Tricky way to convert to lowercase
                if (toLower)
                    sb.Append((char)(c | 32));
                else
                    sb.Append(c);
            }
            else
            {
                var swap = ConvertEdgeCases(c, toLower);

                if (swap != null) sb.Append(swap);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Creates a Slug.
    /// </summary>
    /// <param name="toLower"></param>
    /// <param name="values"></param>
    /// <returns></returns>
    public static string CreateSlug(bool toLower, params string[] values)
    {
        if (values.Length == 0)
            return "";
        return CreateSlug(toLower, string.Join("-", values));
    }

    /// <summary>
    ///     Creates a slug.
    /// </summary>
    /// <param name="toLower"></param>
    /// <param name="value"></param>
    /// <param name="maxLength"></param>
    /// <returns></returns>
    //     References:
    //     https://stackoverflow.com/questions/25259/how-does-stack-overflow-generate-its-seo-friendly-urls
    //     http://www.unicode.org/reports/tr15/tr15-34.html
    //     https://meta.stackexchange.com/questions/7435/non-us-ascii-characters-dropped-from-full-profile-url/7696#7696
    //     https://stackoverflow.com/questions/25259/how-do-you-include-a-webpage-title-as-part-of-a-webpage-url/25486#25486
    //     https://stackoverflow.com/questions/3769457/how-can-i-remove-accents-on-a-string
    public static string CreateSlug(bool toLower, string? value, int maxLength = 100)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var normalized = value.Normalize(NormalizationForm.FormKD);

        var len = normalized.Length;
        var prevDash = false;
        var sb = new StringBuilder(len);

        for (var i = 0; i < len; i++)
        {
            var c = normalized[i];
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (prevDash)
                {
                    sb.Append('-');
                    prevDash = false;
                }

                sb.Append(c);
            }
            else if (c is >= 'A' and <= 'Z')
            {
                if (prevDash)
                {
                    sb.Append('-');
                    prevDash = false;
                }

                // Tricky way to convert to lowercase
                if (toLower)
                    sb.Append((char)(c | 32));
                else
                    sb.Append(c);
            }
            else if (c is ' ' or ',' or '.' or '/' or '\\' or '-' or '_' or '=')
            {
                if (!prevDash && sb.Length > 0) prevDash = true;
            }
            else
            {
                var swap = ConvertEdgeCases(c, toLower);

                if (swap != null)
                {
                    if (prevDash)
                    {
                        sb.Append('-');
                        prevDash = false;
                    }

                    sb.Append(swap);
                }
            }

            if (sb.Length == maxLength)
                break;
        }

        return sb.ToString();
    }

    /// <summary>
    ///     This method mimics the Create method but is focused on creating a spaced string with the intent that in some cases
    ///     this may create a format that communicates the same information and intent as the Create Slug method but is easier
    ///     to read and more user-friendly.
    /// </summary>
    /// <param name="toLower"></param>
    /// <param name="value"></param>
    /// <param name="maxLength"></param>
    /// <returns></returns>
    public static string CreateSpacedString(bool toLower, string value, int? maxLength = 100)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var normalized = value.Normalize(NormalizationForm.FormKD);

        var len = normalized.Length;
        var previousSpace = false;
        var sb = new StringBuilder(len);

        for (var i = 0; i < len; i++)
        {
            var c = normalized[i];
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-')
            {
                if (previousSpace)
                {
                    sb.Append(' ');
                    previousSpace = false;
                }

                sb.Append(c);
            }
            else if (c is >= 'A' and <= 'Z')
            {
                if (previousSpace)
                {
                    sb.Append(' ');
                    previousSpace = false;
                }

                // Tricky way to convert to lowercase
                if (toLower)
                    sb.Append((char)(c | 32));
                else
                    sb.Append(c);
            }
            else if (c is ',' or '.' or '/' or '\\' or '=' or ' ' or ';')
            {
                if (!previousSpace && sb.Length > 0) previousSpace = true;
            }
            else
            {
                var swap = ConvertEdgeCases(c, toLower);

                if (swap != null)
                {
                    if (previousSpace)
                    {
                        sb.Append(' ');
                        previousSpace = false;
                    }

                    sb.Append(swap);
                }
            }

            if (maxLength != null && sb.Length == maxLength)
                break;
        }

        return sb.ToString();
    }

    /// <summary>
    ///     This uses random lower case letters omitting vowels and some easily confused
    ///     letters (like l and 1) to create a string that is less likely to be offensive,
    ///     funny or imply meaning when there is none...
    /// </summary>
    /// <param name="length"></param>
    /// <returns></returns>
    public static string RandomLowerCaseSaferString(int length)
    {
        // ReSharper disable once StringLiteralTypo
        var chars = "bcdfghjmpqrtvwxy";
        var stringChars = new char[length];
        var random = new Random();

        for (var i = 0; i < stringChars.Length; i++) stringChars[i] = chars[random.Next(chars.Length)];

        return new string(stringChars);
    }

    /// <summary>
    ///     A simple and straightforward lower case random string generator - consider using
    ///     the RandomLowerCaseSaferString method for most cases.
    /// </summary>
    /// <param name="length"></param>
    /// <returns></returns>
    public static string RandomLowerCaseString(int length)
    {
        // ReSharper disable once StringLiteralTypo
        var chars = "abcdefghijklmnopqrstuvwxyz";
        var stringChars = new char[length];
        var random = new Random();

        for (var i = 0; i < stringChars.Length; i++) stringChars[i] = chars[random.Next(chars.Length)];

        return new string(stringChars);
    }

    public static string TagListCleanupToSpacedString(string? tags)
    {
        return string.IsNullOrWhiteSpace(tags) ? string.Empty : TagListJoinToSpacedString(TagListParseToSpacedString(tags));
    }

    public static List<string> TagListCleanupToSpacedString(List<string> listToClean)
    {
        if (!listToClean.Any()) return [];

        return listToClean.Select(TagListItemCleanupToSpacedString).Where<string>(x => !string.IsNullOrWhiteSpace(x)).Distinct()
            .ToList();
    }

    /// <summary>
    ///     Use to clean up a single tag - trims and removes inner multi-space
    /// </summary>
    /// <param name="toClean"></param>
    /// <returns></returns>
    public static string TagListItemCleanupToSpacedString(string? toClean)
    {
        if (string.IsNullOrWhiteSpace(toClean)) return string.Empty;

        return Regex.Replace(CreateSpacedString(true, toClean, 200), @"\s+", " ").TrimNullToEmpty()
            .ToLower();
    }

    /// <summary>
    ///     Cleans and joins a list of tags into a string suitable for use as a database Tag value with this program's
    ///     conventions.
    /// </summary>
    /// <param name="tagList"></param>
    /// <returns></returns>
    public static string TagListJoinToSpacedString(List<string> tagList)
    {
        if (tagList.Count < 1) return string.Empty;

        var cleanedList = tagList.Select(TagListItemCleanupToSpacedString).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()
            .OrderBy(x => x).ToList();

        return string.Join(",", cleanedList);
    }

    /// <summary>
    ///     Converts a string into a List of Tags - resulting tags will be cleaned/converted according to program conventions
    /// </summary>
    /// <param name="rawTagString"></param>
    /// <returns></returns>
    public static List<string> TagListParseToSpacedString(string? rawTagString)
    {
        if (rawTagString == null) return [];
        if (string.IsNullOrWhiteSpace(rawTagString)) return [];

        return rawTagString.Split(",").Select(TagListItemCleanupToSpacedString).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()
            .OrderBy(x => x).ToList();
    }

    /// <summary>
    ///     Takes an incoming string, parses and cleans tags according to program conventions, joins the tags
    ///     back into a string. This can be used to convert user input into a database
    ///     appropriate tag value. Note: the output of this method will be clean and correctly formatted but may not
    ///     be what the user intended - this method may be best used in a situation where the user has had
    ///     a preview of the converted content.
    /// </summary>
    /// <param name="toClean"></param>
    /// <returns></returns>
    public static string TagListParseCleanAndJoinToSpacedString(string? toClean)
    {
        return TagListJoinToSpacedString(TagListParseToSpacedString(toClean));
    }
}