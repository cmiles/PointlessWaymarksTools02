using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Xaml.Behaviors;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.WpfCommon.Behaviors;

public class ToggleButtonSelectsParentListItem : Behavior<ToggleButton>
{
    private void AssociatedObjectOnClick(object? sender, RoutedEventArgs e)
    {
        if (sender == null) return;
        var possibleParent = XamlHelpers.FindParent<ListBoxItem>(sender as DependencyObject);
        if (possibleParent == null) return;
        var parentListBox = XamlHelpers.FindParent<ListBox>(possibleParent);
        if (parentListBox != null)
            parentListBox.SelectedItem = possibleParent.DataContext;
        else
            possibleParent.IsSelected = true;
    }

    protected override void OnAttached()
    {
        AssociatedObject.Click += AssociatedObjectOnClick;
    }
}