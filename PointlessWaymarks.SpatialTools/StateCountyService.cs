using System.Text.Json;
using NetTopologySuite.Features;
using PointlessWaymarks.SpatialTools.StateCountyServiceModels;
using Serilog;

namespace PointlessWaymarks.SpatialTools;

public static class StateCountyService
{
    private static readonly HttpClient StateCountyHttpClient = new();
    private static readonly Lazy<FeatureCollection?> DefaultCountyFeatureCollection = new(LoadOfflineFeatureCollection);


    public static async Task<StateCountyResult> GetStateCounty(double latitude, double longitude)
    {
        try
        {
            var (onlineState, onlineCounty, onlineStateCode) = await GetStateCountyFccGov(latitude, longitude);
            if (!string.IsNullOrWhiteSpace(onlineState) && !string.IsNullOrWhiteSpace(onlineCounty) &&
                !string.IsNullOrWhiteSpace(onlineStateCode))
                return new StateCountyResult(onlineState, onlineCounty, onlineStateCode);

            var (offlineState, offlineCounty, offlineStateCode) = GetStateCountyOffline(latitude, longitude);
            return new StateCountyResult(string.IsNullOrWhiteSpace(onlineState) ? offlineState : onlineState,
                string.IsNullOrWhiteSpace(onlineCounty) ? offlineCounty : onlineCounty,
                string.IsNullOrWhiteSpace(onlineStateCode) ? offlineStateCode : onlineStateCode);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error in GetStateCounty for lat {Latitude}, lon {Longitude}", latitude, longitude);
        }

        return GetStateCountyOffline(latitude, longitude);
    }

    public static async Task<StateCountyResult> GetStateCountyFccGov(double latitude, double longitude)
    {
        var requestUrl =
            $"https://geo.fcc.gov/api/census/area?lat={latitude}&lon={longitude}&censusYear=2020&format=json";

        FccAreaApiResponse? deserializedResponse = null;

        try
        {
            deserializedResponse = await JsonSerializer.DeserializeAsync<FccAreaApiResponse>(
                await StateCountyHttpClient.GetStreamAsync(requestUrl));
        }
        catch (Exception e)
        {
            Log.Error(e, "Ignored Exception - Call failed to the FCC Area Api");
        }

        var firstResult = deserializedResponse?.Results.FirstOrDefault();

        return new StateCountyResult(firstResult?.StateName ?? string.Empty,
            firstResult?.CountyName ?? string.Empty,
            firstResult?.StateCode ?? string.Empty);
    }

    private static StateCountyResult GetStateCountyFromGeoJson(double latitude, double longitude)
    {
        var featureCollection = DefaultCountyFeatureCollection.Value;

        if (featureCollection == null || featureCollection.Count == 0)
            return new StateCountyResult();

        var point = PointTools.Wgs84Point(longitude, latitude);

        var matchingFeature = featureCollection.FirstOrDefault(x =>
            x.Geometry != null &&
            x.Geometry.EnvelopeInternal.Intersects(point.Coordinate) &&
            x.Geometry.Intersects(point));

        if (matchingFeature?.Attributes == null)
            return new StateCountyResult();

        var state = matchingFeature.Attributes.Exists("STATE_NAME")
            ? matchingFeature.Attributes["STATE_NAME"]?.ToString() ?? string.Empty
            : string.Empty;

        var county = matchingFeature.Attributes.Exists("NAMELSAD")
            ? matchingFeature.Attributes["NAMELSAD"]?.ToString() ?? string.Empty
            : string.Empty;

        var statecode = matchingFeature.Attributes.Exists("STUSPS")
            ? matchingFeature.Attributes["STUSPS"]?.ToString() ?? string.Empty
            : string.Empty;

        return new StateCountyResult(state, county, statecode);
    }

    public static StateCountyResult GetStateCountyOffline(double latitude, double longitude)
    {
        return GetStateCountyFromGeoJson(latitude, longitude);
    }

