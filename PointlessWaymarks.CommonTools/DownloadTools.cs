namespace PointlessWaymarks.CommonTools;

public static class DownloadTools
{
    public static async Task Download(
        string downloadUrl,
        string destinationFilePath,
        Func<long?, long, double?, bool> progressChanged)
    {
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromDays(1);
        using var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);

        response.EnsureSuccessStatusCode();
        var totalBytes = response.Content.Headers.ContentLength;

        await using var contentStream = await response.Content.ReadAsStreamAsync();
        var totalBytesRead = 0L;
        var readCount = 0L;
        var buffer = new byte[8192];
        var isMoreToRead = true;

        await using var fileStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write,
            FileShare.None, 8192, true);

        do
        {
            var bytesRead = await contentStream.ReadAsync(buffer);
            if (bytesRead == 0)
            {
                isMoreToRead = false;

                if (progressChanged(totalBytes, totalBytesRead, CalculatePercentage(totalBytes, totalBytesRead)))
                    throw new OperationCanceledException();

                continue;
            }

            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));

            totalBytesRead += bytesRead;
            readCount++;

            if (readCount % 100 == 0)
                if (progressChanged(totalBytes, totalBytesRead, CalculatePercentage(totalBytes, totalBytesRead)))
                    throw new OperationCanceledException();
        } while (isMoreToRead);

        return;

        static double? CalculatePercentage(long? totalDownloadSize, long totalBytesRead)
        {
            return totalDownloadSize.HasValue
                ? Math.Round((double)totalBytesRead / totalDownloadSize.Value, 2)
                : null;
        }
    }
}