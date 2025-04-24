using Avalonia.Controls;

namespace PointlessWaymarks.AvaloniaCommon.BoolDataEntry;

public partial class BoolDataEntryControl : UserControl
{
    public BoolDataEntryControl()
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