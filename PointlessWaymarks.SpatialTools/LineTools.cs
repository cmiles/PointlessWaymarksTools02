using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.Simplify;
using PointlessWaymarks.CommonTools;

namespace PointlessWaymarks.SpatialTools;

public static class LineTools
{
    public static List<CoordinateZ> CoordinateListFromGeoJsonFeatureCollectionWithLinestring(string geoJson)
    {
        if (string.IsNullOrWhiteSpace(geoJson)) return [];

        var featureCollection = GeoJsonTools.DeserializeStringToFeatureCollection(geoJson);

        if (featureCollection == null || featureCollection.Count < 1) return [];

        var possibleLine = featureCollection.FirstOrDefault(x => x.Geometry is LineString);

        if (possibleLine == null) return [];

        var geoLine = (LineString)possibleLine.Geometry;

        return geoLine.Coordinates.Select(x => new CoordinateZ(x.X, x.Y, x.Z)).ToList();
    }

    /// <summary>
    ///     Implements Douglas-Peucker simplification algorithm to reduce the number of points in a line
    /// </summary>
    private static Geometry DouglasPeuckerSimplify(Geometry geometry, double tolerance)
    {
        // Use the built-in NetTopologySuite simplification
        var simplifier = new DouglasPeuckerSimplifier(geometry)
        {
            DistanceTolerance = tolerance
        };
        return simplifier.GetResultGeometry();
    }

    public static List<LineElevationChartDataPoint> ElevationChartData(List<CoordinateZ> lineCoordinates)
    {
        if (lineCoordinates.Count == 0) return [];

        var returnList = new List<LineElevationChartDataPoint>
            { new(0, lineCoordinates[0].Z, 0, 0, lineCoordinates[0].Y, lineCoordinates[0].X) };

        if (lineCoordinates.Count == 1) return returnList;

        var accumulatedDistance = 0D;
        var accumulatedClimb = 0D;
        var accumulatedDescent = 0D;

        for (var i = 1; i < lineCoordinates.Count; i++)
        {
            var elevationChange = (lineCoordinates[i].Z - lineCoordinates[i - 1].Z).MetersToFeet();
            switch (elevationChange)
            {
                case > 0:
                    accumulatedClimb += elevationChange;
                    break;
                case < 0:
                    accumulatedDescent += elevationChange;
                    break;
            }

            accumulatedDistance += DistanceTools.GetDistanceInMeters(lineCoordinates[i - 1].X, lineCoordinates[i - 1].Y,
                lineCoordinates[i].X, lineCoordinates[i].Y);

            returnList.Add(new LineElevationChartDataPoint(accumulatedDistance, lineCoordinates[i].Z, accumulatedClimb,
                accumulatedDescent, lineCoordinates[i].Y, lineCoordinates[i].X));
        }

        return returnList;
    }

    public static List<LineElevationChartDataPoint> ElevationChartDataFromGeoJsonFeatureCollectionWithLinestring(
        string geoJson)
    {
        return ElevationChartData(CoordinateListFromGeoJsonFeatureCollectionWithLinestring(geoJson));
    }


    public static async Task<string> GeoJsonWithLineStringFromCoordinateList(List<CoordinateZ> pointList,
        bool replaceElevations, IProgress<string>? progress = null)
    {
        if (replaceElevations)
            await ElevationService.OpenTopoMapZenElevation(pointList, progress)
                .ConfigureAwait(false);

        // ReSharper disable once CoVariantArrayConversion It appears from testing that a linestring will reflect CoordinateZ
        var newLineString = new LineString(pointList.ToArray());
        var newFeature = new Feature(newLineString, new AttributesTable());
        var featureCollection = new FeatureCollection { newFeature };

        return await GeoJsonTools.SerializeFeatureCollectionToGeoJson(featureCollection);
    }

    /// <summary>
    ///     Returns representative points along a line geometry for OSM Overpass API queries.
    ///     Intelligently samples 1-10 points based on length and complexity.
    /// </summary>
    /// <param name="lineGeometry">The line geometry to sample points from</param>
    /// <returns>A list of coordinates representing key points along the line</returns>
    public static List<Coordinate> GetRepresentativePointsFromLine(Geometry lineGeometry)
    {
        // Ensure we have a line
        if (!(lineGeometry is LineString or MultiLineString))
            return [];

        var results = new List<Coordinate>();
        var coordinates = lineGeometry.Coordinates;

        // If it's just a short line with few points, return those points
        if (coordinates.Length <= 3)
            return coordinates.ToList();

        // Calculate complexity metrics
        var lineLength = lineGeometry.Length;
        var straightLineDistance = coordinates.First().Distance(coordinates.Last());
        var sinuosity = lineLength / straightLineDistance;

        // Determine how many points to sample (1-10)
        int pointsToSample;

        if (lineLength < 0.005) // Very short lines (< ~500m)
            pointsToSample = 1;
        else if (lineLength < 0.01) // Short lines
            pointsToSample = sinuosity < 1.2 ? 2 : 3;
        else if (lineLength < 0.05) // Medium lines
            pointsToSample = sinuosity < 1.2 ? 3 :
                sinuosity < 1.5 ? 4 : 5;
        else // Long lines
            pointsToSample = sinuosity < 1.2 ? 5 :
                sinuosity < 1.5 ? 7 : 10;

        // Always include start and end points
        results.Add(coordinates.First());

        if (pointsToSample > 2)
        {
            // For interior points, use Douglas-Peucker simplification to find the most significant points
            var simplificationTolerance = lineLength / (pointsToSample * 2.0);
            var simplifiedLine = DouglasPeuckerSimplify(lineGeometry, simplificationTolerance);

            // Get the simplified interior points (excluding first and last)
            var interiorPoints =
                simplifiedLine.Coordinates.Skip(1).Take(simplifiedLine.Coordinates.Length - 2).ToList();

            // If we have more interior points than we need, sample them evenly
            if (interiorPoints.Count > pointsToSample - 2)
            {
                var step = interiorPoints.Count / (pointsToSample - 2);
                for (var i = 0; i < interiorPoints.Count && results.Count < pointsToSample - 1; i += step)
                    results.Add(interiorPoints[i]);
            }
            else
            {
                // Otherwise use all the interior points
                results.AddRange(interiorPoints);
            }
        }

        // Add the last point
        results.Add(coordinates.Last());

        // Ensure we don't exceed 10 points
        if (results.Count > 10)
        {
            // Keep first and last points, and sample the rest evenly
            var sampledPoints = new List<Coordinate> { results.First() };
            var step = (results.Count - 2) / 8.0;

            for (var i = 1; i < 9; i++)
            {
                var index = 1 + (int)(i * step);
                sampledPoints.Add(results[index]);
            }

            sampledPoints.Add(results.Last());
            return sampledPoints;
        }

        return results;
    }
}