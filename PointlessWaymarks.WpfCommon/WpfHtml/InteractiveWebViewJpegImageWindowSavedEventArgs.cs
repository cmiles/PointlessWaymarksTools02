namespace PointlessWaymarks.WpfCommon.WpfHtml;

public class InteractiveWebViewJpegImageWindowSavedEventArgs(string newFilename, string url) : EventArgs
{
    public string NewFilename { get; } = newFilename;
    public string Url { get; } = url;
}