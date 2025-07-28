using NetTopologySuite.Geometries;
using PointlessWaymarks.CommonTools;

namespace PointlessWaymarks.SpatialTools;

public static class PointTools
{
    public static Polygon CreateCircle(double x, double y, double radiusInFeet)
    {
        return CreateCircle(Wgs84Point(x, y), radiusInFeet);
    }

    /// <summary>
    ///     Creates a circular polygon with the specified diameter in feet around the given point
    /// </summary>
    /// <param name="point">The center point (in WGS84 coordinates)</param>
    /// <param name="radiusInFeet"></param>
    /// <returns>A Polygon representing a circle of the specified diameter around the point</returns>
    public static Polygon CreateCircle(Point point, double radiusInFeet)
    {
        var latitudeDegrees = DistanceTools.ApproximateMetersToLatitudeDegrees( radiusInFeet.FeetToMeters(), point.X, point.Y);
        var longitudeDegrees
            = DistanceTools.ApproximateMetersToLongitudeDegrees(radiusInFeet.FeetToMeters(), point.X, point.Y);

        // Use buffer operation to create a circular polygon
        // The quadrant segments parameter (36) controls how smooth the circle is
        return (Polygon)point.Buffer((latitudeDegrees + longitudeDegrees) / 2D, 36);
    }

    public static Point ProjectCoordinate(Point startPoint, double bearing, double distance)
    {
        const int radius = 6378001;
        var lat1 = startPoint.Y * (Math.PI / 180);
        var lon1 = startPoint.X * (Math.PI / 180);
        var bearing1 = bearing * (Math.PI / 180);
        var lat2 = Math.Asin(Math.Sin(lat1) * Math.Cos(distance / radius) +
                             Math.Cos(lat1) * Math.Sin(distance / radius) * Math.Cos(bearing1));
        var lon2 = lon1 + Math.Atan2(Math.Sin(bearing1) * Math.Sin(distance / radius) * Math.Cos(lat1),
            Math.Cos(distance / radius) - Math.Sin(lat1) * Math.Sin(lat2));
        return Wgs84Point(lon2 * (180 / Math.PI), lat2 * (180 / Math.PI));
    }

    public static Point Wgs84Point(double x, double y, double z)
    {
        return GeoJsonTools.Wgs84GeometryFactory().CreatePoint(new CoordinateZ(x, y, z));
    }

    public static Point Wgs84Point(double x, double y)
    {
        return GeoJsonTools.Wgs84GeometryFactory().CreatePoint(new Coordinate(x, y));
    }
}