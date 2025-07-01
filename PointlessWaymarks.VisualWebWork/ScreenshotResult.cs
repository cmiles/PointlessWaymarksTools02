namespace PointlessWaymarks.VisualWebWork;

public class ScreenshotResult
{
    public byte[]? ImageBytes { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool Success { get; set; }

    public static ScreenshotResult CreateError(string message)
    {
        return new ScreenshotResult { Success = false, Message = message };
    }

    public static ScreenshotResult CreateSuccess(byte[] imageBytes)
    {
        return new ScreenshotResult
            { Success = true, ImageBytes = imageBytes, Message = "Screenshot captured successfully" };
    }
}