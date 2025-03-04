using System.Collections.ObjectModel;

namespace PointlessWaymarks.AvaloniaToolkit.Utility;

public static class ObservableCollectionHelpers
{
    public static async Task AddOnUiThread<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
    {
        await UiThreadSwitcher.ResumeForegroundAsync();

        foreach (var item in items) collection.Add(item);
    }

    public static async Task AddOnUiThread<T>(this ObservableCollection<T> collection, T item)
    {
        await UiThreadSwitcher.ResumeForegroundAsync();

        collection.Add(item);
    }

    public static async Task ClearOnUiThread<T>(this ObservableCollection<T> collection)
    {
        await UiThreadSwitcher.ResumeForegroundAsync();

        collection.Clear();
    }

    public static async Task RemoveOnUiThread<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
    {
        await UiThreadSwitcher.ResumeForegroundAsync();

        foreach (var item in items) collection.Remove(item);
    }

    public static async Task RemoveOnUiThread<T>(this ObservableCollection<T> collection, T item)
    {
        await UiThreadSwitcher.ResumeForegroundAsync();

        collection.Remove(item);
    }
}