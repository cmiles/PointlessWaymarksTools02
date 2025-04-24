using Avalonia;
using Avalonia.Controls;
using Avalonia.Reactive;

namespace PointlessWaymarks.AvaloniaCommon.StringDataEntry;

public partial class StringDataEntryControl : UserControl
{
    public static readonly StyledProperty<double> ValueTextBoxWidthProperty =
        AvaloniaProperty.Register<StringDataEntryControl, double>(
            nameof(ValueTextBoxWidth),
            double.NaN);

    public StringDataEntryControl()
    {
        InitializeComponent();
        
        this.GetObservable(ValueTextBoxWidthProperty).Subscribe(new AnonymousObserver<double>(OnValueTextBoxWidthChanged));
    }

    public double ValueTextBoxWidth
    {
        get => GetValue(ValueTextBoxWidthProperty);
        set => SetValue(ValueTextBoxWidthProperty, value);
    }

    private void OnValueTextBoxWidthChanged(double value)
    {
        if (double.IsNaN(value)) return;
        
        if (this.FindControl<TextBox>("ValueTextBox") is { } textBox)
        {
            textBox.Width = value;
            textBox.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        }
    }
} 