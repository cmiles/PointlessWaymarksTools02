using System.Text;
using Dynastream.Fit;
using GeoTimeZone;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Simplify;
using PointlessWaymarks.CommonTools;
using DateTime = System.DateTime;
using File = Dynastream.Fit.File;

namespace PointlessWaymarks.SpatialTools;

public static class FitTools
{
    public static int DegreesToSemicircles(double degrees)
    {
        return (int)Math.Round(degrees * (2147483648.0 / 180.0));
    }

    public static Task<bool> FitFileHasLatLong(FileInfo fitFile, IProgress<string>? progress = null)
    {
        return FitFileHasLocation(fitFile, progress);
    }

    public static async Task<bool> FitFileHasLocation(FileInfo fitFile, IProgress<string>? progress = null)
    {
        if (fitFile is not { Exists: true }) return false;

        progress?.Report($"Checking for location data in FIT file {fitFile.FullName}");

        return await Task.Run(() =>
        {
            var data = ReadFitFile(fitFile);
            if (data == null) return false;

            foreach (var rec in data.Records)
            {
                var latSemicircles = rec.GetPositionLat();
                var lonSemicircles = rec.GetPositionLong();
                if (latSemicircles == null || lonSemicircles == null) continue;
                if (latSemicircles.Value == 0x7FFFFFFF || lonSemicircles.Value == 0x7FFFFFFF) continue;

                var lat = SemicirclesToDegrees(latSemicircles.Value);
                var lon = SemicirclesToDegrees(lonSemicircles.Value);
                if (lat is >= -90 and <= 90 && lon is >= -180 and <= 180) return true;
            }

            foreach (var cp in data.CoursePoints)
            {
                var latSemicircles = cp.GetPositionLat();
                var lonSemicircles = cp.GetPositionLong();
                if (latSemicircles == null || lonSemicircles == null) continue;
                if (latSemicircles.Value == 0x7FFFFFFF || lonSemicircles.Value == 0x7FFFFFFF) continue;

                var lat = SemicirclesToDegrees(latSemicircles.Value);
                var lon = SemicirclesToDegrees(lonSemicircles.Value);
                if (lat is >= -90 and <= 90 && lon is >= -180 and <= 180) return true;
            }

            return false;
        }).ConfigureAwait(false);
    }

    public static Task<bool> FitFileHasLocationData(FileInfo fitFile, IProgress<string>? progress = null)
    {
        return FitFileHasLocation(fitFile, progress);
    }

    public static Task<bool> FitFileHasLocations(FileInfo fitFile, IProgress<string>? progress = null)
    {
        return FitFileHasLocation(fitFile, progress);
    }

    public static Task<bool> FitFileHasTrack(FileInfo fitFile, IProgress<string>? progress = null)
    {
        return FitFileHasLocation(fitFile, progress);
    }

    public static Task<List<GpxWaypoint>> GpxWaypointsFromFit(FileInfo fitFile, IProgress<string>? progress = null)
    {
        return GpxWaypointsFromFitFile(fitFile, progress);
    }

