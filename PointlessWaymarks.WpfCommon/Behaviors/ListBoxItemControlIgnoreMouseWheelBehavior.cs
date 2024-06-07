﻿using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;
using PointlessWaymarks.WpfCommon.Utility;

namespace PointlessWaymarks.WpfCommon.Behaviors;

/// <summary>
///     For a UIElement inside a ListBoxItem ignores and passes mouse wheel events to the
///     list.
/// </summary>
public sealed class ListBoxItemControlIgnoreMouseWheelBehavior : Behavior<UIElement>
{
    private void AssociatedObject_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;

        var e2 = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent
        };

        try
        {
            var listItem = XamlHelpers.FindParent<ListBoxItem>(AssociatedObject);
            listItem?.RaiseEvent(e2);
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
        }
    }

    //http://stackoverflow.com/questions/2189053/disable-mouse-wheel-on-itemscontrol-in-wpf
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PreviewMouseWheel += AssociatedObject_PreviewMouseWheel;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.PreviewMouseWheel -= AssociatedObject_PreviewMouseWheel;
        base.OnDetaching();
    }
}