using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace PointlessWaymarks.WpfCommon.Utility;

public class ObservableRangeCollection<T> : ObservableCollection<T>
{
    public ObservableRangeCollection()
    {
    }

    public ObservableRangeCollection(IEnumerable<T> collection) : base(collection)
    {
    }

    public ObservableRangeCollection(List<T> list) : base(list)
    {
    }

    public void AddRange(IEnumerable<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        CheckReentrancy();

        var list = collection as IList<T> ?? collection.ToList();
        if (list.Count == 0) return;

        foreach (var item in list)
        {
            Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public void ReplaceRange(IEnumerable<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        CheckReentrancy();

        Items.Clear();
        foreach (var item in collection)
        {
            Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
