using System.Buffers.Binary;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace PointlessWaymarks.SpatialTools;

/// <summary>
/// Represents a 1x1 degree Digital Elevation Model (DEM) tile, commonly stored in HGT format
/// (such as SRTM or Viewfinderpanoramas data).
/// </summary>
public class HgtTile
{
    private readonly short[] _elevations;

    public string TileKey { get; }
    public int GridSize { get; }
    public int OriginLat { get; }
    public int OriginLon { get; }

    /// <summary>
    /// Creates an HgtTile from raw HGT or ZIP bytes with a known tile key (e.g. "N34W111").
    /// </summary>
    public HgtTile(byte[] rawHgtOrZipBytes, string tileKey)
    {
        if (!TryParseTileKey(tileKey, out var lat, out var lon))
        {
            throw new ArgumentException($"Cannot determine origin latitude/longitude from tile key '{tileKey}'",
                nameof(tileKey));
        }

        TileKey = FormatTileKey(lat, lon);
        OriginLat = lat;
        OriginLon = lon;

        var hgtBytes = ExtractHgtBytes(rawHgtOrZipBytes, TileKey);
        if (!IsValidHgtLength(hgtBytes.Length))
        {
            throw new ArgumentException(
                $"Invalid HGT data length ({hgtBytes.Length} bytes). Expected square grid (e.g. 1201x1201x2 = 2884802 or 3601x3601x2 = 25934402).",
                nameof(rawHgtOrZipBytes));
        }

        int numShorts = hgtBytes.Length / 2;
        GridSize = (int)Math.Round(Math.Sqrt(numShorts));
        _elevations = new short[numShorts];

        for (int i = 0; i < numShorts; i++)
        {
            _elevations[i] = BinaryPrimitives.ReadInt16BigEndian(hgtBytes.AsSpan(i * 2, 2));
        }
    }

    /// <summary>
    /// Creates an HgtTile from raw HGT or ZIP bytes with explicit origin coordinates.
    /// </summary>
    public HgtTile(byte[] rawHgtOrZipBytes, int originLat, int originLon, string? tileKey = null)
    {
        OriginLat = originLat;
        OriginLon = originLon;
        TileKey = tileKey ?? FormatTileKey(originLat, originLon);

        var hgtBytes = ExtractHgtBytes(rawHgtOrZipBytes, TileKey);
        if (!IsValidHgtLength(hgtBytes.Length))
        {
            throw new ArgumentException(
                $"Invalid HGT data length ({hgtBytes.Length} bytes). Expected square grid (e.g. 1201x1201x2 = 2884802 or 3601x3601x2 = 25934402).",
                nameof(rawHgtOrZipBytes));
        }

        int numShorts = hgtBytes.Length / 2;
        GridSize = (int)Math.Round(Math.Sqrt(numShorts));
        _elevations = new short[numShorts];

        for (int i = 0; i < numShorts; i++)
        {
            _elevations[i] = BinaryPrimitives.ReadInt16BigEndian(hgtBytes.AsSpan(i * 2, 2));
        }
    }

    /// <summary>
    /// Creates an HgtTile directly from an elevation array and grid size.
    /// </summary>
    public HgtTile(short[] elevations, int gridSize, int originLat, int originLon, string? tileKey = null)
    {
        if (elevations == null || elevations.Length != gridSize * gridSize)
        {
            throw new ArgumentException($"Elevation array length does not match gridSize^2 ({gridSize * gridSize})");
        }

        _elevations = elevations;
        GridSize = gridSize;
        OriginLat = originLat;
        OriginLon = originLon;
        TileKey = tileKey ?? FormatTileKey(originLat, originLon);
    }

    /// <summary>
    /// Formats an origin latitude and longitude into a standard 1x1 degree HGT tile key (e.g. "N34W111", "S05E018").
    /// </summary>
    public static string FormatTileKey(int originLat, int originLon)
    {
        char latPrefix = originLat >= 0 ? 'N' : 'S';
        char lonPrefix = originLon >= 0 ? 'E' : 'W';
        return $"{latPrefix}{Math.Abs(originLat):D2}{lonPrefix}{Math.Abs(originLon):D3}";
    }

    /// <summary>
    /// Computes the HGT tile key covering the given latitude and longitude.
    /// </summary>
    public static string GetTileKey(double latitude, double longitude)
    {
        int originLat = (int)Math.Floor(latitude);
        int originLon = (int)Math.Floor(longitude);

        if (originLat >= 90) originLat = 89;
        if (originLat < -90) originLat = -90;
        if (originLon >= 180) originLon = 179;
        if (originLon < -180) originLon = -180;

        return FormatTileKey(originLat, originLon);
    }

    /// <summary>
    /// Tries to parse the origin latitude and longitude from a tile key or file name (e.g. "N34W111", "n34w111.hgt", "N34W111.zip").
    /// </summary>
    public static bool TryParseTileKey(string keyOrFileName, out int originLat, out int originLon)
    {
        originLat = 0;
        originLon = 0;
        if (string.IsNullOrWhiteSpace(keyOrFileName)) return false;

        var nameOnly = Path.GetFileName(keyOrFileName);
        var match = Regex.Match(nameOnly, @"([NSns])(\d{2})([EWew])(\d{3})");
        if (!match.Success) return false;

        var latSign = match.Groups[1].Value.Equals("S", StringComparison.OrdinalIgnoreCase) ? -1 : 1;
        var latVal = int.Parse(match.Groups[2].Value);
        originLat = latSign * latVal;

        var lonSign = match.Groups[3].Value.Equals("W", StringComparison.OrdinalIgnoreCase) ? -1 : 1;
        var lonVal = int.Parse(match.Groups[4].Value);
        originLon = lonSign * lonVal;

        return true;
    }

