using NetTopologySuite.Geometries;

namespace PointlessWaymarks.SpatialTools;

public static partial class GpxTools
{
    public record GpsRouteInformation(string Name, string Description, List<CoordinateZ> Track);
}