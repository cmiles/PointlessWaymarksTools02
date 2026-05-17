using System.Windows;

namespace PointlessWaymarks.WpfCommon.ColumnSort;

/// <summary>
///     Interaction logic for ColumnSortControl.xaml
/// </summary>
public partial class ColumnSortControl
{
    public static readonly DependencyProperty ButtonHeightProperty = DependencyProperty.Register(
        nameof(ButtonHeight), typeof(double), typeof(ColumnSortControl), new PropertyMetadata(24D));

    public ColumnSortControl()
    {
        InitializeComponent();
    }

    public double ButtonHeight
    {
        get => (double)GetValue(ButtonHeightProperty);
        set => SetValue(ButtonHeightProperty, value);
    }
}