    /// <summary>
    /// Checks if a byte length corresponds to a valid square 16-bit integer DEM grid.
    /// </summary>
    public static bool IsValidHgtLength(int lengthInBytes)
    {
        if (lengthInBytes < 8 || lengthInBytes % 2 != 0) return false;
        int numShorts = lengthInBytes / 2;
        int sqrt = (int)Math.Round(Math.Sqrt(numShorts));
        return sqrt >= 2 && sqrt * sqrt == numShorts;
    }

    /// <summary>
    /// Extracts raw HGT byte content from a buffer that might be a raw HGT file or a ZIP archive containing HGT files.
    /// </summary>
    public static byte[] ExtractHgtBytes(byte[]? fileBytes, string? expectedTileKey = null)
    {
        if (fileBytes == null || fileBytes.Length < 4) return fileBytes ?? [];

        // Check for ZIP magic signature: 'P', 'K', 0x03, 0x04
        if (fileBytes[0] == 0x50 && fileBytes[1] == 0x4B && fileBytes[2] == 0x03 && fileBytes[3] == 0x04)
        {
            using var ms = new MemoryStream(fileBytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

            ZipArchiveEntry? entry = null;

            if (!string.IsNullOrWhiteSpace(expectedTileKey))
            {
                entry = zip.Entries.FirstOrDefault(e =>
                            Path.GetFileName(e.FullName)
                                .Equals($"{expectedTileKey}.hgt", StringComparison.OrdinalIgnoreCase))
                        ?? zip.Entries.FirstOrDefault(e =>
                            Path.GetFileName(e.FullName)
                                .StartsWith(expectedTileKey, StringComparison.OrdinalIgnoreCase) &&
                            e.FullName.EndsWith(".hgt", StringComparison.OrdinalIgnoreCase));
            }

            entry ??= zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".hgt", StringComparison.OrdinalIgnoreCase))
                      ?? zip.Entries.FirstOrDefault(e => !e.FullName.EndsWith("/"));

            if (entry != null)
            {
                using var entryStream = entry.Open();
                using var outMs = new MemoryStream();
                entryStream.CopyTo(outMs);
                return outMs.ToArray();
            }
        }

        return fileBytes;
    }

    /// <summary>
    /// Gets the elevation at the specified latitude and longitude using bilinear interpolation.
    /// Returns null if the coordinate is outside this tile or falls on a void cell without valid neighbors.
    /// </summary>
    public double? GetElevation(double latitude, double longitude)
    {
        const double epsilon = 1e-9;
        if (latitude < OriginLat - epsilon || latitude > OriginLat + 1.0 + epsilon ||
            longitude < OriginLon - epsilon || longitude > OriginLon + 1.0 + epsilon)
        {
            return null;
        }

        double relLat = Math.Clamp(latitude - OriginLat, 0.0, 1.0);
        double relLon = Math.Clamp(longitude - OriginLon, 0.0, 1.0);

        double col = relLon * (GridSize - 1);
        double row = (1.0 - relLat) * (GridSize - 1);

        int x0 = (int)Math.Floor(col);
        int x1 = Math.Min(x0 + 1, GridSize - 1);
        int y0 = (int)Math.Floor(row);
        int y1 = Math.Min(y0 + 1, GridSize - 1);

        double dx = col - x0;
        double dy = row - y0;

        short val00 = _elevations[y0 * GridSize + x0];
        short val10 = _elevations[y0 * GridSize + x1];
        short val01 = _elevations[y1 * GridSize + x0];
        short val11 = _elevations[y1 * GridSize + x1];

        bool is00Valid = val00 > -32000;
        bool is10Valid = val10 > -32000;
        bool is01Valid = val01 > -32000;
        bool is11Valid = val11 > -32000;

        if (is00Valid && is10Valid && is01Valid && is11Valid)
        {
            double top = val00 * (1.0 - dx) + val10 * dx;
            double bottom = val01 * (1.0 - dx) + val11 * dx;
            return top * (1.0 - dy) + bottom * dy;
        }

        double totalWeight = 0;
        double weightedSum = 0;

        if (is00Valid)
        {
            double w = (1.0 - dx) * (1.0 - dy);
            weightedSum += val00 * w;
            totalWeight += w;
        }

        if (is10Valid)
        {
            double w = dx * (1.0 - dy);
            weightedSum += val10 * w;
            totalWeight += w;
        }

        if (is01Valid)
        {
            double w = (1.0 - dx) * dy;
            weightedSum += val01 * w;
            totalWeight += w;
        }

        if (is11Valid)
        {
            double w = dx * dy;
            weightedSum += val11 * w;
            totalWeight += w;
        }

        if (totalWeight < 1e-6) return null;
        return weightedSum / totalWeight;
    }
}