    public static List<StateCountyResult> GetStateCountyOffline(List<IFeature> features)
    {
        if (features.Count == 0)
            return [];

        var featureCollection = DefaultCountyFeatureCollection.Value;
        if (featureCollection == null || featureCollection.Count == 0)
            return [];

        var validFeatures = features.Where(x => x.Geometry != null && !x.Geometry.IsEmpty).ToList();
        if (validFeatures.Count == 0)
            return [];

        var results = new List<StateCountyResult>();

        foreach (var countyFeature in featureCollection)
        {
            if (countyFeature?.Geometry == null || countyFeature.Geometry.IsEmpty)
                continue;

            var countyEnvelope = countyFeature.Geometry.EnvelopeInternal;

            foreach (var checkFeature in validFeatures)
            {
                if (!countyEnvelope.Intersects(checkFeature.Geometry.EnvelopeInternal))
                    continue;

                if (checkFeature.Geometry.Intersects(countyFeature.Geometry)
                    || checkFeature.Geometry.Crosses(countyFeature.Geometry)
                    || checkFeature.Geometry.Contains(countyFeature.Geometry)
                    || checkFeature.Geometry.Overlaps(countyFeature.Geometry)
                    || checkFeature.Geometry.CoveredBy(countyFeature.Geometry)
                    || checkFeature.Geometry.Touches(countyFeature.Geometry)
                    || checkFeature.Geometry.Within(countyFeature.Geometry))
                {
                    var state = countyFeature.Attributes.Exists("STATE_NAME")
                        ? countyFeature.Attributes["STATE_NAME"]?.ToString() ?? string.Empty
                        : string.Empty;

                    var county = countyFeature.Attributes.Exists("NAMELSAD")
                        ? countyFeature.Attributes["NAMELSAD"]?.ToString() ?? string.Empty
                        : string.Empty;

                    var statecode = countyFeature.Attributes.Exists("STUSPS")
                        ? countyFeature.Attributes["STUSPS"]?.ToString() ?? string.Empty
                        : string.Empty;

                    results.Add(new StateCountyResult(state, county, statecode));
                    break;
                }
            }
        }

        return results.Distinct().ToList();
    }

    private static FeatureCollection? LoadOfflineFeatureCollection()
    {
        var filePath = OfflineGeoJsonFilePath();
        if (!File.Exists(filePath))
        {
            Log.Error("County GeoJSON file not found at {FilePath}", filePath);
            return null;
        }

        try
        {
            return GeoJsonTools.DeserializeFileToFeatureCollection(filePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load County GeoJSON file from {FilePath}", filePath);
            return null;
        }
    }

    public static string OfflineGeoJsonFilePath()
    {
        var possibleLocations = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "StateCountyServiceModels", "cb_2025_us_county_5m.geojson"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StateCountyServiceModels",
                "cb_2025_us_county_5m.geojson")
        };

        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            var processDir = Path.GetDirectoryName(processPath);
            if (!string.IsNullOrWhiteSpace(processDir))
                possibleLocations.Add(Path.Combine(processDir, "StateCountyServiceModels",
                    "cb_2025_us_county_5m.geojson"));
        }

        var assemblyLocation = typeof(StateCountyService).Assembly.Location;
        if (!string.IsNullOrWhiteSpace(assemblyLocation))
        {
            var assemblyDir = Path.GetDirectoryName(assemblyLocation);
            if (!string.IsNullOrWhiteSpace(assemblyDir))
                possibleLocations.Add(Path.Combine(assemblyDir, "StateCountyServiceModels",
                    "cb_2025_us_county_5m.geojson"));
        }

        possibleLocations.Add(Path.Combine(Directory.GetCurrentDirectory(), "StateCountyServiceModels",
            "cb_2025_us_county_5m.geojson"));

        foreach (var location in possibleLocations)
            if (File.Exists(location))
                return location;

        return possibleLocations[0];
    }
}