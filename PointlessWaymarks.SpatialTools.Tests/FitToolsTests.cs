using System.Xml;
using Dynastream.Fit;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using PointlessWaymarks.GeoTaggingService;
using PointlessWaymarks.SpatialTools;
using DateTime = System.DateTime;
using File = System.IO.File;
using FitFile = Dynastream.Fit.File;

namespace PointlessWaymarks.SpatialTools.Tests;

[TestFixture]
public class FitToolsTests
{
    private string _testDir = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FitToolsTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testDir))
        {
            try
            {
                Directory.Delete(_testDir, true);
            }
            catch
            {
                // ignored
            }
        }
    }

    private FileInfo CreateTestActivityFitFile(string filename, double startLat, double startLon, int lapCount, int pointsPerLap)
    {
        var filePath = Path.Combine(_testDir, filename);
        using (var fileStream = File.Create(filePath))
        {
            var encode = new Encode(ProtocolVersion.V20);
            encode.Open(fileStream);

            var fileId = new FileIdMesg();
            fileId.SetType(FitFile.Activity);
            fileId.SetManufacturer(Dynastream.Fit.Manufacturer.Garmin);
            fileId.SetProduct(1234);
            var baseTime = new DateTime(2025, 6, 15, 14, 0, 0, DateTimeKind.Utc);
            fileId.SetTimeCreated(new Dynastream.Fit.DateTime(baseTime));
            encode.Write(fileId);

            var session = new SessionMesg();
            session.SetSport(Sport.Running);
            session.SetStartTime(new Dynastream.Fit.DateTime(baseTime));
            session.SetTimestamp(new Dynastream.Fit.DateTime(baseTime.AddMinutes(lapCount * pointsPerLap)));
            session.SetTotalTimerTime(lapCount * pointsPerLap * 60f);
            session.SetTotalElapsedTime(lapCount * pointsPerLap * 60f);
            session.SetTotalDistance(5000f);
            session.SetTotalAscent(150);
            session.SetTotalDescent(120);
            session.SetTotalCalories(450);
            encode.Write(session);

            var currentTime = baseTime;
            var currentLat = startLat;
            var currentLon = startLon;

            for (var l = 0; l < lapCount; l++)
            {
                var lap = new LapMesg();
                lap.SetStartTime(new Dynastream.Fit.DateTime(currentTime));
                lap.SetTimestamp(new Dynastream.Fit.DateTime(currentTime.AddMinutes(pointsPerLap)));
                encode.Write(lap);

                for (var p = 0; p < pointsPerLap; p++)
                {
                    var record = new RecordMesg();
                    record.SetTimestamp(new Dynastream.Fit.DateTime(currentTime));
                    record.SetPositionLat(FitTools.DegreesToSemicircles(currentLat));
                    record.SetPositionLong(FitTools.DegreesToSemicircles(currentLon));
                    record.SetEnhancedAltitude(1200f + (l * pointsPerLap + p) * 2f);
                    encode.Write(record);

                    currentTime = currentTime.AddMinutes(1);
                    currentLat += 0.001;
                    currentLon += 0.001;
                }
            }

            encode.Close();
        }

        return new FileInfo(filePath);
    }

    private FileInfo CreateTestCourseFitFile(string filename, string courseName, double startLat, double startLon, int pointCount)
    {
        var filePath = Path.Combine(_testDir, filename);
        using (var fileStream = File.Create(filePath))
        {
            var encode = new Encode(ProtocolVersion.V20);
            encode.Open(fileStream);

            var fileId = new FileIdMesg();
            fileId.SetType(FitFile.Course);
            var baseTime = new DateTime(2025, 7, 20, 10, 0, 0, DateTimeKind.Utc);
            fileId.SetTimeCreated(new Dynastream.Fit.DateTime(baseTime));
            encode.Write(fileId);

            var course = new CourseMesg();
            course.SetName(System.Text.Encoding.UTF8.GetBytes(courseName));
            course.SetSport(Sport.Cycling);
            encode.Write(course);

            var cp1 = new CoursePointMesg();
            cp1.SetPositionLat(FitTools.DegreesToSemicircles(startLat));
            cp1.SetPositionLong(FitTools.DegreesToSemicircles(startLon));
            cp1.SetName(System.Text.Encoding.UTF8.GetBytes("Start Line"));
            cp1.SetType(CoursePoint.Generic);
            encode.Write(cp1);

            var cp2 = new CoursePointMesg();
            cp2.SetPositionLat(FitTools.DegreesToSemicircles(startLat + 0.01));
            cp2.SetPositionLong(FitTools.DegreesToSemicircles(startLon + 0.01));
            cp2.SetName(System.Text.Encoding.UTF8.GetBytes("Summit Water Station"));
            cp2.SetType(CoursePoint.Water);
            encode.Write(cp2);

            for (var i = 0; i < pointCount; i++)
            {
                var record = new RecordMesg();
                record.SetTimestamp(new Dynastream.Fit.DateTime(baseTime.AddMinutes(i)));
                record.SetPositionLat(FitTools.DegreesToSemicircles(startLat + i * 0.001));
                record.SetPositionLong(FitTools.DegreesToSemicircles(startLon + i * 0.001));
                record.SetEnhancedAltitude(1500f + i * 5f);
                encode.Write(record);
            }

            encode.Close();
        }

        return new FileInfo(filePath);
    }

    [Test]
    public void SemicirclesToDegrees_RoundTripAndKnownValues()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FitTools.SemicirclesToDegrees(0), Is.EqualTo(0.0));
            Assert.That(FitTools.SemicirclesToDegrees(1073741824), Is.EqualTo(90.0).Within(1e-6));
            Assert.That(FitTools.SemicirclesToDegrees(-1073741824), Is.EqualTo(-90.0).Within(1e-6));

            const double originalLat = 36.0528;
            var latSemicircles = FitTools.DegreesToSemicircles(originalLat);
            var recoveredLat = FitTools.SemicirclesToDegrees(latSemicircles);
            Assert.That(recoveredLat, Is.EqualTo(originalLat).Within(1e-5));

            const double originalLon = -112.0837;
            var lonSemicircles = FitTools.DegreesToSemicircles(originalLon);
            var recoveredLon = FitTools.SemicirclesToDegrees(lonSemicircles);
            Assert.That(recoveredLon, Is.EqualTo(originalLon).Within(1e-5));
        });
    }

    [Test]
    public async Task TrackInformationFromFitFile_SingleActivity_CombinesMultipleLapsIntoSingleLine()
    {
        // 3 laps with 5 points each = 15 total points
        var fitFile = CreateTestActivityFitFile("multi_lap_activity.fit", 36.0528, -112.0837, 3, 5);

        var track = await FitTools.TrackInformationFromFitFile(fitFile);
        Assert.That(track, Is.Not.Null, "FIT parsing must produce track information for valid activity file");

        Assert.Multiple(() =>
        {
            Assert.That(track!.Track, Has.Count.EqualTo(15), "Track point count should equal sum of all lap records");
            Assert.That(track.Name, Does.Contain("Run"), "Name should include sport");
            Assert.That(track.Description, Does.Contain("Duration:"), "Description should include duration");
            Assert.That(track.Description, Does.Contain("Distance:"), "Description should include distance");
            Assert.That(track.StartsOnUtc, Is.Not.Null);
            Assert.That(track.StartsOnLocal, Is.Not.Null);
            Assert.That(track.Track[0].Z, Is.EqualTo(1200).Within(1.0), "First point altitude");
            Assert.That(track.Track[^1].Z, Is.EqualTo(1200 + 14 * 2).Within(1.0), "Last point altitude");
        });
    }

    [Test]
    public async Task RouteInformationFromFitFile_CourseWithPointsAndRecords()
    {
        var fitFile = CreateTestCourseFitFile("course_test.fit", "Grand Canyon Ridge Course", 36.1, -112.1, 10);

        var route = await FitTools.RouteInformationFromFitFile(fitFile);
        Assert.That(route, Is.Not.Null, "FIT parsing must produce route information for course file");

        Assert.Multiple(() =>
        {
            Assert.That(route!.Name, Is.EqualTo("Grand Canyon Ridge Course"));
            Assert.That(route.Track, Has.Count.EqualTo(10));
            Assert.That(route.Track[0].X, Is.EqualTo(-112.1).Within(1e-4));
            Assert.That(route.Track[0].Y, Is.EqualTo(36.1).Within(1e-4));
            Assert.That(route.Track[0].Z, Is.EqualTo(1500).Within(1.0));
        });

        var routeFeature = await FitTools.RouteLineFromFitFile(fitFile);
        Assert.That(routeFeature, Is.Not.Null);
        Assert.That(routeFeature!.Geometry, Is.TypeOf<LineString>());

        var bufferedRoute = await FitTools.RouteLineFromFitFileBuffered(fitFile, 25);
        Assert.That(bufferedRoute, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(bufferedRoute!.Feature.Geometry, Is.TypeOf<LineString>());
            Assert.That(bufferedRoute.BufferedFeature.Geometry, Is.TypeOf<Polygon>());
        });
    }

    [Test]
    public async Task WaypointPointsFromFitFile_ExtractsCoursePoints()
    {
        var fitFile = CreateTestCourseFitFile("course_waypoints.fit", "Waypoints Course", 36.1, -112.1, 5);

        var (features, boundingBox) = await FitTools.WaypointPointsFromFitFile(fitFile);

        Assert.Multiple(() =>
        {
            Assert.That(features, Has.Count.EqualTo(2), "Should extract 2 course points as waypoints");
            Assert.That(features[0].Attributes["title"]?.ToString(), Is.EqualTo("Start Line"));
            Assert.That(features[1].Attributes["title"]?.ToString(), Is.EqualTo("Summit Water Station"));
            Assert.That(features[1].Attributes["description"]?.ToString(), Is.EqualTo("Water"));
            Assert.That(boundingBox.IsNull, Is.False);
        });

        var (circleFeatures, circleBounds) = await FitTools.WaypointPointsFromFitFileAs2DCircles(fitFile, 50);
        Assert.Multiple(() =>
        {
            Assert.That(circleFeatures, Has.Count.EqualTo(2));
            Assert.That(circleFeatures[0].Geometry, Is.TypeOf<Polygon>());
            Assert.That(circleBounds.Area, Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task SpatialFeaturesAndBuffering_GeneratesValidGeometries()
    {
        var fitFile = CreateTestActivityFitFile("spatial_activity.fit", 36.0528, -112.0837, 2, 4);

        var track = await FitTools.TrackInformationFromFitFile(fitFile);
        Assert.That(track, Is.Not.Null);

        var lineFeature = FitTools.LineFeatureFromFitTrack(track!);
        var bufferedLineFeature = FitTools.LineFeatureFromFitTrackBuffered(track!, 100);

        Assert.Multiple(() =>
        {
            Assert.That(lineFeature.Geometry, Is.TypeOf<LineString>());
            Assert.That(bufferedLineFeature.Geometry, Is.TypeOf<Polygon>());
            Assert.That(lineFeature.Attributes["title"]?.ToString(), Is.EqualTo(track!.Name));
            Assert.That(bufferedLineFeature.Attributes["title"]?.ToString(), Is.EqualTo(track!.Name));
        });

        var singleTrackLine = await FitTools.TrackLineFromFitFile(fitFile);
        Assert.That(singleTrackLine, Is.Not.Null);
        Assert.That(singleTrackLine!.Geometry, Is.TypeOf<LineString>());

        var singleBufferedTrackLine = await FitTools.TrackLineFromFitFileBuffered(fitFile, 50);
        Assert.That(singleBufferedTrackLine, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(singleBufferedTrackLine!.Feature.Geometry, Is.TypeOf<LineString>());
            Assert.That(singleBufferedTrackLine.BufferedFeature.Geometry, Is.TypeOf<Polygon>());
        });
    }

    [Test]
    public async Task NonExistentAndEmptyFiles_HandledGracefully()
    {
        var nonExistent = new FileInfo(Path.Combine(_testDir, "does_not_exist.fit"));
        var trackInfo = await FitTools.TrackInformationFromFitFile(nonExistent);
        var routeInfo = await FitTools.RouteInformationFromFitFile(nonExistent);
        var trackLine = await FitTools.TrackLineFromFitFile(nonExistent);
        var routeLine = await FitTools.RouteLineFromFitFile(nonExistent);
        var bufferedTrackLine = await FitTools.TrackLineFromFitFileBuffered(nonExistent, 50);
        var bufferedRouteLine = await FitTools.RouteLineFromFitFileBuffered(nonExistent, 50);
        var (waypoints, wpBounds) = await FitTools.WaypointPointsFromFitFile(nonExistent);

        Assert.Multiple(() =>
        {
            Assert.That(trackInfo, Is.Null);
            Assert.That(routeInfo, Is.Null);
            Assert.That(trackLine, Is.Null);
            Assert.That(routeLine, Is.Null);
            Assert.That(bufferedTrackLine, Is.Null);
            Assert.That(bufferedRouteLine, Is.Null);
            Assert.That(waypoints, Is.Empty);
            Assert.That(wpBounds.IsNull, Is.True);
        });

        var emptyFile = new FileInfo(Path.Combine(_testDir, "empty.fit"));
        await File.WriteAllBytesAsync(emptyFile.FullName, []);

        var emptyTrackInfo = await FitTools.TrackInformationFromFitFile(emptyFile);
        var emptyTrackLine = await FitTools.TrackLineFromFitFile(emptyFile);

        Assert.Multiple(() =>
        {
            Assert.That(emptyTrackInfo, Is.Null);
            Assert.That(emptyTrackLine, Is.Null);
        });
    }

    [Test]
    public async Task IndoorActivityWithNoGps_ReturnsEmptyAndNull()
    {
        var filePath = Path.Combine(_testDir, "indoor_workout.fit");
        using (var fileStream = File.Create(filePath))
        {
            var encode = new Encode(ProtocolVersion.V20);
            encode.Open(fileStream);

            var fileId = new FileIdMesg();
            fileId.SetType(FitFile.Activity);
            var baseTime = new DateTime(2025, 6, 15, 14, 0, 0, DateTimeKind.Utc);
            fileId.SetTimeCreated(new Dynastream.Fit.DateTime(baseTime));
            encode.Write(fileId);

            var session = new SessionMesg();
            session.SetSport(Sport.FitnessEquipment);
            session.SetStartTime(new Dynastream.Fit.DateTime(baseTime));
            session.SetTotalTimerTime(1800f);
            session.SetTotalCalories(250);
            encode.Write(session);

            for (var i = 0; i < 5; i++)
            {
                var record = new RecordMesg();
                record.SetTimestamp(new Dynastream.Fit.DateTime(baseTime.AddMinutes(i)));
                record.SetHeartRate((byte)(120 + i));
                encode.Write(record);
            }

            encode.Close();
        }

        var fitFile = new FileInfo(filePath);
        var trackInfo = await FitTools.TrackInformationFromFitFile(fitFile);
        var trackLine = await FitTools.TrackLineFromFitFile(fitFile);

        Assert.Multiple(() =>
        {
            Assert.That(trackInfo, Is.Null);
            Assert.That(trackLine, Is.Null);
        });
    }

    [Test]
    public async Task ActivityWithNegativeCoordinatesAndMissingElevation_DecodesProperly()
    {
        var filePath = Path.Combine(_testDir, "southern_west_activity.fit");
        using (var fileStream = File.Create(filePath))
        {
            var encode = new Encode(ProtocolVersion.V20);
            encode.Open(fileStream);

            var fileId = new FileIdMesg();
            fileId.SetType(FitFile.Activity);
            var baseTime = new DateTime(2025, 8, 10, 12, 0, 0, DateTimeKind.Utc);
            fileId.SetTimeCreated(new Dynastream.Fit.DateTime(baseTime));
            encode.Write(fileId);

            var session = new SessionMesg();
            session.SetSport(Sport.Hiking);
            session.SetStartTime(new Dynastream.Fit.DateTime(baseTime));
            session.SetTotalTimerTime(600f);
            encode.Write(session);

            // Patagonia coordinates: -50.0 degrees lat, -73.0 degrees lon
            for (var i = 0; i < 3; i++)
            {
                var record = new RecordMesg();
                record.SetTimestamp(new Dynastream.Fit.DateTime(baseTime.AddMinutes(i)));
                record.SetPositionLat(FitTools.DegreesToSemicircles(-50.0 + i * 0.01));
                record.SetPositionLong(FitTools.DegreesToSemicircles(-73.0 + i * 0.01));
                // No altitude set
                encode.Write(record);
            }

            encode.Close();
        }

        var fitFile = new FileInfo(filePath);
        var trackInfo = await FitTools.TrackInformationFromFitFile(fitFile);

        Assert.That(trackInfo, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(trackInfo!.Track, Has.Count.EqualTo(3));
            Assert.That(trackInfo.Track[0].Y, Is.EqualTo(-50.0).Within(1e-4));
            Assert.That(trackInfo.Track[0].X, Is.EqualTo(-73.0).Within(1e-4));
            Assert.That(trackInfo.Track[0].Z, Is.EqualTo(0.0), "Elevation should default to 0 when missing");
            Assert.That(trackInfo.Name, Does.Contain("Hike"));
        });
    }

    [Test]
    public async Task CourseWithOnlyCoursePoints_FallsBackToCoursePointsForRoute()
    {
        var filePath = Path.Combine(_testDir, "course_points_only.fit");
        using (var fileStream = File.Create(filePath))
        {
            var encode = new Encode(ProtocolVersion.V20);
            encode.Open(fileStream);

            var fileId = new FileIdMesg();
            fileId.SetType(FitFile.Course);
            var baseTime = new DateTime(2025, 9, 1, 8, 0, 0, DateTimeKind.Utc);
            fileId.SetTimeCreated(new Dynastream.Fit.DateTime(baseTime));
            encode.Write(fileId);

            var course = new CourseMesg();
            course.SetName(System.Text.Encoding.UTF8.GetBytes("Points Only Course"));
            encode.Write(course);

            var cp1 = new CoursePointMesg();
            cp1.SetPositionLat(FitTools.DegreesToSemicircles(34.0));
            cp1.SetPositionLong(FitTools.DegreesToSemicircles(-118.0));
            cp1.SetName(System.Text.Encoding.UTF8.GetBytes("Start"));
            encode.Write(cp1);

            var cp2 = new CoursePointMesg();
            cp2.SetPositionLat(FitTools.DegreesToSemicircles(34.1));
            cp2.SetPositionLong(FitTools.DegreesToSemicircles(-118.1));
            cp2.SetName(System.Text.Encoding.UTF8.GetBytes("End"));
            encode.Write(cp2);

            encode.Close();
        }

        var fitFile = new FileInfo(filePath);
        var routeInfo = await FitTools.RouteInformationFromFitFile(fitFile);

        Assert.That(routeInfo, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(routeInfo!.Name, Is.EqualTo("Points Only Course"));
            Assert.That(routeInfo.Track, Has.Count.EqualTo(2));
            Assert.That(routeInfo.Track[0].Y, Is.EqualTo(34.0).Within(1e-4));
            Assert.That(routeInfo.Track[0].X, Is.EqualTo(-118.0).Within(1e-4));
        });

        var routeLine = await FitTools.RouteLineFromFitFile(fitFile);
        var bufferedRouteLine = await FitTools.RouteLineFromFitFileBuffered(fitFile, 25);

        Assert.Multiple(() =>
        {
            Assert.That(routeLine, Is.Not.Null);
            Assert.That(bufferedRouteLine, Is.Not.Null);
            Assert.That(routeLine!.Geometry, Is.TypeOf<LineString>());
            Assert.That(bufferedRouteLine!.Feature.Geometry, Is.TypeOf<LineString>());
            Assert.That(bufferedRouteLine.BufferedFeature.Geometry, Is.TypeOf<Polygon>());
        });
    }

    [Test]
    public async Task FitFileHasLocation_ActivityWithGps_ReturnsTrue()
    {
        var fitFile = CreateTestActivityFitFile("activity_has_location.fit", 36.0528, -112.0837, 2, 3);
        Assert.Multiple(async () =>
        {
            Assert.That(await FitTools.FitFileHasLocation(fitFile), Is.True);
            Assert.That(await FitTools.FitFileHasLocationData(fitFile), Is.True);
            Assert.That(await FitTools.FitFileHasLocations(fitFile), Is.True);
            Assert.That(await FitTools.FitFileHasTrackOrCourse(fitFile), Is.True);
            Assert.That(await FitTools.FitFileHasLatLong(fitFile), Is.True);
        });
    }

    [Test]
    public async Task FitFileHasLocation_CourseWithPoints_ReturnsTrue()
    {
        var fitFile = CreateTestCourseFitFile("course_has_location.fit", "Location Course", 36.1, -112.1, 5);
        Assert.That(await FitTools.FitFileHasLocation(fitFile), Is.True);
    }

    [Test]
    public async Task FitFileHasLocation_CourseWithOnlyCoursePoints_ReturnsTrue()
    {
        var filePath = Path.Combine(_testDir, "has_loc_points_only.fit");
        using (var fileStream = File.Create(filePath))
        {
            var encode = new Encode(ProtocolVersion.V20);
            encode.Open(fileStream);

            var fileId = new FileIdMesg();
            fileId.SetType(FitFile.Course);
            encode.Write(fileId);

            var cp1 = new CoursePointMesg();
            cp1.SetPositionLat(FitTools.DegreesToSemicircles(34.0));
            cp1.SetPositionLong(FitTools.DegreesToSemicircles(-118.0));
            encode.Write(cp1);

            encode.Close();
        }

        var fitFile = new FileInfo(filePath);
        Assert.That(await FitTools.FitFileHasLocation(fitFile), Is.True);
    }

    [Test]
    public async Task FitFileHasLocation_IndoorNoGps_ReturnsFalse()
    {
        var filePath = Path.Combine(_testDir, "indoor_no_gps_check.fit");
        using (var fileStream = File.Create(filePath))
        {
            var encode = new Encode(ProtocolVersion.V20);
            encode.Open(fileStream);

            var fileId = new FileIdMesg();
            fileId.SetType(FitFile.Activity);
            encode.Write(fileId);

            for (var i = 0; i < 3; i++)
            {
                var record = new RecordMesg();
                record.SetHeartRate((byte)(120 + i));
                encode.Write(record);
            }

            encode.Close();
        }

        var fitFile = new FileInfo(filePath);
        Assert.That(await FitTools.FitFileHasLocation(fitFile), Is.False);
    }

    [Test]
    public async Task FitFileHasLocation_NonExistentAndEmpty_ReturnsFalse()
    {
        var nonExistent = new FileInfo(Path.Combine(_testDir, "does_not_exist_loc.fit"));
        Assert.That(await FitTools.FitFileHasLocation(nonExistent), Is.False);

        var emptyFile = new FileInfo(Path.Combine(_testDir, "empty_loc.fit"));
        await File.WriteAllBytesAsync(emptyFile.FullName, []);
        Assert.That(await FitTools.FitFileHasLocation(emptyFile), Is.False);
    }

    [Test]
    public async Task WriteActivityFitFile_SingleLineFeature_RoundTripsAccurately()
    {
        var coordinates = new[]
        {
            new CoordinateZ(-112.0837, 36.0528, 2100.0),
            new CoordinateZ(-112.0847, 36.0538, 2110.0),
            new CoordinateZ(-112.0857, 36.0548, 2120.0),
            new CoordinateZ(-112.0867, 36.0558, 2135.0)
        };
        var lineString = new LineString(coordinates);
        var attributes = new NetTopologySuite.Features.AttributesTable
        {
            { "title", "Grand Canyon Rim Line" },
            { "description", "Scenic Rim Route" }
        };
        var feature = new NetTopologySuite.Features.Feature(lineString, attributes);

        var fitFilePath = Path.Combine(_testDir, "single_line_export.fit");
        var fitFile = new FileInfo(fitFilePath);
        var startTimeUtc = new DateTime(2025, 8, 10, 15, 30, 0, DateTimeKind.Utc);

        FitTools.WriteActivityFitFile(fitFile, feature, startTimeUtc, "Grand Canyon Rim Line", "Scenic Rim Route");

        Assert.That(fitFile.Exists, Is.True);
        Assert.That(fitFile.Length, Is.GreaterThan(0));

        var hasLocation = await FitTools.FitFileHasLocation(fitFile);
        Assert.That(hasLocation, Is.True);

        var trackInfo = await FitTools.TrackInformationFromFitFile(fitFile);
        Assert.That(trackInfo, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(trackInfo!.Track, Has.Count.EqualTo(4));
            Assert.That(trackInfo.Track[0].X, Is.EqualTo(-112.0837).Within(1e-4));
            Assert.That(trackInfo.Track[0].Y, Is.EqualTo(36.0528).Within(1e-4));
            Assert.That(trackInfo.Track[0].Z, Is.EqualTo(2100.0).Within(1e-1));
            Assert.That(trackInfo.Track[^1].Z, Is.EqualTo(2135.0).Within(1e-1));
            Assert.That(trackInfo.StartsOnUtc, Is.Not.Null);
            Assert.That(trackInfo.StartsOnUtc!.Value, Is.EqualTo(startTimeUtc));
        });

        var trackLine = await FitTools.TrackLineFromFitFile(fitFile);
        Assert.That(trackLine, Is.Not.Null);
        Assert.That(trackLine!.Geometry, Is.TypeOf<LineString>());
    }

    [Test]
    public async Task WriteActivityFitFile_MultipleLineFeatures_RoundTripsAsSingleMergedTrack()
    {
        var line1 = new NetTopologySuite.Features.Feature(new LineString([
            new CoordinateZ(-112.10, 36.10, 1000.0),
            new CoordinateZ(-112.11, 36.11, 1010.0),
            new CoordinateZ(-112.12, 36.12, 1020.0)
        ]), new NetTopologySuite.Features.AttributesTable());

        var line2 = new NetTopologySuite.Features.Feature(new LineString([
            new CoordinateZ(-112.13, 36.13, 1030.0),
            new CoordinateZ(-112.14, 36.14, 1040.0)
        ]), new NetTopologySuite.Features.AttributesTable());

        var fitFilePath = Path.Combine(_testDir, "multi_line_export.fit");
        var fitFile = new FileInfo(fitFilePath);
        var startTimeUtc = new DateTime(2025, 9, 1, 9, 0, 0, DateTimeKind.Utc);

        var lineList = new List<(NetTopologySuite.Features.IFeature line, DateTime? utcStart, string name, string description)>
        {
            (line1, startTimeUtc, "Segment 1", "First Segment"),
            (line2, startTimeUtc.AddHours(1), "Segment 2", "Second Segment")
        };

        FitTools.WriteActivityFitFile(fitFile, lineList);

        var trackInfo = await FitTools.TrackInformationFromFitFile(fitFile);
        Assert.That(trackInfo, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(trackInfo!.Track, Has.Count.EqualTo(5), "All segments must be merged into 1 continuous track");
            Assert.That(trackInfo.Track[0].X, Is.EqualTo(-112.10).Within(1e-4));
            Assert.That(trackInfo.Track[^1].X, Is.EqualTo(-112.14).Within(1e-4));
        });
    }

    [Test]
    public async Task WriteActivityFitFile_GpsTrackInformation_RoundTrips()
    {
        var trackInfo = new GpxTools.GpsTrackInformation(
            "Test Track Info",
            "Description of Track",
            null,
            null,
            new DateTime(2025, 5, 20, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 5, 20, 13, 0, 0, DateTimeKind.Utc),
            [
                new CoordinateZ(-111.9, 34.8, 1500.0),
                new CoordinateZ(-111.91, 34.81, 1520.0),
                new CoordinateZ(-111.92, 34.82, 1540.0)
            ]);

        var fitFilePath = Path.Combine(_testDir, "track_info_export.fit");
        var fitFile = new FileInfo(fitFilePath);

        FitTools.WriteActivityFitFile(fitFile, [trackInfo]);

        var readBack = await FitTools.TrackInformationFromFitFile(fitFile);
        Assert.That(readBack, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(readBack!.Track, Has.Count.EqualTo(3));
            Assert.That(readBack.Track[0].X, Is.EqualTo(-111.9).Within(1e-4));
            Assert.That(readBack.Track[0].Y, Is.EqualTo(34.8).Within(1e-4));
        });
    }

    [Test]
    public async Task GpxWaypointsFromFitFile_ActivityFitFile_ExtractsWaypointsWithTimestampsAndElevation()
    {
        var fitFile = CreateTestActivityFitFile("waypoints_test_activity.fit", 32.2217, -110.9265, 2, 5);

        var waypoints = await FitTools.GpxWaypointsFromFitFile(fitFile);

        Assert.That(waypoints, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(waypoints, Has.Count.EqualTo(10));
            Assert.That((double)waypoints[0].Longitude, Is.EqualTo(-110.9265).Within(1e-4));
            Assert.That((double)waypoints[0].Latitude, Is.EqualTo(32.2217).Within(1e-4));
            Assert.That(waypoints[0].ElevationInMeters, Is.Not.Null);
            Assert.That(waypoints[0].TimestampUtc, Is.Not.Null);
            Assert.That(waypoints[0].TimestampUtc!.Value, Is.EqualTo(new DateTime(2025, 6, 15, 14, 0, 0, DateTimeKind.Utc)));
            Assert.That(waypoints[^1].TimestampUtc!.Value, Is.EqualTo(new DateTime(2025, 6, 15, 14, 9, 0, DateTimeKind.Utc)));
        });
    }

    [Test]
    public async Task GpxWaypointsFromFitFile_CourseFitFile_ExtractsWaypointsWithNames()
    {
        var fitFile = CreateTestCourseFitFile("waypoints_test_course.fit", "Catalina Loop", 32.35, -110.85, 4);

        var waypoints = await FitTools.GpxWaypointsFromFitFile(fitFile);

        Assert.That(waypoints, Is.Not.Null);
        Assert.Multiple(() =>
        {
            // 4 records + 2 course points = 6 waypoints
            Assert.That(waypoints, Has.Count.EqualTo(6));
            var coursePointWaypoints = waypoints.Where(x => !string.IsNullOrEmpty(x.Name)).ToList();
            Assert.That(coursePointWaypoints, Has.Count.EqualTo(2));
            Assert.That(coursePointWaypoints[0].Name, Is.EqualTo("Start Line"));
            Assert.That(coursePointWaypoints[1].Name, Is.EqualTo("Summit Water Station"));
        });
    }

    [Test]
    public async Task FileListGpxService_WithFitFiles_ExtractsPointsForGeoTagging()
    {
        var fitFile = CreateTestActivityFitFile("service_test_activity.fit", 32.12, -110.52, 1, 10);

        var service = new FileListGpxService([fitFile]);
        var queryTime = new DateTime(2025, 6, 15, 14, 4, 30, DateTimeKind.Utc);

        var points = await service.GetGpxPoints([queryTime], null, CancellationToken.None);

        Assert.That(points, Is.Not.Null);
        Assert.That(points, Has.Count.EqualTo(10));
        Assert.Multiple(() =>
        {
            Assert.That(points[0].Source, Is.EqualTo("service_test_activity.fit"));
            Assert.That((double)points[0].Waypoint.Latitude, Is.EqualTo(32.12).Within(1e-4));
            Assert.That((double)points[0].Waypoint.Longitude, Is.EqualTo(-110.52).Within(1e-4));
        });
    }

    [Test]
    public async Task FileListGpxService_WithMixedFitAndGpxFiles_ExtractsPointsCorrectly()
    {
        var fitFile = CreateTestActivityFitFile("service_mixed_activity.fit", 32.12, -110.52, 1, 5);

        var gpxPath = Path.Combine(_testDir, "service_mixed_activity.gpx");
        var gpxFile = new FileInfo(gpxPath);
        var gpxBaseTime = new DateTime(2025, 6, 15, 16, 0, 0, DateTimeKind.Utc);

        var gpxCoordinates = new List<CoordinateZ>();
        for (var i = 0; i < 5; i++)
        {
            gpxCoordinates.Add(new CoordinateZ(-110.60 + i * 0.001, 32.20 + i * 0.001, 1000 + i * 10));
        }

        var lineFeature = new NetTopologySuite.Features.Feature(new LineString(gpxCoordinates.ToArray()), new NetTopologySuite.Features.AttributesTable());
        var track = GpxTools.GpxTrackFromLineFeature(lineFeature, gpxBaseTime, "GPX Track", string.Empty, "Description");

        using (var fileStream = File.Create(gpxFile.FullName))
        {
            var writerSettings = new XmlWriterSettings { Encoding = System.Text.Encoding.UTF8, Indent = true, CloseOutput = true };
            using var xmlWriter = XmlWriter.Create(fileStream, writerSettings);
            GpxWriter.Write(xmlWriter, null, new GpxMetadata("Test"), null, null, [track], null);
        }

        var service = new FileListGpxService([fitFile, gpxFile]);

        var fitQueryTime = new DateTime(2025, 6, 15, 14, 2, 0, DateTimeKind.Utc);
        var fitPoints = await service.GetGpxPoints([fitQueryTime], null, CancellationToken.None);
        Assert.That(fitPoints, Has.Count.EqualTo(5));
        Assert.That(fitPoints[0].Source, Is.EqualTo("service_mixed_activity.fit"));

        var gpxQueryTime = new DateTime(2025, 6, 15, 16, 2, 0, DateTimeKind.Utc);
        var extractedGpxPoints = await service.GetGpxPoints([gpxQueryTime], null, CancellationToken.None);
        Assert.That(extractedGpxPoints, Has.Count.EqualTo(5));
        Assert.That(extractedGpxPoints[0].Source, Is.EqualTo("service_mixed_activity.gpx"));

        var bothQueryTimes = new List<DateTime> { fitQueryTime, gpxQueryTime };
        var allPoints = await service.GetGpxPoints(bothQueryTimes, null, CancellationToken.None);
        Assert.That(allPoints, Has.Count.EqualTo(10));
    }
}
