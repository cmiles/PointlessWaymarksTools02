using Avalonia;
using Avalonia.Controls;
using Avalonia.Reactive;

namespace PointlessWaymarks.AvaloniaCommon.StringDataEntry;

public partial class StringDataEntryMultiLineControl : UserControl
{
    public static readonly StyledProperty<double> ValueTextBoxWidthProperty =
        AvaloniaProperty.Register<StringDataEntryMultiLineControl, double>(
            nameof(ValueTextBoxWidth),
            double.NaN);

    public static readonly StyledProperty<double> ValueTextBoxHeightProperty =
        AvaloniaProperty.Register<StringDataEntryMultiLineControl, double>(
            nameof(ValueTextBoxHeight),
            double.NaN);

    public StringDataEntryMultiLineControl()
    {
        InitializeComponent();
        
        this.GetObservable(ValueTextBoxWidthProperty).Subscribe(new AnonymousObserver<double>(OnValueTextBoxWidthChanged));
        this.GetObservable(ValueTextBoxHeightProperty).Subscribe(new AnonymousObserver<double>(OnValueTextBoxHeightChanged));
    }

    public double ValueTextBoxWidth
    {
        get => GetValue(ValueTextBoxWidthProperty);
        set => SetValue(ValueTextBoxWidthProperty, value);
    }

    public double ValueTextBoxHeight
    {
        get => GetValue(ValueTextBoxHeightProperty);
        set => SetValue(ValueTextBoxHeightProperty, value);
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

    private void OnValueTextBoxHeightChanged(double value)
    {
        if (double.IsNaN(value)) return;
        
        if (this.FindControl<TextBox>("ValueTextBox") is { } textBox)
        {
            textBox.Height = value;
        }
    }
} 