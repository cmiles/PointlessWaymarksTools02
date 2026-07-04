using System.Text;

namespace PointlessWaymarks.CommonTools;

public class Utf8StringWriter : StringWriter
{
    //https://stackoverflow.com/questions/955611/xmlwriter-to-write-to-a-string-instead-of-to-a-file/955698#955698
    public override Encoding Encoding => Encoding.UTF8;
}

public sealed class Utf8StringWriterNoBom : StringWriter
{
    // The 'false' parameter explicitly tells the encoding NOT to emit a BOM.
    public override Encoding Encoding { get; } = new UTF8Encoding(false);
}