using NetTopologySuite.Geometries;

namespace PointlessWaymarks.SpatialTools;

public static partial class GpxTools
{
    public record GpsTrackInformation(
        string Name,
        string Description,
        DateTime? StartsOnLocal,
        DateTime? EndsOnLocal,
        DateTime? StartsOnUtc,
        DateTime? EndsOnUtc,
        List<CoordinateZ> Track);
}