using System.ComponentModel;

namespace PointlessWaymarks.AvaloniaCommon.ColumnSort
{
    public class SortDescription
    {
        public SortDescription(string propertyName, ListSortDirection direction)
        {
            if (direction != ListSortDirection.Ascending && direction != ListSortDirection.Descending)
                throw new InvalidEnumArgumentException("direction", (int)direction, typeof(ListSortDirection));

            PropertyName = propertyName;
            Direction = direction;
        }

        /// <summary>
        /// Property name to sort by.
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// Sort direction.
        /// </summary>
        public ListSortDirection Direction { get; set; }
    }
}
