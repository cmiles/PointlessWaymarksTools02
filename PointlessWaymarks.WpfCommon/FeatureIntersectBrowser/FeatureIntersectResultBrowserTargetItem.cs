using NetTopologySuite.Features;
using PointlessWaymarks.SpatialTools;

namespace PointlessWaymarks.WpfCommon.FeatureIntersectBrowser;

public class FeatureIntersectResultBrowserTargetItem
{
    public required List<string> Attributes { get; set; }
    public required IFeature Feature { get; set; }
    public required string JsonString { get; set; }

    public required string Name { get; set; }

    public static async Task<FeatureIntersectResultBrowserTargetItem> CreateInstance(IFeature feature, string name)
    {
        var jsonString = await GeoJsonTools.SerializeFeatureToGeoJson(feature);

        var attributesList = new List<string>();

        foreach (var attributeName in feature.Attributes.GetNames())
            attributesList.Add(attributeName + ":" + feature.Attributes[attributeName]);

        return new FeatureIntersectResultBrowserTargetItem
            { Attributes = attributesList, Feature = feature, JsonString = jsonString, Name = name };
    }
}
