using System.Collections.ObjectModel;

namespace PointlessWaymarks.AvaloniaCommon;

public interface IStandardListWithContext<T> : IListSelectionWithContext<T>
{
    ObservableCollection<T> Items { get; init; }
}