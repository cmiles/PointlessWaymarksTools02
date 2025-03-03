using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;

namespace PointlessWaymarks.AvaloniaToolkit.Behaviors;

/// <summary>
///     A sync behaviour for a MultiSelector from https://github.com/itsChris/WpfMvvmDataGridMultiselect
/// </summary>
public class DataGridSelectedItemsBindingBehavior : StyledElementBehavior<DataGrid>
{
    public static readonly StyledProperty<IList?> SynchronizedSelectedItemsProperty =
        AvaloniaProperty.Register<DataGridSelectedItemsBindingBehavior, IList?>(nameof(SynchronizedSelectedItems));

    private DataGrid? _dataGrid;
    private bool _pause;

    public DataGridSelectedItemsBindingBehavior()
    {
        SynchronizedSelectedItemsProperty.Changed.AddClassHandler<DataGridSelectedItemsBindingBehavior>(
            AvaloniaPropertyChanged);
    }

    public IList? SynchronizedSelectedItems
    {
        get => GetValue(SynchronizedSelectedItemsProperty);
        set => SetValue(SynchronizedSelectedItemsProperty, value);
    }

    private void AvaloniaPropertyChanged(DataGridSelectedItemsBindingBehavior source,
        AvaloniaPropertyChangedEventArgs args)
    {
        if (_dataGrid is null)
        {
            if (source.AssociatedObject is null) return;
            _dataGrid = source.AssociatedObject;
            _dataGrid.SelectionChanged += DataGridOnSelectionChanged;
        }

        if (args.Property.Name == nameof(SynchronizedSelectedItems))
        {
            if (SynchronizedSelectedItems is INotifyCollectionChanged collectionChanged)
                collectionChanged.CollectionChanged += SynchronizedSelectedItems_CollectionChanged;

            SynchronizeLists(SynchronizedSelectedItems, AssociatedObject?.SelectedItems);
        }
    }

    private void DataGridOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_pause) SynchronizeLists(AssociatedObject?.SelectedItems, SynchronizedSelectedItems);
    }

    private void SynchronizeLists(IList? changed, IList? target)
    {
        if (target == null) return;

        _pause = true;

        if (changed == null)
        {
            target.Clear();
        }
        else
        {
            // Remove items from target that are not in changed
            for (var i = target.Count - 1; i >= 0; i--)
                if (!changed.Contains(target[i]))
                    target.RemoveAt(i);

            // Add items to target that are in changed but not in target
            foreach (var item in changed)
                if (!target.Contains(item))
                    target.Add(item);
        }

        _pause = false;
    }


    private void SynchronizedSelectedItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_pause) SynchronizeLists(SynchronizedSelectedItems, AssociatedObject?.SelectedItems);
    }
}