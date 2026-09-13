using NetTopologySuite.Geometries;

namespace PointlessWaymarks.SpatialTools;

public record GpsRouteInformation(string Name, string Description, List<CoordinateZ> Track);