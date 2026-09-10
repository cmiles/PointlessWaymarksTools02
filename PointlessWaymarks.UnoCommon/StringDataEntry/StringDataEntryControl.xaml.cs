using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PointlessWaymarks.UnoCommon.StringDataEntry;

public sealed partial class StringDataEntryControl : UserControl
{
    public static readonly DependencyProperty ValueTextBoxWidthProperty =
        DependencyProperty.Register(
            nameof(ValueTextBoxWidth),
            typeof(double),
            typeof(StringDataEntryControl),
            new PropertyMetadata(double.NaN, ValueTextBoxWidthChangedCallback));

    public StringDataEntryControl()
    {
        InitializeComponent();
    }

    public double ValueTextBoxWidth
    {
        get => (double)GetValue(ValueTextBoxWidthProperty);
        set => SetValue(ValueTextBoxWidthProperty, value);
    }

    private static void ValueTextBoxWidthChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not StringDataEntryControl control || e.NewValue is not double widthValue) return;
        if (double.IsNaN(widthValue)) return;
        control.ValueTextBox.HorizontalAlignment = HorizontalAlignment.Left;
        control.ValueTextBox.Width = widthValue;
    }
}
