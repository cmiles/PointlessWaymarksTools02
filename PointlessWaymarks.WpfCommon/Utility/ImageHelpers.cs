using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PDFtoImage;
using PhotoSauce.MagicScaler;
using PointlessWaymarks.CommonTools;
using SkiaSharp;
using PixelFormats = System.Windows.Media.PixelFormats;

namespace PointlessWaymarks.WpfCommon.Utility;

public static class ImageHelpers
{
    //https://docs.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.bitmapsource.create?view=net-5.0
    public static readonly BitmapSource BlankImage = BitmapSource.Create(8, 8, 96, 96, PixelFormats.Indexed1,
        new BitmapPalette(new List<Color> { Colors.Transparent }), new byte[8], 1);

    public static async Task<BitmapSource> InMemoryThumbnailFromFile(FileInfo file, int width, int quality)
    {
        await using var fileStream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read);
        await using var outStream = new MemoryStream();

        var settings = new ProcessImageSettings
            { Width = width, EncoderOptions = new JpegEncoderOptions(quality, ChromaSubsampleMode.Default, true) };
        MagicImageProcessor.ProcessImage(fileStream, outStream, settings);

        outStream.Position = 0;

        var uiImage = new BitmapImage();
        uiImage.BeginInit();
        uiImage.CacheOption = BitmapCacheOption.OnLoad;
        uiImage.StreamSource = outStream;
        uiImage.EndInit();
        uiImage.Freeze();

        return uiImage;
    }

    public static async Task<string> PdfToJpeg(string pdfFileName, int maxWidth,
        int jpegQuality, SKColor backgroundColor)
    {
        // Convert PDF pages to images asynchronously
        await using var pdfStream = File.OpenRead(pdfFileName);
        var skBitmaps = await Conversion.ToImagesAsync(pdfStream).ToListAsync();

        // Calculate the total height and maximum width
        var totalHeight = 0;
        var combinedWidth = 0;
        foreach (var bitmap in skBitmaps)
        {
            totalHeight += bitmap.Height;
            combinedWidth = Math.Max(combinedWidth, bitmap.Width);
        }

        // Scale the combined width to the max width if necessary
        if (combinedWidth > maxWidth)
        {
            var scale = (float)maxWidth / combinedWidth;
            combinedWidth = maxWidth;
            totalHeight = (int)(totalHeight * scale);
        }

        // Create a new bitmap to hold the combined image
        using var combinedBitmap = new SKBitmap(combinedWidth, totalHeight);
        using var canvas = new SKCanvas(combinedBitmap);
        canvas.Clear(backgroundColor);

        // Draw each image onto the combined bitmap
        var yOffset = 0;
        foreach (var bitmap in skBitmaps)
        {
            var scaledHeight = (int)(bitmap.Height * ((float)combinedWidth / bitmap.Width));
            var destRect = new SKRect(0, yOffset, combinedWidth, yOffset + scaledHeight);
            canvas.DrawBitmap(bitmap, destRect);
            yOffset += scaledHeight;
        }

        // Save the combined image as a JPEG
        using var image = SKImage.FromBitmap(combinedBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, jpegQuality);
        var safeName = UniqueFileTools.UniqueFile(new DirectoryInfo(Path.GetDirectoryName(pdfFileName)!),
            $"{Path.GetFileNameWithoutExtension(pdfFileName)}.jpg")!;
        await using var outputStream = File.OpenWrite(safeName.FullName);
        data.SaveTo(outputStream);

        return safeName.FullName;
    }
}