    public static async Task<List<GpxWaypoint>> GpxWaypointsFromFitFile(FileInfo fitFile,
        IProgress<string>? progress = null)
    {
        if (fitFile is not { Exists: true }) return [];

        progress?.Report($"Reading waypoints/records from FIT file {fitFile.FullName}");

        return await Task.Run(() =>
        {
            var data = ReadFitFile(fitFile);
            if (data == null) return [];

            var waypoints = new List<GpxWaypoint>();

            foreach (var rec in data.Records)
            {
                var latSemicircles = rec.GetPositionLat();
                var lonSemicircles = rec.GetPositionLong();
                if (latSemicircles == null || lonSemicircles == null) continue;
                if (latSemicircles.Value == 0x7FFFFFFF || lonSemicircles.Value == 0x7FFFFFFF) continue;

                var lat = SemicirclesToDegrees(latSemicircles.Value);
                var lon = SemicirclesToDegrees(lonSemicircles.Value);
                if (lat < -90 || lat > 90 || lon < -180 || lon > 180) continue;

                var altitude = rec.GetEnhancedAltitude() ?? rec.GetAltitude();
                double? elevation = altitude != null ? (double)altitude.Value : null;

                var dt = rec.GetTimestamp()?.GetDateTime();
                DateTime? utcTimestamp = dt != null ? DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc) : null;

                var wp = new GpxWaypoint(new GpxLongitude(lon), new GpxLatitude(lat), elevation);
                if (utcTimestamp != null) wp = wp.WithTimestampUtc(utcTimestamp);

                waypoints.Add(wp);
            }

            foreach (var cp in data.CoursePoints)
            {
                var latSemicircles = cp.GetPositionLat();
                var lonSemicircles = cp.GetPositionLong();
                if (latSemicircles == null || lonSemicircles == null) continue;
                if (latSemicircles.Value == 0x7FFFFFFF || lonSemicircles.Value == 0x7FFFFFFF) continue;

                var lat = SemicirclesToDegrees(latSemicircles.Value);
                var lon = SemicirclesToDegrees(lonSemicircles.Value);
                if (lat < -90 || lat > 90 || lon < -180 || lon > 180) continue;

                var dt = cp.GetTimestamp()?.GetDateTime();
                DateTime? utcTimestamp = dt != null ? DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc) : null;

                var nameBytes = cp.GetName();
                var name = nameBytes != null
                    ? Encoding.UTF8.GetString(nameBytes).TrimEnd('\0', ' ')
                    : string.Empty;

                var pointType = cp.GetType();
                var typeString = pointType switch
                {
                    var t when t != null && t != CoursePoint.Invalid => t.ToString() ?? string.Empty,
                    _ => string.Empty
                };

                if (string.IsNullOrWhiteSpace(name)) name = typeString;

                var wp = new GpxWaypoint(new GpxLongitude(lon), new GpxLatitude(lat), null);
                if (utcTimestamp != null) wp = wp.WithTimestampUtc(utcTimestamp);

                if (!string.IsNullOrWhiteSpace(name)) wp = wp.WithName(name);

                if (!string.IsNullOrWhiteSpace(typeString)) wp = wp.WithDescription(typeString);

                waypoints.Add(wp);
            }

            return waypoints;
        }).ConfigureAwait(false);
    }

    public static Feature LineFeatureFromFitTrack(GpsTrackInformation trackInformation)
    {
        // ReSharper disable once CoVariantArrayConversion
        var newLine = new LineString(trackInformation.Track.ToArray());
        var feature = new Feature
        {
            Geometry = newLine,
            BoundingBox = GeoJsonTools.GeometryBoundingBox([newLine]),
            Attributes = new AttributesTable()
        };

        feature.Attributes.Add("title", trackInformation.Name);
        feature.Attributes.Add("description", trackInformation.Description);

        return feature;
    }

    public static Feature LineFeatureFromFitTrackBuffered(GpsTrackInformation trackInformation,
        double bufferInFeet)
    {
        if (bufferInFeet <= 0) return LineFeatureFromFitTrack(trackInformation);

        // ReSharper disable once CoVariantArrayConversion
        var newLine = new LineString(trackInformation.Track.ToArray());

        var latitudeDegrees = DistanceTools.ApproximateMetersToLatitudeDegrees(bufferInFeet.FeetToMeters(),
            newLine.StartPoint.X, newLine.StartPoint.Y);
        var longitudeDegrees
            = DistanceTools.ApproximateMetersToLongitudeDegrees(bufferInFeet.FeetToMeters(), newLine.StartPoint.X,
                newLine.StartPoint.Y);

        var bufferedLine = newLine.Buffer((latitudeDegrees + longitudeDegrees) / 2D);

        var feature = new Feature
        {
            Geometry = bufferedLine,
            BoundingBox = GeoJsonTools.GeometryBoundingBox([newLine]),
            Attributes = new AttributesTable()
        };

        feature.Attributes.Add("title", trackInformation.Name);
        feature.Attributes.Add("description", trackInformation.Description);

        return feature;
    }

    private static FitFileData? ReadFitFile(FileInfo fitFile)
    {
        if (fitFile is not { Exists: true }) return null;
        if (fitFile.Length == 0) return null;

        try
        {
            using var stream = fitFile.OpenRead();
            var decode = new Decode();
            var broadcaster = new MesgBroadcaster();
            var data = new FitFileData();

            broadcaster.FileIdMesgEvent += (_, e) => data.FileId = new FileIdMesg(e.mesg);
            broadcaster.CourseMesgEvent += (_, e) => data.Course = new CourseMesg(e.mesg);
            broadcaster.CoursePointMesgEvent += (_, e) => data.CoursePoints.Add(new CoursePointMesg(e.mesg));
            broadcaster.SessionMesgEvent += (_, e) => data.Sessions.Add(new SessionMesg(e.mesg));
            broadcaster.LapMesgEvent += (_, e) => data.Laps.Add(new LapMesg(e.mesg));
            broadcaster.RecordMesgEvent += (_, e) => data.Records.Add(new RecordMesg(e.mesg));

            decode.MesgEvent += broadcaster.OnMesg;
            decode.MesgDefinitionEvent += broadcaster.OnMesgDefinition;

            if (!decode.IsFIT(stream)) return null;
            stream.Position = 0;
            if (!decode.CheckIntegrity(stream)) return null;
            stream.Position = 0;

            decode.Read(stream);
            return data;
        }
        catch
        {
            return null;
        }
    }

    public static double SemicirclesToDegrees(int semicircles)
    {
        return semicircles * (180.0 / 2147483648.0);
    }

    public static Task<GpsTrackInformation?> TrackInformationFromFit(FileInfo fitFile,
        IProgress<string>? progress = null)
    {
        return TrackInformationFromFitFile(fitFile, progress);
    }

    public static async Task<GpsTrackInformation?> TrackInformationFromFitFile(FileInfo fitFile,
        IProgress<string>? progress = null)
    {
        if (fitFile is not { Exists: true }) return null;

        progress?.Report($"Reading FIT file {fitFile.FullName}");

        return await Task.Run(() =>
        {
            var data = ReadFitFile(fitFile);
            if (data == null) return null;

            var validRecords = new List<(RecordMesg record, double lat, double lon, double elevation)>();
            foreach (var rec in data.Records)
            {
                var latSemicircles = rec.GetPositionLat();
                var lonSemicircles = rec.GetPositionLong();
                if (latSemicircles == null || lonSemicircles == null) continue;
                if (latSemicircles.Value == 0x7FFFFFFF || lonSemicircles.Value == 0x7FFFFFFF) continue;

                var lat = SemicirclesToDegrees(latSemicircles.Value);
                var lon = SemicirclesToDegrees(lonSemicircles.Value);
                if (lat < -90 || lat > 90 || lon < -180 || lon > 180) continue;

                var elevation = (double)(rec.GetEnhancedAltitude() ?? rec.GetAltitude() ?? 0f);
                validRecords.Add((rec, lat, lon, elevation));
            }

            if (validRecords.Count == 0 && data.CoursePoints.Count > 0)
                foreach (var cp in data.CoursePoints)
                {
                    var latSemicircles = cp.GetPositionLat();
                    var lonSemicircles = cp.GetPositionLong();
                    if (latSemicircles == null || lonSemicircles == null) continue;
                    if (latSemicircles.Value == 0x7FFFFFFF || lonSemicircles.Value == 0x7FFFFFFF) continue;

                    var lat = SemicirclesToDegrees(latSemicircles.Value);
                    var lon = SemicirclesToDegrees(lonSemicircles.Value);
                    if (lat < -90 || lat > 90 || lon < -180 || lon > 180) continue;

                    validRecords.Add((new RecordMesg(), lat, lon, 0));
                }

            if (validRecords.Count == 0) return null;

            var pointList = validRecords.Select(r => new CoordinateZ(r.lon, r.lat, r.elevation)).ToList();

            var firstTimestampRec = validRecords.FirstOrDefault(r => r.record.GetTimestamp() != null);
            var lastTimestampRec = validRecords.LastOrDefault(r => r.record.GetTimestamp() != null);

            DateTime? startDateTimeUtc = null;
            DateTime? endDateTimeUtc = null;
            DateTime? startDateTimeLocal = null;
            DateTime? endDateTimeLocal = null;

            var session = data.Sessions.FirstOrDefault();
            var sessionStartTime = session?.GetStartTime()?.GetDateTime();
            var sessionEndTime = session?.GetTimestamp()?.GetDateTime();

            if (sessionStartTime != null)
                startDateTimeUtc = DateTime.SpecifyKind(sessionStartTime.Value, DateTimeKind.Utc);
            else if (firstTimestampRec.record != null)
                startDateTimeUtc = DateTime.SpecifyKind(firstTimestampRec.record.GetTimestamp()!.GetDateTime(),
                    DateTimeKind.Utc);

            if (sessionEndTime != null)
                endDateTimeUtc = DateTime.SpecifyKind(sessionEndTime.Value, DateTimeKind.Utc);
            else if (lastTimestampRec.record != null)
                endDateTimeUtc = DateTime.SpecifyKind(lastTimestampRec.record.GetTimestamp()!.GetDateTime(),
                    DateTimeKind.Utc);

            if (startDateTimeUtc != null)
                try
                {
                    var startTimezoneIanaIdentifier =
                        TimeZoneLookup.GetTimeZone(validRecords[0].lat, validRecords[0].lon);
                    var startTimeZone = TimeZoneInfo.FindSystemTimeZoneById(startTimezoneIanaIdentifier.Result);
                    var startUtcOffset = startTimeZone.GetUtcOffset(startDateTimeUtc.Value);
                    startDateTimeLocal = startDateTimeUtc.Value.Add(startUtcOffset);
                }
                catch
                {
                    startDateTimeLocal = startDateTimeUtc.Value.ToLocalTime();
                }

            if (endDateTimeUtc != null)
                try
                {
                    var endTimezoneIanaIdentifier =
                        TimeZoneLookup.GetTimeZone(validRecords[^1].lat, validRecords[^1].lon);
                    var endTimeZone = TimeZoneInfo.FindSystemTimeZoneById(endTimezoneIanaIdentifier.Result);
                    var endUtcOffset = endTimeZone.GetUtcOffset(endDateTimeUtc.Value);
                    endDateTimeLocal = endDateTimeUtc.Value.Add(endUtcOffset);
                }
                catch
                {
                    endDateTimeLocal = endDateTimeUtc.Value.ToLocalTime();
                }

            var sport = session?.GetSport();
            var sportName = sport switch
            {
                Sport.Running => "Run",
                Sport.Cycling => "Bike",
                Sport.Hiking => "Hike",
                Sport.Walking => "Walk",
                Sport.Swimming => "Swim",
                Sport.FitnessEquipment or Sport.Training => "Strength",
                var s when s != null && s != Sport.Generic && s != Sport.Invalid => s.ToString() ?? "Activity",
                _ => "Activity"
            };

            var nameParts = new List<string> { sportName, startDateTimeLocal?.ToString("M/d/yyyy") ?? string.Empty }
                .Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            var name = string.Join(" - ", nameParts);
            if (string.IsNullOrWhiteSpace(name)) name = Path.GetFileNameWithoutExtension(fitFile.Name);

            var stats = DistanceTools.LineStatsInImperialFromCoordinateList(pointList);
            var descriptionParts = new List<string>();

            var durationSec = session?.GetTotalTimerTime() ?? session?.GetTotalElapsedTime();
            if (durationSec is > 0)
            {
                var durationSpan = TimeSpan.FromSeconds(durationSec.Value);
                descriptionParts.Add(durationSpan.TotalHours >= 1
                    ? $"Duration: {(int)durationSpan.TotalHours}:{durationSpan.Minutes:D2}:{durationSpan.Seconds:D2}"
                    : $"Duration: {durationSpan.Minutes}:{durationSpan.Seconds:D2}");
            }

            var distanceMiles = session?.GetTotalDistance() is { } d and > 0
                ? ((double)d).MetersToMiles()
                : stats.Length;
            if (distanceMiles > 0) descriptionParts.Add($"Distance: {distanceMiles:0.##} mi");

            var climbFeet = session?.GetTotalAscent() is { } a and > 0
                ? ((double)a).MetersToFeet()
                : stats.ElevationClimb;
            if (climbFeet > 0) descriptionParts.Add($"Climb: {Math.Round(climbFeet):N0} ft");

            var descentFeet = session?.GetTotalDescent() is { } desc and > 0
                ? ((double)desc).MetersToFeet()
                : stats.ElevationDescent;
            if (descentFeet > 0) descriptionParts.Add($"Descent: {Math.Round(descentFeet):N0} ft");

            if (session?.GetTotalCalories() is { } cal and > 0) descriptionParts.Add($"Calories: {cal}");

            var description = string.Join(", ", descriptionParts);

            return new GpsTrackInformation(name, description, startDateTimeLocal, endDateTimeLocal,
                startDateTimeUtc, endDateTimeUtc, pointList);
        }).ConfigureAwait(false);
    }

    public static Task<GpsTrackInformation?> TrackInformationSimplifiedFromFit(FileInfo fitFile,
        IProgress<string>? progress = null)
    {
        return TrackInformationSimplifiedFromFitFile(fitFile, progress);
    }

    /// <summary>
    /// The Simplified version of a Track from a Fit File: eliminates zero altititude points when reasonable and
    /// uses RDP to simplify the track. This is a reaction to reviewing fit data where the device is outputing
    /// a record every second but may output without altitude - FIT files have a different priority and persepctive than old
    /// school gps output since frequent heart rate and other sensor data can be valuable without position and
    /// location.
    /// </summary>
    /// <param name="fitFile"></param>
    /// <param name="progress"></param>
    /// <returns></returns>
    public static async Task<GpsTrackInformation?> TrackInformationSimplifiedFromFitFile(FileInfo fitFile,
        IProgress<string>? progress = null)
    {
        if (fitFile is not { Exists: true }) return null;

        progress?.Report($"Reading FIT file {fitFile.FullName}");

        var data = ReadFitFile(fitFile);
        if (data == null) return null;

        var parsedRecords = new List<(RecordMesg record, double lat, double lon, double? elevation)>();

        foreach (var rec in data.Records)
        {
            var latSemicircles = rec.GetPositionLat();
            var lonSemicircles = rec.GetPositionLong();
            if (latSemicircles == null || lonSemicircles == null) continue;
            if (latSemicircles.Value == 0x7FFFFFFF || lonSemicircles.Value == 0x7FFFFFFF) continue;

            var lat = SemicirclesToDegrees(latSemicircles.Value);
            var lon = SemicirclesToDegrees(lonSemicircles.Value);
            if (lat < -90 || lat > 90 || lon < -180 || lon > 180) continue;

            double? elevation = null;
            if (rec.GetEnhancedAltitude() != null)
                elevation = rec.GetEnhancedAltitude();
            else if (rec.GetAltitude() != null)
                elevation = rec.GetAltitude();

            parsedRecords.Add((rec, lat, lon, elevation));
        }

        if (parsedRecords.Count == 0 && data.CoursePoints.Count > 0)
            foreach (var cp in data.CoursePoints)
            {
                var latSemicircles = cp.GetPositionLat();
                var lonSemicircles = cp.GetPositionLong();
                if (latSemicircles == null || lonSemicircles == null) continue;
                if (latSemicircles.Value == 0x7FFFFFFF || lonSemicircles.Value == 0x7FFFFFFF) continue;

                var lat = SemicirclesToDegrees(latSemicircles.Value);
                var lon = SemicirclesToDegrees(lonSemicircles.Value);
                if (lat < -90 || lat > 90 || lon < -180 || lon > 180) continue;

                parsedRecords.Add((new RecordMesg(), lat, lon, 0));
            }

        if (parsedRecords.Count == 0) return null;

        // --- 1. FILTER MISSING ELEVATION POINTS & BUILD PARALLEL LISTS ---
        var filteredRecords = new List<CoordinateZ>();
        var filteredTuples = new List<(RecordMesg record, double lat, double lon, double? elevation)>();

        var pointsRequiringElevation = new List<CoordinateZ>();
        var indicesRequiringElevation = new List<int>();

        var distanceThresholdMeters = 15.0;

        for (var i = 0; i < parsedRecords.Count; i++)
        {
            var current = parsedRecords[i];

            if (current.elevation.HasValue)
            {
                filteredRecords.Add(new CoordinateZ(current.lon, current.lat, current.elevation.Value));
                filteredTuples.Add(current);
                continue;
            }

            var isNearValidPoint = false;

            for (var j = i - 1; j >= 0; j--)
                if (parsedRecords[j].elevation.HasValue)
                {
                    if (DistanceTools.GetDistanceInMeters(current.lon, current.lat, parsedRecords[j].lon,
                            parsedRecords[j].lat) <= distanceThresholdMeters)
                        isNearValidPoint = true;
                    break;
                }

            if (!isNearValidPoint)
                for (var j = i + 1; j < parsedRecords.Count; j++)
                    if (parsedRecords[j].elevation.HasValue)
                    {
                        if (DistanceTools.GetDistanceInMeters(current.lon, current.lat, parsedRecords[j].lon,
                                parsedRecords[j].lat) <= distanceThresholdMeters)
                            isNearValidPoint = true;
                        break;
                    }

            if (!isNearValidPoint)
            {
                var isolatedPoint = new CoordinateZ(current.lon, current.lat, 0);
                filteredRecords.Add(isolatedPoint);
                filteredTuples.Add(current);

                pointsRequiringElevation.Add(isolatedPoint);
                indicesRequiringElevation.Add(filteredTuples.Count - 1);
            }
        }

        // --- 2. FETCH MISSING ELEVATIONS VIA SERVICE ---
        if (pointsRequiringElevation.Count > 0)
        {
            var updatedPoints = await ElevationService.OpenTopoNedElevation(pointsRequiringElevation, null);

            if (updatedPoints.Count == pointsRequiringElevation.Count)
                for (var i = 0; i < pointsRequiringElevation.Count; i++)
                {
                    pointsRequiringElevation[i].Z = updatedPoints[i].Z;

                    var tupleIndex = indicesRequiringElevation[i];
                    var oldTuple = filteredTuples[tupleIndex];
                    filteredTuples[tupleIndex] = (oldTuple.record, oldTuple.lat, oldTuple.lon, updatedPoints[i].Z);
                }
        }

        // --- 3. RDP SIMPLIFICATION VIA NTS ---
        var rdpToleranceDegrees = 0.00002;
        var simplifiedPoints =
            DouglasPeuckerLineSimplifier.Simplify([.. filteredRecords],
                rdpToleranceDegrees);

        var pointList = simplifiedPoints.Select(r => new CoordinateZ(r.X, r.Y, r.Z)).ToList();

        var firstTimestampRec = parsedRecords.FirstOrDefault(r => r.record.GetTimestamp() != null);
        var lastTimestampRec = parsedRecords.LastOrDefault(r => r.record.GetTimestamp() != null);

        DateTime? startDateTimeUtc = null;
        DateTime? endDateTimeUtc = null;
        DateTime? startDateTimeLocal = null;
        DateTime? endDateTimeLocal = null;

        var session = data.Sessions.FirstOrDefault();
        var sessionStartTime = session?.GetStartTime()?.GetDateTime();
        var sessionEndTime = session?.GetTimestamp()?.GetDateTime();

        if (sessionStartTime != null)
            startDateTimeUtc = DateTime.SpecifyKind(sessionStartTime.Value, DateTimeKind.Utc);
        else if (firstTimestampRec.record != null)
            startDateTimeUtc =
                DateTime.SpecifyKind(firstTimestampRec.record.GetTimestamp()!.GetDateTime(), DateTimeKind.Utc);

        if (sessionEndTime != null)
            endDateTimeUtc = DateTime.SpecifyKind(sessionEndTime.Value, DateTimeKind.Utc);
        else if (lastTimestampRec.record != null)
            endDateTimeUtc =
                DateTime.SpecifyKind(lastTimestampRec.record.GetTimestamp()!.GetDateTime(), DateTimeKind.Utc);

        if (startDateTimeUtc != null)
            try
            {
                var startTimezoneIanaIdentifier = TimeZoneLookup.GetTimeZone(parsedRecords[0].lat, parsedRecords[0].lon);
                var startTimeZone = TimeZoneInfo.FindSystemTimeZoneById(startTimezoneIanaIdentifier.Result);
                var startUtcOffset = startTimeZone.GetUtcOffset(startDateTimeUtc.Value);
                startDateTimeLocal = startDateTimeUtc.Value.Add(startUtcOffset);
            }
            catch
            {
                startDateTimeLocal = startDateTimeUtc.Value.ToLocalTime();
            }

        if (endDateTimeUtc != null)
            try
            {
                var endTimezoneIanaIdentifier = TimeZoneLookup.GetTimeZone(parsedRecords[^1].lat, parsedRecords[^1].lon);
                var endTimeZone = TimeZoneInfo.FindSystemTimeZoneById(endTimezoneIanaIdentifier.Result);
                var endUtcOffset = endTimeZone.GetUtcOffset(endDateTimeUtc.Value);
                endDateTimeLocal = endDateTimeUtc.Value.Add(endUtcOffset);
            }
            catch
            {
                endDateTimeLocal = endDateTimeUtc.Value.ToLocalTime();
            }

        var sport = session?.GetSport();
        var sportName = sport switch
        {
            Sport.Running => "Run",
            Sport.Cycling => "Bike",
            Sport.Hiking => "Hike",
            Sport.Walking => "Walk",
            Sport.Swimming => "Swim",
            Sport.FitnessEquipment or Sport.Training => "Strength",
            var s when s != null && s != Sport.Generic && s != Sport.Invalid => s.ToString() ?? "Activity",
            _ => "Activity"
        };

        var nameParts = new List<string> { sportName, startDateTimeLocal?.ToString("M/d/yyyy") ?? string.Empty }
            .Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        var name = string.Join(" - ", nameParts);
        if (string.IsNullOrWhiteSpace(name)) name = Path.GetFileNameWithoutExtension(fitFile.Name);

        var stats = DistanceTools.LineStatsInImperialFromCoordinateList(pointList);
        var descriptionParts = new List<string>();

        var durationSec = session?.GetTotalTimerTime() ?? session?.GetTotalElapsedTime();
        if (durationSec is > 0)
        {
            var durationSpan = TimeSpan.FromSeconds(durationSec.Value);
            descriptionParts.Add(durationSpan.TotalHours >= 1
                ? $"Duration: {(int)durationSpan.TotalHours}:{durationSpan.Minutes:D2}:{durationSpan.Seconds:D2}"
                : $"Duration: {durationSpan.Minutes}:{durationSpan.Seconds:D2}");
        }

        var distanceMiles = session?.GetTotalDistance() is { } d and > 0
            ? ((double)d).MetersToMiles()
            : stats.Length;
        if (distanceMiles > 0) descriptionParts.Add($"Distance: {distanceMiles:0.##} mi");

        var climbFeet = session?.GetTotalAscent() is { } a and > 0
            ? ((double)a).MetersToFeet()
            : stats.ElevationClimb;
        if (climbFeet > 0) descriptionParts.Add($"Climb: {Math.Round(climbFeet):N0} ft");

        var descentFeet = session?.GetTotalDescent() is { } desc and > 0
            ? ((double)desc).MetersToFeet()
            : stats.ElevationDescent;
        if (descentFeet > 0) descriptionParts.Add($"Descent: {Math.Round(descentFeet):N0} ft");

        if (session?.GetTotalCalories() is { } cal and > 0) descriptionParts.Add($"Calories: {cal}");

        var description = string.Join(", ", descriptionParts);

        return new GpsTrackInformation(name, description, startDateTimeLocal, endDateTimeLocal,
            startDateTimeUtc, endDateTimeUtc, pointList);
    }

    public static Task<Feature?> TrackLineFromFit(FileInfo fitFile, IProgress<string>? progress = null)
    {
        return TrackLineFromFitFile(fitFile, progress);
    }

    public static Task<GpxTools.FeatureAndBufferedFeature?> TrackLineFromFitBuffered(FileInfo fitFile,
        double bufferInFeet, IProgress<string>? progress = null)
    {
        return TrackLineFromFitFileBuffered(fitFile, bufferInFeet, progress);
    }

    public static async Task<Feature?> TrackLineFromFitFile(FileInfo fitFile,
        IProgress<string>? progress = null)
    {
        var trackInfo = await TrackInformationFromFitFile(fitFile, progress).ConfigureAwait(false);
        return trackInfo == null ? null : LineFeatureFromFitTrack(trackInfo);
    }

    public static async Task<GpxTools.FeatureAndBufferedFeature?> TrackLineFromFitFileBuffered(
        FileInfo fitFile,
        double bufferInFeet, IProgress<string>? progress = null)
    {
        var trackInfo = await TrackInformationFromFitFile(fitFile, progress).ConfigureAwait(false);
        if (trackInfo == null) return null;

        return new GpxTools.FeatureAndBufferedFeature(
            LineFeatureFromFitTrack(trackInfo),
            LineFeatureFromFitTrackBuffered(trackInfo, bufferInFeet));
    }

    public static Task<Feature?> TrackLineSimplifiedFromFit(FileInfo fitFile, IProgress<string>? progress = null)
    {
        return TrackLineSimplifiedFromFitFile(fitFile, progress);
    }

    public static Task<GpxTools.FeatureAndBufferedFeature?> TrackLineSimplifiedFromFitBuffered(FileInfo fitFile,
        double bufferInFeet, IProgress<string>? progress = null)
    {
        return TrackLineSimplifiedFromFitFileBuffered(fitFile, bufferInFeet, progress);
    }

    public static async Task<Feature?> TrackLineSimplifiedFromFitFile(FileInfo fitFile,
        IProgress<string>? progress = null)
    {
        var trackInfo = await TrackInformationSimplifiedFromFitFile(fitFile, progress).ConfigureAwait(false);
        return trackInfo == null ? null : LineFeatureFromFitTrack(trackInfo);
    }

    public static async Task<GpxTools.FeatureAndBufferedFeature?> TrackLineSimplifiedFromFitFileBuffered(
        FileInfo fitFile,
        double bufferInFeet, IProgress<string>? progress = null)
    {
        var trackInfo = await TrackInformationSimplifiedFromFitFile(fitFile, progress).ConfigureAwait(false);
        if (trackInfo == null) return null;

        return new GpxTools.FeatureAndBufferedFeature(
            LineFeatureFromFitTrack(trackInfo),
            LineFeatureFromFitTrackBuffered(trackInfo, bufferInFeet));
    }

    public static async Task<(List<Feature> features, Envelope boundingBox)> WaypointPointsFromFitFile(FileInfo fitFile)
    {
        if (fitFile is not { Exists: true }) return ([], new Envelope());

        return await Task.Run(() =>
        {
            var data = ReadFitFile(fitFile);
            if (data == null) return (new List<Feature>(), new Envelope());

            var returnList = new List<Feature>();
            var bounds = new Envelope();

            foreach (var loopWaypoint in data.CoursePoints)
            {
                var latSemicircles = loopWaypoint.GetPositionLat();
                var lonSemicircles = loopWaypoint.GetPositionLong();
                if (latSemicircles == null || lonSemicircles == null) continue;
                if (latSemicircles.Value == 0x7FFFFFFF || lonSemicircles.Value == 0x7FFFFFFF) continue;

                var lat = SemicirclesToDegrees(latSemicircles.Value);
                var lon = SemicirclesToDegrees(lonSemicircles.Value);
                if (lat < -90 || lat > 90 || lon < -180 || lon > 180) continue;

                var nameBytes = loopWaypoint.GetName();
                var name = nameBytes != null
                    ? Encoding.UTF8.GetString(nameBytes).TrimEnd('\0', ' ')
                    : string.Empty;

                var pointType = loopWaypoint.GetType();
                var typeString = pointType switch
                {
                    var t when t != null && t != CoursePoint.Invalid => t.ToString() ?? string.Empty,
                    _ => string.Empty
                };

                if (string.IsNullOrWhiteSpace(name)) name = typeString;
                if (string.IsNullOrWhiteSpace(name)) name = "Waypoint";

                var attributeTable = new AttributesTable
                {
                    { "title", name },
                    { "description", typeString }
                };

                var point = PointTools.Wgs84Point(lon, lat);
                returnList.Add(new Feature(point, attributeTable));
                bounds.ExpandToInclude(point.Coordinate);
            }

            return (returnList, bounds);
        }).ConfigureAwait(false);
    }

    public static async Task<(List<Feature> features, Envelope boundingBox)> WaypointPointsFromFitFileAs2DCircles(
        FileInfo fitFile, int? bufferRadiusInFeet)
    {
        if (fitFile is not { Exists: true }) return ([], new Envelope());

        return await Task.Run(() =>
        {
            var data = ReadFitFile(fitFile);
            if (data == null) return (new List<Feature>(), new Envelope());

            var returnList = new List<Feature>();
            var bounds = new Envelope();

            foreach (var loopWaypoint in data.CoursePoints)
            {
                var latSemicircles = loopWaypoint.GetPositionLat();
                var lonSemicircles = loopWaypoint.GetPositionLong();
                if (latSemicircles == null || lonSemicircles == null) continue;
                if (latSemicircles.Value == 0x7FFFFFFF || lonSemicircles.Value == 0x7FFFFFFF) continue;

                var lat = SemicirclesToDegrees(latSemicircles.Value);
                var lon = SemicirclesToDegrees(lonSemicircles.Value);
                if (lat < -90 || lat > 90 || lon < -180 || lon > 180) continue;

                var nameBytes = loopWaypoint.GetName();
                var name = nameBytes != null
                    ? Encoding.UTF8.GetString(nameBytes).TrimEnd('\0', ' ')
                    : string.Empty;

                var pointType = loopWaypoint.GetType();
                var typeString = pointType switch
                {
                    var t when t != null && t != CoursePoint.Invalid => t.ToString() ?? string.Empty,
                    _ => string.Empty
                };

                if (string.IsNullOrWhiteSpace(name)) name = typeString;
                if (string.IsNullOrWhiteSpace(name)) name = "Waypoint";

                var attributeTable = new AttributesTable
                {
                    { "title", name },
                    { "description", typeString }
                };

                var point = PointTools.Wgs84Point(lon, lat);
                returnList.Add(new Feature(
                    bufferRadiusInFeet > 0 ? PointTools.CreateCircle(point, bufferRadiusInFeet.Value) : point,
                    attributeTable));
                bounds.ExpandToInclude(point.Coordinate);
            }

            return (returnList, bounds);
        }).ConfigureAwait(false);
    }

    /// <summary>
    ///     Writes line features as an Activity FIT file to the specified Stream.
    /// </summary>
    public static void WriteActivityFitFile(Stream stream,
        IEnumerable<(IFeature line, DateTime? utcStart, string name, string description)> lines)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(lines);

        var validLines = lines
            .Where(x => x.line?.Geometry?.Coordinates is { Length: > 0 })
            .ToList();

        if (validLines.Count == 0) return;

        var encode = new Encode(ProtocolVersion.V20);
        encode.Open(stream);

        var firstValidStart = validLines.FirstOrDefault(x => x.utcStart.HasValue).utcStart;
        var fileCreationTime = DateTime.SpecifyKind(firstValidStart ?? DateTime.UtcNow, DateTimeKind.Utc);

        var fileId = new FileIdMesg();
        fileId.SetType(File.Activity);
        fileId.SetManufacturer(Manufacturer.Development);
        fileId.SetProduct(0);
        fileId.SetTimeCreated(new Dynastream.Fit.DateTime(fileCreationTime));
        encode.Write(fileId);

        var currentClock = fileCreationTime;
        var overallStartTime = currentClock;
        var overallCumulativeDistanceMeters = 0.0;
        var overallAscentMeters = 0.0;
        var overallDescentMeters = 0.0;
        var allRecords = new List<RecordMesg>();
        var allLaps = new List<LapMesg>();

        foreach (var (line, utcStart, _, _) in validLines)
        {
            var coords = line.Geometry.Coordinates;
            if (coords == null || coords.Length == 0) continue;

            if (utcStart.HasValue)
            {
                var lineUtcStart = DateTime.SpecifyKind(utcStart.Value, DateTimeKind.Utc);
                if (lineUtcStart >= currentClock)
                    currentClock = lineUtcStart;
            }

            var lapStartTime = currentClock;
            var lapStartDistance = overallCumulativeDistanceMeters;
            var lapAscentMeters = 0.0;
            var lapDescentMeters = 0.0;

            for (var i = 0; i < coords.Length; i++)
            {
                var coord = coords[i];
                if (i > 0)
                {
                    var prevCoord = coords[i - 1];
                    var distMeters = DistanceTools.GetDistanceInMeters(prevCoord.X, prevCoord.Y, coord.X, coord.Y);
                    var distMiles = distMeters.MetersToMiles();
                    var secondsToAdd = Math.Max(TimeSpan.FromHours(distMiles / 2.0).TotalSeconds, 1.0);
                    currentClock = currentClock.AddSeconds(secondsToAdd);
                    overallCumulativeDistanceMeters += distMeters;

                    if (!double.IsNaN(prevCoord.Z) && !double.IsNaN(coord.Z))
                    {
                        var elevDiff = coord.Z - prevCoord.Z;
                        if (elevDiff > 0)
                        {
                            lapAscentMeters += elevDiff;
                            overallAscentMeters += elevDiff;
                        }
                        else if (elevDiff < 0)
                        {
                            lapDescentMeters += Math.Abs(elevDiff);
                            overallDescentMeters += Math.Abs(elevDiff);
                        }
                    }
                }

                var record = new RecordMesg();
                record.SetTimestamp(new Dynastream.Fit.DateTime(currentClock));
                record.SetPositionLat(DegreesToSemicircles(coord.Y));
                record.SetPositionLong(DegreesToSemicircles(coord.X));
                if (!double.IsNaN(coord.Z))
                {
                    record.SetEnhancedAltitude((float)coord.Z);
                    record.SetAltitude((float)coord.Z);
                }

                record.SetDistance((float)overallCumulativeDistanceMeters);
                allRecords.Add(record);
            }

            var lapEndTime = currentClock;
            var lapElapsedSeconds = (float)(lapEndTime - lapStartTime).TotalSeconds;
            var lapDistanceMeters = (float)(overallCumulativeDistanceMeters - lapStartDistance);

            var lap = new LapMesg();
            lap.SetStartTime(new Dynastream.Fit.DateTime(lapStartTime));
            lap.SetTimestamp(new Dynastream.Fit.DateTime(lapEndTime));
            lap.SetTotalElapsedTime(lapElapsedSeconds);
            lap.SetTotalTimerTime(lapElapsedSeconds);
            lap.SetTotalDistance(lapDistanceMeters);
            lap.SetTotalAscent((ushort)Math.Min(ushort.MaxValue, Math.Max(0, Math.Round(lapAscentMeters))));
            lap.SetTotalDescent((ushort)Math.Min(ushort.MaxValue, Math.Max(0, Math.Round(lapDescentMeters))));
            lap.SetStartPositionLat(DegreesToSemicircles(coords[0].Y));
            lap.SetStartPositionLong(DegreesToSemicircles(coords[0].X));
            lap.SetEndPositionLat(DegreesToSemicircles(coords[^1].Y));
            lap.SetEndPositionLong(DegreesToSemicircles(coords[^1].X));
            lap.SetSport(Sport.Generic);
            allLaps.Add(lap);
        }

        var overallEndTime = currentClock;
        var overallElapsedSeconds = (float)(overallEndTime - overallStartTime).TotalSeconds;

        var session = new SessionMesg();
        session.SetSport(Sport.Generic);
        session.SetStartTime(new Dynastream.Fit.DateTime(overallStartTime));
        session.SetTimestamp(new Dynastream.Fit.DateTime(overallEndTime));
        session.SetTotalElapsedTime(overallElapsedSeconds);
        session.SetTotalTimerTime(overallElapsedSeconds);
        session.SetTotalDistance((float)overallCumulativeDistanceMeters);
        session.SetTotalAscent((ushort)Math.Min(ushort.MaxValue, Math.Max(0, Math.Round(overallAscentMeters))));
        session.SetTotalDescent((ushort)Math.Min(ushort.MaxValue, Math.Max(0, Math.Round(overallDescentMeters))));
        session.SetFirstLapIndex(0);
        session.SetNumLaps((ushort)allLaps.Count);

        var activity = new ActivityMesg();
        activity.SetTimestamp(new Dynastream.Fit.DateTime(overallEndTime));
        activity.SetTotalTimerTime(overallElapsedSeconds);
        activity.SetNumSessions(1);
        activity.SetType(Activity.Manual);

        encode.Write(session);
        foreach (var lap in allLaps) encode.Write(lap);

        foreach (var record in allRecords) encode.Write(record);

        encode.Write(activity);

        encode.Close();
    }

    /// <summary>
    ///     Writes line features as an Activity FIT file to the specified FileInfo.
    /// </summary>
    public static void WriteActivityFitFile(FileInfo fitFile,
        IEnumerable<(IFeature line, DateTime? utcStart, string name, string description)> lines)
    {
        ArgumentNullException.ThrowIfNull(fitFile);
        using var stream = fitFile.Open(FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        WriteActivityFitFile(stream, lines);
    }

    /// <summary>
    ///     Writes a single line feature as an Activity FIT file to the specified FileInfo.
    /// </summary>
    public static void WriteActivityFitFile(FileInfo fitFile, IFeature line, DateTime? utcStart, string name,
        string description = "")
    {
        WriteActivityFitFile(fitFile, [(line, utcStart, name, description)]);
    }

    /// <summary>
    ///     Writes a single line feature as an Activity FIT file to the specified Stream.
    /// </summary>
    public static void WriteActivityFitFile(Stream stream, IFeature line, DateTime? utcStart, string name,
        string description = "")
    {
        WriteActivityFitFile(stream, [(line, utcStart, name, description)]);
    }

    /// <summary>
    ///     Writes track information objects as an Activity FIT file to the specified Stream.
    /// </summary>
    public static void WriteActivityFitFile(Stream stream, IEnumerable<GpsTrackInformation> tracks)
    {
        var lineList = tracks.Select(t =>
        {
            var feature = LineFeatureFromFitTrack(t);
            return (feature as IFeature, t.StartsOnUtc, t.Name, t.Description);
        });
        WriteActivityFitFile(stream, lineList);
    }

    /// <summary>
    ///     Writes track information objects as an Activity FIT file to the specified FileInfo.
    /// </summary>
    public static void WriteActivityFitFile(FileInfo fitFile, IEnumerable<GpsTrackInformation> tracks)
    {
        ArgumentNullException.ThrowIfNull(fitFile);
        using var stream = fitFile.Open(FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        WriteActivityFitFile(stream, tracks);
    }

    private class FitFileData
    {
        public CourseMesg? Course { get; set; }
        public List<CoursePointMesg> CoursePoints { get; } = [];
        public FileIdMesg? FileId { get; set; }
        public List<LapMesg> Laps { get; } = [];
        public List<RecordMesg> Records { get; } = [];
        public List<SessionMesg> Sessions { get; } = [];
    }
}