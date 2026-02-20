using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PointlessWaymarks.WpfCommon.Behaviors;

/// <summary>
/// When enabled on a container inside a ListBoxItem, any left mouse click within
/// that container will select the corresponding item in the parent ListBox.
/// </summary>
public static class SelectGrandParentListItemOnChildClick
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(SelectGrandParentListItemOnChildClick),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) =>
        (bool)obj.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject obj, bool value) =>
        obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element) return;

        if ((bool)e.NewValue)
            element.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        else
            element.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
    }

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DependencyObject source) return;

        var listBoxItem = FindParent<ListBoxItem>(source);
        if (listBoxItem == null) return;

        var listBox = FindParent<ListBox>(listBoxItem);
        if (listBox is null) return;

        var listBoxGrandParentItem = FindParent<ListBoxItem>(listBox);
        if (listBoxGrandParentItem == null) return;

        var listBoxGrandParent = FindParent<ListBox>(listBoxGrandParentItem);

        if (listBoxGrandParent != null)
            listBoxGrandParent.SelectedItem = listBoxGrandParentItem.DataContext;
        else
            listBoxGrandParentItem.IsSelected = true;
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var current = child;

        while (current != null)
        {
            // Try logical, then visual tree
            current = LogicalTreeHelper.GetParent(current) ??
                      (current is Visual v ? VisualTreeHelper.GetParent(v) : null);

            if (current is T typed) return typed;
        }

        return null;
    }
}