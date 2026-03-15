using System.Windows;
using PointlessWaymarks.LlamaAspects;
using PointlessWaymarks.WpfCommon.Status;

namespace PointlessWaymarks.WpfCommon.LocationPicker;

[NotifyPropertyChanged]
[StaThreadConstructorGuard]
public partial class LocationPickerWindow
{
    public LocationPickerWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    public LocationPickerContext? LocationPicker { get; set; }
    public required StatusControlContext StatusContext { get; set; }
    public string WindowTitle { get; set; } = "Location Picker";

    private async void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        DialogResult = false;
    }

    private async void ChooseLocationButton_OnClick(object sender, RoutedEventArgs e)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        if (LocationPicker!.HasValidationIssues)
        {
            await StatusContext.ToastError("Validation Error...");
            return;
        }

        DialogResult = true;
    }

    /// <summary>
    ///     Creates a new instance - this method can be called from any thread and will
    ///     switch to the UI thread as needed. Does not show the window - consider using
    ///     PositionWindowAndShowOnUiThread() from the WindowInitialPositionHelpers.
    /// </summary>
    public static async Task<LocationPickerWindow> CreateInstance(double initialLatitude, double initialLongitude,
        double? initialElevation, string windowTitle = "Location Picker",
        string serializedMapIcons = "", string calTopoApiKey = "",
        Guid? geoNamesSettingsId = null)
    {
        await ThreadSwitcher.ResumeForegroundAsync();

        var window = new LocationPickerWindow
        {
            WindowTitle = windowTitle,
            StatusContext = await StatusControlContext.CreateInstance()
        };

        await ThreadSwitcher.ResumeBackgroundAsync();

        window.LocationPicker = await LocationPickerContext.CreateInstance(window.StatusContext,
            initialLatitude, initialLongitude, initialElevation,
            serializedMapIcons, calTopoApiKey, geoNamesSettingsId);
        await window.LocationPicker.LoadData();

        await ThreadSwitcher.ResumeForegroundAsync();

        return window;
    }
}
