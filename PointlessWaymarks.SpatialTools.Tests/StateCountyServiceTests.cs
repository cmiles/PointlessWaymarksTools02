namespace PointlessWaymarks.SpatialTools.Tests;

public class StateCountyServiceTests
{
    [Test]
    public void TestCountyGeoJsonFilePathExists()
    {
        var path = StateCountyService.OfflineGeoJsonFilePath();
        Assert.That(File.Exists(path), Is.True, $"GeoJSON file should exist at path: {path}");
    }

    [Test]
    [TestCase(34.85, -84.35, "Georgia", "Fannin County", "GA")]
    [TestCase(40.75, -92.40, "Iowa", "Davis County", "IA")]
    [TestCase(32.2226, -110.9747, "Arizona", "Pima County", "AZ")]
    [TestCase(37.7749, -122.4194, "California", "San Francisco County", "CA")]
    [TestCase(47.6062, -122.3321, "Washington", "King County", "WA")]
    public void TestGetStateCountyFromGeoJson(double latitude, double longitude, string expectedState, string expectedCounty, string expectedStateCode)
    {
        var (state, county, statecode) = StateCountyService.GetStateCountyOffline(latitude, longitude);
        Assert.That(state, Is.EqualTo(expectedState));
        Assert.That(county, Is.EqualTo(expectedCounty));
        Assert.That(statecode, Is.EqualTo(expectedStateCode));
    }

    [Test]
    public void TestGetStateCountyFromGeoJson_OutsideUs_ReturnsEmpty()
    {
        // Point in middle of the Atlantic ocean
        var (state, county, statecode) = StateCountyService.GetStateCountyOffline(0.0, 0.0);
        Assert.That(state, Is.EqualTo(string.Empty));
        Assert.That(county, Is.EqualTo(string.Empty));
        Assert.That(statecode, Is.EqualTo(string.Empty));
    }

    [Test]
    public void TestGetStateCountyLocalAndOfflineAliases()
    {
        var result = StateCountyService.GetStateCountyOffline(34.85, -84.35);
        Assert.That(result.State, Is.EqualTo("Georgia"));
        Assert.That(result.County, Is.EqualTo("Fannin County"));
        Assert.That(result.StateCode, Is.EqualTo("GA"));

        var (state2, county2, statecode2) = StateCountyService.GetStateCountyOffline(34.85, -84.35);
        Assert.That(state2, Is.EqualTo("Georgia"));
        Assert.That(county2, Is.EqualTo("Fannin County"));
        Assert.That(statecode2, Is.EqualTo("GA"));
    }

    [Test]
    [TestCase(34.85, -84.35, "Georgia", "Fannin County", "GA")]
    [TestCase(40.75, -92.40, "Iowa", "Davis County", "IA")]
    [TestCase(32.2226, -110.9747, "Arizona", "Pima County", "AZ")]
    public async Task TestGetStateCounty(double latitude, double longitude, string expectedState, string expectedCounty, string expectedStateCode)
    {
        var (state, county, statecode) = await StateCountyService.GetStateCounty(latitude, longitude);
        Assert.That(state, Is.EqualTo(expectedState));
        Assert.That(county, Is.EqualTo(expectedCounty));
        Assert.That(statecode, Is.EqualTo(expectedStateCode));
    }

    [Test]
    public void TestGetStateCountyOffline_FeatureList_EmptyOrNull()
    {
        var emptyResult = StateCountyService.GetStateCountyOffline([]);
        Assert.That(emptyResult, Is.Empty);

        var nullResult = StateCountyService.GetStateCountyOffline((List<NetTopologySuite.Features.IFeature>?)null!);
        Assert.That(nullResult, Is.Empty);
    }

    [Test]
    public void TestGetStateCountyOffline_FeatureList_MultiplePointsAndDistinct()
    {
        var pointGa1 = new NetTopologySuite.Features.Feature(PointTools.Wgs84Point(-84.35, 34.85), new NetTopologySuite.Features.AttributesTable());
        var pointGa2 = new NetTopologySuite.Features.Feature(PointTools.Wgs84Point(-84.36, 34.86), new NetTopologySuite.Features.AttributesTable());
        var pointIa = new NetTopologySuite.Features.Feature(PointTools.Wgs84Point(-92.40, 40.75), new NetTopologySuite.Features.AttributesTable());

        var results = StateCountyService.GetStateCountyOffline([pointGa1, pointGa2, pointIa]);

        Assert.That(results.Count, Is.EqualTo(2));
        Assert.That(results.Any(r => r.State == "Georgia" && r.County == "Fannin County" && r.StateCode == "GA"), Is.True);
        Assert.That(results.Any(r => r.State == "Iowa" && r.County == "Davis County" && r.StateCode == "IA"), Is.True);
    }

    [Test]
    public void TestGetStateCountyOffline_FeatureList_LineStringCrossingCounties()
    {
        // Line between Georgia and Iowa crossing multiple states/counties
        var lineCoords = new[]
        {
            new NetTopologySuite.Geometries.Coordinate(-84.35, 34.85),
            new NetTopologySuite.Geometries.Coordinate(-92.40, 40.75)
        };
        var lineGeometry = new NetTopologySuite.Geometries.LineString(lineCoords);
        var lineFeature = new NetTopologySuite.Features.Feature(lineGeometry, new NetTopologySuite.Features.AttributesTable());

        var results = StateCountyService.GetStateCountyOffline([lineFeature]);

        Assert.That(results.Count, Is.GreaterThan(2));
        Assert.That(results.Any(r => r.State == "Georgia" && r.County == "Fannin County" && r.StateCode == "GA"), Is.True);
        Assert.That(results.Any(r => r.State == "Iowa" && r.County == "Davis County" && r.StateCode == "IA"), Is.True);
    }
}
