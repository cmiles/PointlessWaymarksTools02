namespace PointlessWaymarks.AvaloniaCommon.ColumnSort;

public static class ListContextSortHelpers
{
    public static async Task SortList(List<SortDescription>? listSorts, object? items)
    {
        if (items == null) return;

        await ThreadSwitcher.ResumeForegroundAsync();

        //TODO: Sort the list using the SortDescriptions

        //if (items is ICollectionView collectionView)
        //{
        //    collectionView.SortDescriptions.Clear();

        //    if (listSorts == null || listSorts.Count < 1) return;

        //    foreach (var loopSorts in listSorts) 
        //        collectionView.SortDescriptions.Add(loopSorts);
        //}
    }
} 