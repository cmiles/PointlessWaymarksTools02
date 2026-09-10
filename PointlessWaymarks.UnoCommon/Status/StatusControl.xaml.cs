using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PointlessWaymarks.UnoCommon.Status;

public sealed partial class StatusControl : UserControl
{
    private StatusControlContext? _subscribedContext;

    public StatusControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (_subscribedContext != null)
        {
            _subscribedContext.StatusLog.CollectionChanged -= OnStatusLogCollectionChanged;
            _subscribedContext = null;
        }

        if (args.NewValue is StatusControlContext newContext)
        {
            _subscribedContext = newContext;
            _subscribedContext.StatusLog.CollectionChanged += OnStatusLogCollectionChanged;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_subscribedContext != null)
        {
            _subscribedContext.StatusLog.CollectionChanged -= OnStatusLogCollectionChanged;
            _subscribedContext = null;
        }
    }

    private void OnStatusLogCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is { Count: > 0 } && e.NewItems[0] != null)
        {
            try
            {
                StatusLogListView?.ScrollIntoView(e.NewItems[0]!);
            }
            catch
            {
                // Ignore scroll exceptions during teardown/layout transitions
            }
        }
    }

    private void OnMessageBoxButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string buttonText } && DataContext is StatusControlContext context)
        {
            context.UserMessageBoxResponseCommand.Execute(buttonText);
        }
    }
}
