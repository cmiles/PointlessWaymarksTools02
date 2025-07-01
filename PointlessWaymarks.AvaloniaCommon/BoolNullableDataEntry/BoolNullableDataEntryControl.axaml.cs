using Avalonia.Controls;
using PointlessWaymarks.AvaloniaCommon.BoolDataEntry;

namespace PointlessWaymarks.AvaloniaCommon.BoolNullableDataEntry;

public partial class BoolNullableDataEntryControl : UserControl
{
    public BoolNullableDataEntryControl()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        
        if (DataContext is BoolDataEntryContext context)
        {
            context.CheckForChangesAndValidate();
        }
    }
} 