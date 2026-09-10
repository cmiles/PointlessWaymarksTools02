using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PointlessWaymarks.UnoCommon.StringDataEntry;

public sealed partial class StringDataEntryMultiLineFillControl : UserControl
{
    public static readonly DependencyProperty ValueTextBoxHeightProperty =
        DependencyProperty.Register(
            nameof(ValueTextBoxHeight),
            typeof(double),
            typeof(StringDataEntryMultiLineFillControl),
            new PropertyMetadata(double.NaN, ValueTextBoxHeightChangedCallback));

    public static readonly DependencyProperty ValueTextBoxWidthProperty =
        DependencyProperty.Register(
            nameof(ValueTextBoxWidth),
            typeof(double),
            typeof(StringDataEntryMultiLineFillControl),
            new PropertyMetadata(double.NaN, ValueTextBoxWidthChangedCallback));

    public StringDataEntryMultiLineFillControl()
    {
        InitializeComponent();
    }

    public double ValueTextBoxHeight
    {
        get => (double)GetValue(ValueTextBoxHeightProperty);
        set => SetValue(ValueTextBoxHeightProperty, value);
    }

    public double ValueTextBoxWidth
    {
        get => (double)GetValue(ValueTextBoxWidthProperty);
        set => SetValue(ValueTextBoxWidthProperty, value);
    }

    private static void ValueTextBoxHeightChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not StringDataEntryMultiLineFillControl control || e.NewValue is not double heightValue) return;
        if (double.IsNaN(heightValue)) return;
        control.ValueTextBox.Height = heightValue;
    }

    private static void ValueTextBoxWidthChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not StringDataEntryMultiLineFillControl control || e.NewValue is not double widthValue) return;
        if (double.IsNaN(widthValue)) return;
        control.ValueTextBox.HorizontalAlignment = HorizontalAlignment.Left;
        control.ValueTextBox.Width = widthValue;
    }
}
