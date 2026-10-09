using System.Collections.Concurrent;
using System.Data;
using System.IO.Compression;
using System.Text.Json;
using NetTopologySuite.Geometries;
using PointlessWaymarks.SpatialTools.ElevationServiceModels;
using Serilog;

namespace PointlessWaymarks.SpatialTools;

public static class ElevationService
{
    private static readonly HttpClient ElevationHttpClient = new();

    private static readonly ConcurrentDictionary<string, Task<HgtTile?>> StaticDemTileCache =
        new(StringComparer.OrdinalIgnoreCase);

    public static string StaticDemAddress { get; set; } = @"M:\PointlessWaymarksPublications\dem";
    
    /// <summary>
    /// Clears the in-memory cache of loaded static DEM tiles.
    /// </summary>
    public static void ClearStaticDemCache()
    {
        StaticDemTileCache.Clear();
    }

    /// <summary>
    /// Manually registers/caches an HgtTile in memory for a given directory or URL base.
    /// </summary>
    public static void CacheStaticDemTile(string localDirectoryOrUrl, HgtTile tile)
    {
        var cacheKey = $"{localDirectoryOrUrl.TrimEnd('/', '\\')}|{tile.TileKey}";
        StaticDemTileCache[cacheKey] = Task.FromResult<HgtTile?>(tile);
    }

