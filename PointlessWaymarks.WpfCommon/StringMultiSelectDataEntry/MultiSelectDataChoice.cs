using PointlessWaymarks.LlamaAspects;

namespace PointlessWaymarks.WpfCommon.StringMultiSelectDataEntry;

[NotifyPropertyChanged]
public partial class MultiSelectDataChoice
{
    public string DataString { get; set; } = string.Empty;
    public string DisplayString { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}
