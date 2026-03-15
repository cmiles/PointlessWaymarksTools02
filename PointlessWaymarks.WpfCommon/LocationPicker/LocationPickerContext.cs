using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using PointlessWaymarks.CommonTools;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.SpatialTools;
using PointlessWaymarks.WpfCommon.ChangesAndValidation;
using PointlessWaymarks.WpfCommon.ConversionDataEntry;
using PointlessWaymarks.WpfCommon.Elevation;
using PointlessWaymarks.WpfCommon.GeoSearch;
using PointlessWaymarks.WpfCommon.Status;
using PointlessWaymarks.WpfCommon.Utility;
using PointlessWaymarks.WpfCommon.WebViewVirtualDomain;
using PointlessWaymarks.WpfCommon.WpfHtml;

namespace PointlessWaymarks.WpfCommon.LocationPicker;

[NotifyPropertyChanged]
[GenerateStatusCommands]
public partial class LocationPickerContext : IHasChanges, ICheckForChangesAndValidation,
    IHasValidationIssues, IWebViewMessenger
{
    public LocationPickerContext(StatusControlContext statusContext, string serializedMapIcons,
        GeoSearchContext factoryLocationSearchContext, double initialLatitude, double initialLongitude,
        double? initialElevation, string calTopoApiKey = "")
    {
        StatusContext = statusContext;

        InitialLatitude = initialLatitude;
        InitialLongitude = initialLongitude;
        InitialElevation = initialElevation;

        BuildCommands();

        FromWebView = new WorkQueue<FromWebViewMessage>
        {
            Processor = ProcessFromWebView
        };

        ToWebView = new WorkQueue<ToWebViewRequest>(true);

        MapPreviewNavigationManager = (uri, _) =>
        {
            StatusContext.RunFireAndForgetBlockingTask(async () =>
            {
                await ThreadSwitcher.ResumeForegroundAsync();
                ProcessHelpers.OpenUrlInExternalBrowser(uri.OriginalString);
            });
        };

        this.SetupCmsLeafletPointChooserMapHtmlAndJs("Map", initialLatitude,
            initialLongitude, serializedMapIcons, calTopoApiKey);

        PropertyChanged += OnPropertyChanged;

        LocationSearchContext = factoryLocationSearchContext;

        LocationSearchContext.LocationSelected += (_, args) =>
        {
            var centerData = new MapJsonCoordinateDto(args.Latitude, args.Longitude, "CenterCoordinateRequest");

            var serializedData = JsonSerializer.Serialize(centerData);

            ToWebView.Enqueue(new JsonData { Json = serializedData });
        };
    }

    public bool BroadcastLatLongChange { get; set; } = true;
    public ConversionDataEntryContext<double?>? ElevationEntry { get; set; }
    public double? InitialElevation { get; set; }
    public double InitialLatitude { get; set; }
    public double InitialLongitude { get; set; }
    public ConversionDataEntryContext<double>? LatitudeEntry { get; set; }
    public GeoSearchContext LocationSearchContext { get; set; }
    public ConversionDataEntryContext<double>? LongitudeEntry { get; set; }
    public SpatialBounds? MapBounds { get; set; }
    public Action<Uri, string> MapPreviewNavigationManager { get; set; }
    public StatusControlContext StatusContext { get; set; }

    public void CheckForChangesAndValidationIssues()
    {
        HasChanges = PropertyScanners.ChildPropertiesHaveChanges(this);
        HasValidationIssues = PropertyScanners.ChildPropertiesHaveValidationIssues(this);
    }

    public bool HasChanges { get; set; }
    public bool HasValidationIssues { get; set; }
    public WorkQueue<FromWebViewMessage> FromWebView { get; set; }
    public WorkQueue<ToWebViewRequest> ToWebView { get; set; }

    [NonBlockingCommand]
    public async Task CenterMapOnSelectedLocation()
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var centerData =
            new MapJsonCoordinateDto(LatitudeEntry!.UserValue, LongitudeEntry!.UserValue, "CenterCoordinateRequest");

        var serializedData = JsonSerializer.Serialize(centerData);

        ToWebView.Enqueue(JsonData.CreateRequest(serializedData));
    }

    public static async Task<LocationPickerContext> CreateInstance(StatusControlContext statusContext,
        double initialLatitude, double initialLongitude, double? initialElevation,
        string serializedMapIcons = "", string calTopoApiKey = "",
        Guid? geoNamesSettingsId = null)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var factoryLocationSearchContext =
            await GeoSearchContext.CreateInstance(statusContext, geoNamesSettingsId);

        return new LocationPickerContext(statusContext, serializedMapIcons, factoryLocationSearchContext,
            initialLatitude, initialLongitude, initialElevation, calTopoApiKey);
    }

    [BlockingCommand]
    public async Task GetElevation()
    {
        if (LatitudeEntry!.HasValidationIssues || LongitudeEntry!.HasValidationIssues)
        {
            await StatusContext.ToastError("Lat Long is not valid");
            return;
        }

        var possibleElevation =
            await ElevationGuiHelper.GetElevation(LatitudeEntry.UserValue, LongitudeEntry.UserValue, StatusContext);

        if (possibleElevation != null) ElevationEntry!.UserText = possibleElevation.Value.MetersToFeet().ToString("N0");
    }

    private void LatitudeLongitudeChangeBroadcast()
    {
        if (BroadcastLatLongChange && !LatitudeEntry!.HasValidationIssues && !LongitudeEntry!.HasValidationIssues)
        {
            var centerData = new MapJsonCoordinateDto(LatitudeEntry.UserValue, LongitudeEntry.UserValue,
                "MoveUserLocationSelection");

            var serializedData = JsonSerializer.Serialize(centerData);

            ToWebView.Enqueue(JsonData.CreateRequest(serializedData));
        }
    }

    public async Task LoadData()
    {
        ElevationEntry =
            await ConversionDataEntryContext<double?>.CreateInstance(
                ConversionDataEntryHelpers.DoubleNullableConversion);
        ElevationEntry.ValidationFunctions = [SpatialValueValidations.ElevationValidation];
        ElevationEntry.ComparisonFunction = (o, u) => (o == null && u == null) || o.IsApproximatelyEqualTo(u, .001);
        ElevationEntry.Title = "Elevation (feet)";
        ElevationEntry.HelpText = "Elevation in Feet";
        ElevationEntry.ReferenceValue = InitialElevation;
        ElevationEntry.UserText = InitialElevation?.ToString("N0") ?? string.Empty;

        LatitudeEntry =
            await ConversionDataEntryContext<double>.CreateInstance(ConversionDataEntryHelpers.DoubleConversion);
        LatitudeEntry.ValidationFunctions = [SpatialValueValidations.LatitudeValidation];
        LatitudeEntry.ComparisonFunction = (o, u) => o.IsApproximatelyEqualTo(u, .000001);
        LatitudeEntry.Title = "Latitude";
        LatitudeEntry.HelpText = "In DDD.DDDDDD°";
        LatitudeEntry.ReferenceValue = InitialLatitude;
        LatitudeEntry.UserText = InitialLatitude.ToString("F6");
        LatitudeEntry.PropertyChanged += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(args.PropertyName)) return;
            if (args.PropertyName == nameof(LatitudeEntry.UserValue)) LatitudeLongitudeChangeBroadcast();
        };

        LongitudeEntry =
            await ConversionDataEntryContext<double>.CreateInstance(ConversionDataEntryHelpers.DoubleConversion);
        LongitudeEntry.ValidationFunctions = [SpatialValueValidations.LongitudeValidation];
        LongitudeEntry.ComparisonFunction = (o, u) => o.IsApproximatelyEqualTo(u, .000001);
        LongitudeEntry.Title = "Longitude";
        LongitudeEntry.HelpText = "In DDD.DDDDDD°";
        LongitudeEntry.ReferenceValue = InitialLongitude;
        LongitudeEntry.UserText = InitialLongitude.ToString("F6");
        LongitudeEntry.PropertyChanged += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(args.PropertyName)) return;
            if (args.PropertyName == nameof(LongitudeEntry.UserValue)) LatitudeLongitudeChangeBroadcast();
        };

        LatitudeLongitudeChangeBroadcast();

        PropertyScanners.SubscribeToChildHasChangesAndHasValidationIssues(this, CheckForChangesAndValidationIssues);
    }

    public async Task MapMessageReceived(string json)
    {
        await ThreadSwitcher.ResumeBackgroundAsync();

        var parsedJson = JsonNode.Parse(json);

        if (parsedJson == null) return;

        var messageType = parsedJson["messageType"]?.ToString() ?? string.Empty;

        if (messageType.Equals("userSelectedLatitudeLongitudeChanged",
                StringComparison.InvariantCultureIgnoreCase))
        {
            var latitude = parsedJson["latitude"]?.GetValue<double>();
            var longitude = parsedJson["longitude"]?.GetValue<double>();

            if (latitude == null || longitude == null) return;

            BroadcastLatLongChange = false;

            LatitudeEntry!.UserText = latitude.Value.ToString("F6");
            LongitudeEntry!.UserText = longitude.Value.ToString("F6");

            BroadcastLatLongChange = true;
        }

        if (messageType == "mapBoundsChange")
            try
            {
                var bounds = parsedJson["bounds"];
                var northEast = bounds?["_northEast"];
                var southWest = bounds?["_southWest"];

                if (northEast?["lat"] is { } neLatNode &&
                    northEast["lng"] is { } neLngNode &&
                    southWest?["lat"] is { } swLatNode &&
                    southWest["lng"] is { } swLngNode &&
                    neLatNode.GetValue<double?>() is { } neLat &&
                    neLngNode.GetValue<double?>() is { } neLng &&
                    swLatNode.GetValue<double?>() is { } swLat &&
                    swLngNode.GetValue<double?>() is { } swLng)
                    MapBounds = new SpatialBounds(neLat, neLng, swLat, swLng);
                else
                    throw new NullReferenceException(
                        $"A mapBoundsChange message in {nameof(LocationPickerContext)} contained null values.");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName)) return;

        if (!e.PropertyName.Contains("HasChanges") && !e.PropertyName.Contains("Validation"))
            CheckForChangesAndValidationIssues();
    }

    public Task ProcessFromWebView(FromWebViewMessage args)
    {
        if (!string.IsNullOrWhiteSpace(args.Message))
            StatusContext.RunFireAndForgetNonBlockingTask(async () => await MapMessageReceived(args.Message));
        return Task.CompletedTask;
    }
}