    private static async Task<string> OpenTopoGetStringWithRetry(string requestUri, IProgress<string>? progress)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                return await ElevationHttpClient.GetStringAsync(requestUri).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests && attempt < 3)
            {
                progress?.Report($"Rate limited by OpenTopoData (attempt {attempt}/3), waiting before retry...");
                await Task.Delay(2500 * attempt).ConfigureAwait(false);
            }
        }

        return await ElevationHttpClient.GetStringAsync(requestUri).ConfigureAwait(false);
    }

    private static async Task<List<ElevationResult>> OpenTopoQuery(string openTopoDataSet,
        List<CoordinateZ> coordinates, IProgress<string>? progress)
    {
        if (!coordinates.Any()) return [];

        var partitionedCoordinates = coordinates.Chunk(100).ToList();

        var resultList = new List<ElevationResult>();

        progress?.Report(
            $"{coordinates.Count} Coordinates for Elevation - querying in {partitionedCoordinates.Count} groups from OpenTopo {openTopoDataSet}...");

        var isFirst = true;

        foreach (var loopCoordinateGroups in partitionedCoordinates)
        {
            if (isFirst)
            {
                isFirst = false;
            }
            else
            {
                await Task.Delay(2000);
            }

            var requestUri =
                $"https://api.opentopodata.org/v1/{openTopoDataSet}?locations={string.Join("|", loopCoordinateGroups.Select(x => $"{x.Y},{x.X}"))}";

            progress?.Report($"Sending request to {requestUri}");

            var elevationReturn = await OpenTopoGetStringWithRetry(requestUri, progress).ConfigureAwait(false);

            progress?.Report($"Parsing Return from {requestUri}");

            var elevationParsed = JsonSerializer.Deserialize<ElevationResponse>(elevationReturn);

            if (elevationParsed == null)
            {
                Log.Error("Elevation Service - Could not parse information from the Elevation Service - Uri {0}",
                    requestUri);
                throw new DataException(
                    $"Elevation Service - Could not parse information from the Elevation Service - Uri {requestUri}");
            }

            resultList.AddRange(elevationParsed.Elevations);
        }

        return resultList;
    }

    private static async Task<List<CoordinateZ>> OpenTopoElevation(string openTopoDataSet,
        List<CoordinateZ> coordinates, IProgress<string>? progress)
    {
        if (!coordinates.Any()) return coordinates;

        var resultList = await OpenTopoQuery(openTopoDataSet, coordinates, progress).ConfigureAwait(false);

        progress?.Report("Assigning results to Coordinates");

        foreach (var loopResults in resultList)
        {
            if (loopResults.Location == null) continue;
            // ReSharper disable CompareOfFloatsByEqualityOperator
            //Expecting to get the exact Lat Long back thru the elevation query
            coordinates.Where(x => x.X == loopResults.Location.Longitude && x.Y == loopResults.Location.Latitude)
                // ReSharper restore CompareOfFloatsByEqualityOperator
                .ToList().ForEach(x => x.Z = loopResults.Elevation ?? 0);
        }

        return coordinates;
    }

    private static async Task<double?> OpenTopoElevation(string openTopoDataSet, double latitude, double longitude,
        IProgress<string>? progress)
    {
        var requestUri = $"https://api.opentopodata.org/v1/{openTopoDataSet}?locations={latitude},{longitude}";

        progress?.Report($"Sending request to {requestUri}");

        var elevationReturn = await OpenTopoGetStringWithRetry(requestUri, progress).ConfigureAwait(false);

        progress?.Report($"Parsing Return from {requestUri}");

        var elevationParsed = JsonSerializer.Deserialize<ElevationResponse>(elevationReturn);

        return elevationParsed?.Elevations.FirstOrDefault()?.Elevation;
    }

    public static async Task<List<CoordinateZ>> OpenTopoMapZenElevation(List<CoordinateZ> coordinates,
        IProgress<string>? progress)
    {
        return await OpenTopoElevation("mapzen", coordinates, progress).ConfigureAwait(false);
    }

    public static async Task<double?> OpenTopoMapZenElevation(double latitude, double longitude,
        IProgress<string>? progress)
    {
        return await OpenTopoElevation("mapzen", latitude, longitude, progress).ConfigureAwait(false);
    }

    public static async Task<List<CoordinateZ>> OpenTopoNedElevation(List<CoordinateZ> coordinates,
        IProgress<string>? progress)
    {
        return await OpenTopoElevation("ned10m", coordinates, progress).ConfigureAwait(false);
    }

    public static async Task<double?> OpenTopoNedElevation(double latitude, double longitude,
        IProgress<string>? progress)
    {
        return await OpenTopoElevation("ned10m", latitude, longitude, progress).ConfigureAwait(false);
    }

    public static async Task<List<CoordinateZ>> Elevation(List<CoordinateZ> coordinates, 
        IProgress<string>? progress = null)
    {
        return await Elevation(coordinates, StaticDemAddress, progress).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets elevation values for a list of coordinates. Where possible, Static DEMs (local files or URL)
    /// from staticDemAddress are used; OpenTopo Ned10 is used as the first fallback for any coordinates that cannot
    /// be resolved via Static DEMs (or if staticDemAddress is not provided), and OpenTopo Mapzen is used as a final fallback
    /// for coordinates not covered by Ned10 (e.g. non-US locations).
    /// Updates coordinate.Z with the found elevation in meters.
    /// </summary>
    /// <param name="coordinates">The list of coordinates to assign elevation values to.</param>
    /// <param name="staticDemAddress">Optional local directory path or URL where HGT/ZIP DEM files are located.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <returns>The updated list of coordinates.</returns>
    public static async Task<List<CoordinateZ>> Elevation(List<CoordinateZ> coordinates, string? staticDemAddress, IProgress<string>? progress = null)
    {
        if (coordinates.Count == 0) return coordinates;

        var coordinatesNeedingFallback = new List<CoordinateZ>();

        if (!string.IsNullOrWhiteSpace(staticDemAddress))
        {
            var tileKeys = coordinates
                .Select(c => HgtTile.GetTileKey(c.Y, c.X))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            progress?.Report(
                $"{coordinates.Count} Coordinates for Elevation - checking {tileKeys.Count} DEM tiles from {staticDemAddress}...");

            var tileDict = new Dictionary<string, HgtTile?>(StringComparer.OrdinalIgnoreCase);
            var tileIndex = 0;

            foreach (var key in tileKeys)
            {
                tileIndex++;
                progress?.Report($"Loading DEM tile {key} ({tileIndex}/{tileKeys.Count})...");
                var tile = await GetStaticDemTile(staticDemAddress, key, progress).ConfigureAwait(false);
                tileDict[key] = tile;
            }

            foreach (var coord in coordinates)
            {
                var key = HgtTile.GetTileKey(coord.Y, coord.X);
                if (tileDict.TryGetValue(key, out var tile) && tile != null)
                {
                    var elev = tile.GetElevation(coord.Y, coord.X);
                    if (elev.HasValue)
                    {
                        coord.Z = elev.Value;
                        continue;
                    }
                }

                coordinatesNeedingFallback.Add(coord);
            }
        }
        else
        {
            coordinatesNeedingFallback.AddRange(coordinates);
        }

        if (coordinatesNeedingFallback.Count == 0)
        {
            progress?.Report($"All {coordinates.Count} Coordinates resolved via Static DEM");
            return coordinates;
        }

        progress?.Report(
            $"{coordinatesNeedingFallback.Count} of {coordinates.Count} Coordinates not resolved via Static DEM - querying OpenTopo Ned10...");

        var coordinatesNeedingZenFallback = new List<CoordinateZ>();

        try
        {
            var nedResults = await OpenTopoQuery("ned10m", coordinatesNeedingFallback, progress).ConfigureAwait(false);

            var resolvedCoords = new HashSet<CoordinateZ>();
            foreach (var loopResult in nedResults)
            {
                if (loopResult.Location == null || !loopResult.Elevation.HasValue) continue;

                // ReSharper disable CompareOfFloatsByEqualityOperator
                var matching = coordinatesNeedingFallback
                    .Where(x => x.X == loopResult.Location.Longitude && x.Y == loopResult.Location.Latitude)
                    .ToList();
                // ReSharper restore CompareOfFloatsByEqualityOperator

                foreach (var c in matching)
                {
                    c.Z = loopResult.Elevation.Value;
                    resolvedCoords.Add(c);
                }
            }

            foreach (var coord in coordinatesNeedingFallback)
            {
                if (!resolvedCoords.Contains(coord))
                {
                    coordinatesNeedingZenFallback.Add(coord);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex,
                "OpenTopo Ned query failed for {Count} coordinates - falling back to OpenTopo Mapzen",
                coordinatesNeedingFallback.Count);
            progress?.Report(
                $"OpenTopo Ned query error ({ex.Message}) - falling back to OpenTopo Mapzen...");
            coordinatesNeedingZenFallback = coordinatesNeedingFallback;
        }

        if (coordinatesNeedingZenFallback.Count > 0)
        {
            await Task.Delay(1000).ConfigureAwait(false);
            progress?.Report(
                $"{coordinatesNeedingZenFallback.Count} of {coordinates.Count} Coordinates not resolved via OpenTopo Ned10 - falling back to OpenTopo Mapzen...");
            await OpenTopoMapZenElevation(coordinatesNeedingZenFallback, progress).ConfigureAwait(false);
        }
        else
        {
            progress?.Report($"All {coordinates.Count} Coordinates resolved via OpenTopo Ned10");
        }

        return coordinates;
    }

    public static async Task<double?> Elevation(double latitude, double longitude, IProgress<string>? progress = null)
    {
        return await Elevation(latitude, longitude, StaticDemAddress, progress).ConfigureAwait(false);
    }
    
    /// <summary>
    /// Gets the elevation value in meters for a single latitude and longitude coordinate. Where possible,
    /// Static DEMs (local files or URL) from staticDemAddress are used; OpenTopo Ned10 is used as the first fallback
    /// if staticDemAddress is not provided or if Static DEM elevation cannot be determined, and OpenTopo Mapzen is used as
    /// a final fallback if Ned10 elevation is unavailable (e.g. non-US locations).
    /// </summary>
    /// <param name="latitude">The latitude of the target location.</param>
    /// <param name="longitude">The longitude of the target location.</param>
    /// <param name="staticDemAddress">Optional local directory path or URL where HGT/ZIP DEM files are located.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <returns>The elevation in meters, or null if neither Static DEM nor OpenTopo (Ned10 or Mapzen) could provide an elevation.</returns>
    public static async Task<double?> Elevation(double latitude, double longitude, string? staticDemAddress, IProgress<string>? progress = null)
    {
        if (!string.IsNullOrWhiteSpace(staticDemAddress))
        {
            var staticElev = await StaticDemElevation(staticDemAddress, latitude, longitude, progress)
                .ConfigureAwait(false);
            if (staticElev.HasValue)
            {
                return staticElev.Value;
            }

            progress?.Report(
                $"Static DEM elevation unavailable for {latitude}, {longitude} - falling back to OpenTopo Ned10...");
        }

        try
        {
            var nedElev = await OpenTopoNedElevation(latitude, longitude, progress).ConfigureAwait(false);
            if (nedElev.HasValue)
            {
                return nedElev.Value;
            }

            await Task.Delay(1000).ConfigureAwait(false);
            progress?.Report(
                $"OpenTopo Ned elevation unavailable for {latitude}, {longitude} - falling back to OpenTopo Mapzen...");
        }
        catch (Exception ex)
        {
            await Task.Delay(1000).ConfigureAwait(false);
            Log.Warning(ex,
                "OpenTopo Ned elevation query failed for {Latitude}, {Longitude} - falling back to OpenTopo Mapzen",
                latitude, longitude);
            progress?.Report(
                $"OpenTopo Ned elevation error for {latitude}, {longitude} ({ex.Message}) - falling back to OpenTopo Mapzen...");
        }

        return await OpenTopoMapZenElevation(latitude, longitude, progress).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets elevation values for a list of coordinates from local DEM files or a DEM web endpoint containing
    /// 1x1 degree HGT tiles (e.g. from viewfinderpanoramas.org). Updates coordinate.Z with the found elevation in meters.
    /// </summary>
    /// <param name="localDirectoryOrUrl">A local directory path or URL where HGT/ZIP DEM files (e.g. N34W111.hgt or N34W111.zip) are located.</param>
    /// <param name="coordinates">The list of coordinates to assign elevation values to.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <returns>The updated list of coordinates.</returns>
    public static async Task<List<CoordinateZ>> StaticDemElevation(string localDirectoryOrUrl,
        List<CoordinateZ> coordinates, IProgress<string>? progress = null)
    {
        if (coordinates.Count == 0) return coordinates;

        var tileKeys = coordinates
            .Select(c => HgtTile.GetTileKey(c.Y, c.X))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        progress?.Report(
            $"{coordinates.Count} Coordinates for Static DEM Elevation - {tileKeys.Count} DEM tiles needed from {localDirectoryOrUrl}...");

        var tileDict = new Dictionary<string, HgtTile?>(StringComparer.OrdinalIgnoreCase);
        var tileIndex = 0;

        foreach (var key in tileKeys)
        {
            tileIndex++;
            progress?.Report($"Loading DEM tile {key} ({tileIndex}/{tileKeys.Count})...");
            var tile = await GetStaticDemTile(localDirectoryOrUrl, key, progress).ConfigureAwait(false);
            tileDict[key] = tile;
        }

        progress?.Report("Assigning results to Coordinates");

        foreach (var coord in coordinates)
        {
            var key = HgtTile.GetTileKey(coord.Y, coord.X);
            if (tileDict.TryGetValue(key, out var tile) && tile != null)
            {
                var elev = tile.GetElevation(coord.Y, coord.X);
                if (elev.HasValue)
                {
                    coord.Z = elev.Value;
                }
            }
        }

        progress?.Report($"Static DEM Elevation completed for {coordinates.Count} Coordinates");
        return coordinates;
    }

    /// <summary>
    /// Gets the elevation value in meters for a single latitude and longitude coordinate from local DEM files
    /// or a DEM web endpoint containing 1x1 degree HGT tiles (e.g. from viewfinderpanoramas.org).
    /// </summary>
    /// <param name="localDirectoryOrUrl">A local directory path or URL where HGT/ZIP DEM files (e.g. N34W111.hgt or N34W111.zip) are located.</param>
    /// <param name="latitude">The latitude of the target location.</param>
    /// <param name="longitude">The longitude of the target location.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <returns>The elevation in meters, or null if the tile could not be found or the point is void/out of bounds.</returns>
    public static async Task<double?> StaticDemElevation(string localDirectoryOrUrl, double latitude,
        double longitude, IProgress<string>? progress = null)
    {
        var tileKey = HgtTile.GetTileKey(latitude, longitude);
        progress?.Report($"Loading DEM tile for {latitude}, {longitude} ({tileKey}) from {localDirectoryOrUrl}...");

        var tile = await GetStaticDemTile(localDirectoryOrUrl, tileKey, progress).ConfigureAwait(false);
        if (tile == null)
        {
            progress?.Report($"DEM tile {tileKey} not found at {localDirectoryOrUrl}");
            return null;
        }

        var elevation = tile.GetElevation(latitude, longitude);
        progress?.Report(elevation.HasValue
            ? $"Found elevation {elevation.Value:F1}m for {latitude}, {longitude}"
            : $"Could not determine elevation for {latitude}, {longitude} (void or out of bounds)");

        return elevation;
    }

    /// <summary>
    /// Gets the elevation value for a single CoordinateZ object from local DEM files or a DEM web endpoint,
    /// updating its Z property with the elevation in meters.
    /// </summary>
    /// <param name="localDirectoryOrUrl">A local directory path or URL where HGT/ZIP DEM files are located.</param>
    /// <param name="coordinate">The coordinate to update with elevation.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <returns>The updated CoordinateZ.</returns>
    public static async Task<CoordinateZ> StaticDemElevation(string localDirectoryOrUrl,
        CoordinateZ coordinate, IProgress<string>? progress = null)
    {
        var elev = await StaticDemElevation(localDirectoryOrUrl, coordinate.Y, coordinate.X, progress)
            .ConfigureAwait(false);
        if (elev.HasValue)
        {
            coordinate.Z = elev.Value;
        }

        return coordinate;
    }

    /// <summary>
    /// Gets or loads an HgtTile for the specified tile key from the given directory or URL (with in-memory caching).
    /// </summary>
    public static Task<HgtTile?> GetStaticDemTile(string localDirectoryOrUrl, string tileKey,
        IProgress<string>? progress = null)
    {
        var normalizedKey = tileKey.ToUpperInvariant();
        var cacheKey = $"{localDirectoryOrUrl.TrimEnd('/', '\\')}|{normalizedKey}";

        return StaticDemTileCache.GetOrAdd(cacheKey,
            _ => LoadStaticDemTile(localDirectoryOrUrl, normalizedKey, progress));
    }

    private static async Task<HgtTile?> LoadStaticDemTile(string localDirectoryOrUrl, string tileKey,
        IProgress<string>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(localDirectoryOrUrl))
        {
            return null;
        }

        if (Uri.TryCreate(localDirectoryOrUrl, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return await LoadStaticDemTileFromUrl(localDirectoryOrUrl, tileKey, progress).ConfigureAwait(false);
        }

        return await LoadStaticDemTileFromLocal(localDirectoryOrUrl, tileKey, progress).ConfigureAwait(false);
    }

    private static async Task<HgtTile?> LoadStaticDemTileFromLocal(string localPathOrDirectory, string tileKey,
        IProgress<string>? progress = null)
    {
        try
        {
            if (File.Exists(localPathOrDirectory))
            {
                progress?.Report($"Loading DEM file directly from {localPathOrDirectory}");
                var fileBytes = await File.ReadAllBytesAsync(localPathOrDirectory).ConfigureAwait(false);
                var hgtBytes = HgtTile.ExtractHgtBytes(fileBytes, tileKey);
                if (hgtBytes.Length > 0 && HgtTile.IsValidHgtLength(hgtBytes.Length))
                {
                    return new HgtTile(hgtBytes, tileKey);
                }
            }

            if (!Directory.Exists(localPathOrDirectory))
            {
                progress?.Report($"Local DEM directory {localPathOrDirectory} does not exist.");
                return null;
            }

            var candidateFileNames = new[]
            {
                $"{tileKey}.hgt",
                $"{tileKey}.zip",
                $"{tileKey}.hgt.zip",
                $"{tileKey.ToLowerInvariant()}.hgt",
                $"{tileKey.ToLowerInvariant()}.zip",
                $"{tileKey.ToLowerInvariant()}.hgt.zip",
                $"{tileKey.ToUpperInvariant()}.HGT",
                $"{tileKey.ToUpperInvariant()}.ZIP"
            };

            foreach (var cand in candidateFileNames)
            {
                var directPath = Path.Combine(localPathOrDirectory, cand);
                if (File.Exists(directPath))
                {
                    progress?.Report($"Found DEM file {directPath}");
                    var fileBytes = await File.ReadAllBytesAsync(directPath).ConfigureAwait(false);
                    var hgtBytes = HgtTile.ExtractHgtBytes(fileBytes, tileKey);
                    if (hgtBytes.Length > 0 && HgtTile.IsValidHgtLength(hgtBytes.Length))
                    {
                        return new HgtTile(hgtBytes, tileKey);
                    }
                }
            }

            // Search top directory for any file matching tileKey
            var matchingFiles = Directory.GetFiles(localPathOrDirectory, $"*{tileKey}*");
            foreach (var file in matchingFiles)
            {
                if (file.EndsWith(".hgt", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    progress?.Report($"Found DEM file {file}");
                    var fileBytes = await File.ReadAllBytesAsync(file).ConfigureAwait(false);
                    var hgtBytes = HgtTile.ExtractHgtBytes(fileBytes, tileKey);
                    if (hgtBytes.Length > 0 && HgtTile.IsValidHgtLength(hgtBytes.Length))
                    {
                        return new HgtTile(hgtBytes, tileKey);
                    }
                }
            }

            // Search subdirectories
            var subDirFiles = Directory.GetFiles(localPathOrDirectory, $"*{tileKey}*", SearchOption.AllDirectories);
            foreach (var file in subDirFiles)
            {
                if (file.EndsWith(".hgt", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    progress?.Report($"Found DEM file {file}");
                    var fileBytes = await File.ReadAllBytesAsync(file).ConfigureAwait(false);
                    var hgtBytes = HgtTile.ExtractHgtBytes(fileBytes, tileKey);
                    if (hgtBytes.Length > 0 && HgtTile.IsValidHgtLength(hgtBytes.Length))
                    {
                        return new HgtTile(hgtBytes, tileKey);
                    }
                }
            }

            // Search inside any zip archives in the directory
            var zipFiles = Directory.GetFiles(localPathOrDirectory, "*.zip", SearchOption.AllDirectories);
            foreach (var zipPath in zipFiles)
            {
                try
                {
                    await using var archive = await ZipFile.OpenReadAsync(zipPath);
                    var entry = archive.Entries.FirstOrDefault(e =>
                        Path.GetFileName(e.FullName).Equals($"{tileKey}.hgt", StringComparison.OrdinalIgnoreCase) ||
                        (Path.GetFileName(e.FullName).StartsWith(tileKey, StringComparison.OrdinalIgnoreCase) &&
                         e.FullName.EndsWith(".hgt", StringComparison.OrdinalIgnoreCase)));

                    if (entry != null)
                    {
                        progress?.Report($"Found {tileKey} inside {zipPath}");
                        await using var entryStream = await entry.OpenAsync();
                        using var ms = new MemoryStream();
                        await entryStream.CopyToAsync(ms).ConfigureAwait(false);
                        var hgtBytes = ms.ToArray();
                        if (hgtBytes.Length > 0 && HgtTile.IsValidHgtLength(hgtBytes.Length))
                        {
                            return new HgtTile(hgtBytes, tileKey);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Verbose(ex, "Error reading zip archive {ZipPath}", zipPath);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading static DEM tile {TileKey} from {LocalPath}", tileKey, localPathOrDirectory);
        }

        return null;
    }

    private static async Task<HgtTile?> LoadStaticDemTileFromUrl(string urlBase, string tileKey,
        IProgress<string>? progress = null)
    {
        var trimmedBase = urlBase.TrimEnd('/');

        var candidateUrls = new[]
        {
            $"{trimmedBase}/{tileKey}.hgt",
            $"{trimmedBase}/{tileKey}.zip",
            $"{trimmedBase}/{tileKey}.hgt.zip",
            $"{trimmedBase}/{tileKey.ToLowerInvariant()}.hgt",
            $"{trimmedBase}/{tileKey.ToLowerInvariant()}.zip",
            $"{trimmedBase}/{tileKey.ToLowerInvariant()}.hgt.zip",
            $"{trimmedBase}/{tileKey.ToUpperInvariant()}.HGT",
            $"{trimmedBase}/{tileKey.ToUpperInvariant()}.ZIP"
        };

        foreach (var url in candidateUrls)
        {
            try
            {
                progress?.Report($"Requesting DEM tile from {url}");
                using var response = await ElevationHttpClient.GetAsync(url).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var fileBytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    var hgtBytes = HgtTile.ExtractHgtBytes(fileBytes, tileKey);
                    if (hgtBytes.Length > 0 && HgtTile.IsValidHgtLength(hgtBytes.Length))
                    {
                        progress?.Report($"Successfully loaded DEM tile {tileKey} from {url}");
                        return new HgtTile(hgtBytes, tileKey);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Verbose(ex, "Attempt to download DEM tile from {Url} failed", url);
            }
        }

        progress?.Report($"Could not find DEM tile {tileKey} at {urlBase}");
        return null;
    }
}