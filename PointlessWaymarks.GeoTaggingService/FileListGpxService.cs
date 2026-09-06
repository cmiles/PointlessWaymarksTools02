using NetTopologySuite.IO;
using PointlessWaymarks.SpatialTools;

namespace PointlessWaymarks.GeoTaggingService;

public class FileListGpxService(List<FileInfo> listOfGpxFiles) : IGpxService
{
    private List<(DateTime startDateTime, DateTime endDateTime, FileInfo file)>? _gpxFiles;

    public async Task<List<WaypointAndSource>> GetGpxPoints(List<DateTime> photoDateTimeUtcList,
        IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (_gpxFiles == null) await ScanFiles(progress);

        //This is a brute force approach since the expectation is local files.
        List<(DateTime startDateTime, DateTime endDateTime, FileInfo file)> possibleFiles = [];

        foreach (var loopPhotoDateTime in photoDateTimeUtcList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            possibleFiles.AddRange(_gpxFiles!.Where(x =>
                loopPhotoDateTime >= x.startDateTime && loopPhotoDateTime <= x.endDateTime &&
                !possibleFiles.Any(y => x.file.FullName.Equals(y.file.FullName))).ToList());
        }

        progress?.Report($"Found {possibleFiles.Count} Gpx Files");

        if (!possibleFiles.Any())
        {
            progress?.Report("No Gpx Files Found");
            return [];
        }

        var allPointsList = new List<WaypointAndSource>();

        foreach (var loopFile in possibleFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (loopFile.file.Extension.Equals(".fit", StringComparison.OrdinalIgnoreCase))
            {
                var waypoints = await FitTools.GpxWaypointsFromFitFile(loopFile.file, progress);
                if (waypoints.Count == 0) continue;

                allPointsList.AddRange(waypoints
                    .Where(x => x.TimestampUtc is not null)
                    .Select(x => new WaypointAndSource(x, loopFile.file.Name))
                    .OrderBy(x => x.Waypoint.TimestampUtc)
                    .ToList());
            }
            else
            {
                var gpx = await GpxTools.ReadGpxFile(loopFile.file, progress);

                if (!gpx.Tracks.Any(t => t.Segments.SelectMany(y => y.Waypoints).Count() > 1)) continue;

                allPointsList.AddRange(gpx.Tracks.SelectMany(x => x.Segments).SelectMany(x => x.Waypoints)
                    .Select(x => new WaypointAndSource(x, loopFile.file.Name))
                    .OrderBy(x => x.Waypoint.TimestampUtc)
                    .ToList());
            }
        }

        progress?.Report($"Found {allPointsList.Count} Points");

        cancellationToken.ThrowIfCancellationRequested();

        return allPointsList;
    }

    public async Task ScanFiles(IProgress<string>? progress)
    {
        if (!listOfGpxFiles.Any())
        {
            progress?.Report("No GPX, FIT or TCX files?");
            _gpxFiles = [];
            return;
        }

        var filesNotPresent = listOfGpxFiles.Where(x =>
        {
            x.Refresh();
            return !x.Exists;
        }).ToList();

        if (filesNotPresent.Any())
            progress?.Report(
                $"Files found in List that are no longer present - skipping {filesNotPresent.Count} files - {string.Join(" ,", filesNotPresent.Select(x => x.FullName))}");

        var newGpxList = new List<(DateTime startDateTime, DateTime endDateTime, FileInfo file)>();

        var counter = 0;

        var existingFiles = listOfGpxFiles.Where(x => x.Exists).ToList();

        foreach (var loopFile in existingFiles)
        {
            if (++counter % 50 == 0) progress?.Report($"File List Gpx Service - {counter} of {existingFiles.Count}");

            var allPoints = new List<GpxWaypoint>();

            if (loopFile.Extension.Equals(".fit", StringComparison.OrdinalIgnoreCase))
            {
                var fitPoints = await FitTools.GpxWaypointsFromFitFile(loopFile, progress);
                allPoints.AddRange(fitPoints.Where(x => x.TimestampUtc is not null));
            }
            else
            {
                var gpx = await GpxTools.ReadGpxFile(loopFile, progress);

                allPoints.AddRange(gpx.Tracks.SelectMany(x => x.Segments).SelectMany(x => x.Waypoints)
                    .Where(x => x.TimestampUtc is not null));

                allPoints.AddRange(gpx.Waypoints.Where(x => x.TimestampUtc is not null));
            }

            if (!allPoints.Any())
            {
                progress?.Report($"File List Gpx Service - {loopFile.FullName} no points for use GeoTagging found");
                continue;
            }

            var timestampMin = allPoints.Where(x => x.TimestampUtc != null).MinBy(x => x.TimestampUtc!.Value)!
                .TimestampUtc;
            var timestampMax = allPoints.Where(x => x.TimestampUtc != null).MaxBy(x => x.TimestampUtc!.Value)!
                .TimestampUtc;

            if (timestampMin != null && timestampMax != null)
            {
                var toAdd = (timestampMin.Value,
                    timestampMax.Value, loopFile);

                newGpxList.Add(toAdd);

                progress?.Report(
                    $"File Gpx Service - {toAdd.loopFile.FullName} UTC from {toAdd.Item1} to {toAdd.Item2}");
            }
        }

        _gpxFiles = newGpxList;
    }
}