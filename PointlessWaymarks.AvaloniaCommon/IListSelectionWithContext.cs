using PointlessWaymarks.AvaloniaCommon.Status;

namespace PointlessWaymarks.AvaloniaCommon;

public interface IListSelectionWithContext<T>
{
    StatusControlContext StatusContext { get; set; }
    T? SelectedListItem();
    List<T> SelectedListItems();
}