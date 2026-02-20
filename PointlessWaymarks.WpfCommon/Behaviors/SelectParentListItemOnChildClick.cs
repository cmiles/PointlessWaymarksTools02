using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.WpfCommon.Behaviors;

/// <summary>
///     Attached property that, when set on a container inside a ListBoxItem,
///     selects that ListBoxItem whenever any child Button or ToggleButton is
///     clicked. Uses the bubbling ButtonBase.Click routed event so it covers
///     all descendants without modifying individual buttons.
/// </summary>
public static class SelectParentListItemOnChildClick
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(SelectParentListItemOnChildClick),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element) return;

        if ((bool)e.NewValue)
            element.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnButtonClick));
        else
            element.RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnButtonClick));
    }

    private static void OnButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not DependencyObject container) return;
        var listBoxItem = XamlHelpers.FindParent<ListBoxItem>(container);
        if (listBoxItem == null) return;
        var parentListBox = XamlHelpers.FindParent<ListBox>(listBoxItem);
        if (parentListBox != null)
            parentListBox.SelectedItem = listBoxItem.DataContext;
        else
            listBoxItem.IsSelected = true;
    }
}
